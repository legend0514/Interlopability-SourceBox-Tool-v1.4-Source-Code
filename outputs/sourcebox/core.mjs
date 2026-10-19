import net from 'node:net';
import {createCube} from './cube.mjs';
import {createPortals} from './portals.mjs';
import {createWorldResolver,canInteractInWorld} from './world-routing.mjs';
import {randomUUID} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {preflight} from './preflight.mjs';
export function createCore({config=preflight(),log=console.log}={}){
 const clients=new Map();
 const resolveWorld=createWorldResolver(config);
 const vec=x=>Array.isArray(x)&&x.length===3&&x.every(v=>Number.isFinite(v)&&Math.abs(v)<=1000000);
 const validProps=(props,game)=>Array.isArray(props)&&props.length<=4&&new Set(props.map(p=>p?.id)).size===props.length&&props.every(p=>p&&config.physics.objects?.some(d=>d.id===p.id&&d.owner===game)&&typeof p.alive==='boolean'&&(!p.alive||(vec(p.position)&&vec(p.rotation)&&vec(p.velocity)&&vec(p.angularVelocity))));
 const event=(type,data={})=>log(JSON.stringify({time:new Date().toISOString(),type,...data}));
 const emit=(c,m)=>{if(!c.socket.destroyed){if(c.socket.writableLength>65536)c.socket.destroy();else c.socket.write(JSON.stringify(m)+'\n');}};
 const cube=createCube({definition:config.physics.cube,clients,emit,log:event});
 const portals=config.portals?createPortals({config:{...config.portals,destinationWorlds:config.destinationWorlds},clients,emit,log:event}):null;
 const timer=setInterval(()=>cube.tick(),100);timer.unref();
 const server=net.createServer(socket=>{
  let buffer=Buffer.alloc(0),client=null;
  const send=m=>{if(!socket.destroyed)socket.write(JSON.stringify(m)+'\n');};
  const reject=reason=>{event('rejected',{game:client?.game,reason});socket.destroy();};
  socket.setNoDelay(true);socket.setTimeout(config.runtime.timeoutMs,()=>reject('timeout'));
  socket.on('error',e=>event('socket_error',{code:e.code}));
  const broadcast=(m)=>{for(const c of clients.values())if(c!==client){if(c.socket.writableLength>65536)c.socket.destroy();else c.socket.write(JSON.stringify(m)+'\n');}};
  socket.on('close',()=>{if(client&&clients.get(client.game)===client){clients.delete(client.game);cube.disconnect(client);portals?.disconnect(client);broadcast({type:'remove',game:client.game,entity:'player:'+client.game,scope:'game'});event('disconnected',{game:client.game,session:client.session});}});
  socket.on('data',chunk=>{
   buffer=Buffer.concat([buffer,chunk]);
   for(;;){
    const i=buffer.indexOf(10);
    if(i<0){if(buffer.length>config.runtime.maxFrameBytes)reject('frame too large');return;}
    if(i>config.runtime.maxFrameBytes){reject('frame too large');return;}
    const raw=buffer.subarray(0,i);buffer=buffer.subarray(i+1);
    let m;try{m=JSON.parse(raw.toString('utf8'));}catch{reject('invalid JSON');return;}
    if(!m||typeof m!=='object'||Array.isArray(m)){reject('invalid message');return;}
    if(!client){
     if(m.type!=='hello'||m.protocol!==config.protocol||!(config.games.some(g=>g.id===m.game)||(config.gmod?.enabled&&m.game===config.gmod.id)||(config.hl1mp?.enabled&&m.game===config.hl1mp.id)||(config.cs2?.enabled&&m.game===config.cs2.id)||(config.portal2?.enabled&&m.game===config.portal2.id))||!Number.isSafeInteger(m.pid)||m.pid<=0){reject('invalid hello');return;}
     if(clients.has(m.game)){reject('duplicate game');return;}
     client={game:m.game,pid:m.pid,session:randomUUID(),seq:0,socket,cubeProtocol:m.cubeProtocol,portalProtocol:m.portalProtocol,portalTraverse:m.portalTraverse===true};clients.set(m.game,client);
     send({type:'welcome',protocol:config.protocol,session:client.session,portalsEnabled:config.portals?.enabled===true});for(const peer of clients.values())if(peer!==client&&peer.physics)send(peer.physics);event('connected',{game:m.game,pid:m.pid,session:client.session,portalTraverse:client.portalTraverse});cube.welcome(client);portals?.welcome(client);
    }else{
     if(!['heartbeat','state','cube_take','cube_frozen','cube_state','cube_sync','cube_ready','player_hit','scene','scene_interact','portal_sync','portal_fire','portal_clear','portal_ack','portal_world_ready'].includes(m.type)||!Number.isSafeInteger(m.seq)||m.seq<=client.seq||!Number.isFinite(m.timestamp)||m.timestamp<0){reject('invalid or stale heartbeat');return;}
     if(m.type.startsWith('portal_')){client.seq=m.seq;portals?.handle(client,m);send({type:'ack',seq:m.seq});continue;}
     if(m.type==='scene'){
      const cfg=config.gmodScene,ids=m.live;
      if(client.game!=='gmod'||!cfg||typeof m.generation!=='string'||m.generation.length>128||!Array.isArray(ids)||ids.length>cfg.maxEntities||new Set(ids).size!==ids.length||!ids.every(id=>Number.isSafeInteger(id)&&id>0&&id<=2147483647)||!Array.isArray(m.objects)||m.objects.length>cfg.batchSize||new Set(m.objects.map(o=>o?.id)).size!==m.objects.length||!m.objects.every(o=>o&&ids.includes(o.id)&&((o.kind??'prop')==='prop'?cfg.models.includes(o.model):o.kind==='npc'&&[...cfg.npcModels,...(cfg.addon?.npcModels??[])].includes(o.model))&&(o.sequence===undefined||(typeof o.sequence==='string'&&/^[a-zA-Z0-9_ .-]{0,96}$/.test(o.sequence)))&&(o.health===undefined||(Number.isFinite(o.health)&&o.health>=0&&o.health<=1000000))&&vec(o.position)&&vec(o.rotation)&&vec(o.velocity)&&vec(o.angularVelocity))){reject('invalid spawned scene');return;}
      client.seq=m.seq;if(client.sceneGeneration!==m.generation)client.scene=new Map();client.sceneGeneration=m.generation;client.sceneAt=Date.now();client.scene??=new Map();for(const id of client.scene.keys())if(!ids.includes(id))client.scene.delete(id);for(const o of m.objects)client.scene.set(o.id,o);
      broadcast({type:'scene',game:client.game,session:client.session+':'+m.generation,seq:m.seq,live:ids,objects:m.objects.map(o=>o.kind==='npc'?{...o,renderModel:'models/sourcebox/npc/'+o.model.slice(7)}:o)});send({type:'ack',seq:m.seq});
      if(Date.now()-(client.sceneLogged??0)>1000){event('scene',{game:client.game,count:ids.length});client.sceneLogged=Date.now();}continue;
     }
     if(m.type==='scene_interact'){
      client.seq=m.seq;send({type:'ack',seq:m.seq});
      const host=clients.get('gmod'),cfg=config.gmodScene.interactions,at=Date.now();
      const object=host?.scene?.get(m.id);
      if(at-(client.sceneWindow??0)>=1000){client.sceneWindow=at;client.sceneHits=0;}
      if(client.game==='gmod'||!client.hasState||at-client.lastStateAt>2000||!object||at-host.sceneAt>2000||m.session!==host.session+':'+host.sceneGeneration||!['use','hit'].includes(m.action)||(client.sceneHits??0)>=cfg.maxPerSecond)continue;
      const distance=Math.hypot(...client.position.map((v,i)=>v-object.position[i]));
      if(!canInteractInWorld(client.world,resolveWorld(object.position))||distance>(m.action==='use'?cfg.useDistance:cfg.hitDistance))continue;
      client.sceneHits=(client.sceneHits??0)+1;
      emit(host,{type:'scene_interact',generation:host.sceneGeneration,id:m.id,action:m.action,source:client.game,position:client.position,event:randomUUID()});
      event('scene_interaction',{from:client.game,id:m.id,action:m.action});continue;
     }
     if(m.type==='player_hit'){
      client.seq=m.seq;send({type:'ack',seq:m.seq});const target=(['hl2','tf2','gmod','hl1mp','cs2','portal2'].includes(m.target)&&m.target!==client.game)?m.target:((client.game==='gmod'||client.game==='hl1mp'||client.game==='cs2'||client.game==='portal2')?null:(client.game==='hl2'?'tf2':'hl2'));const peer=clients.get(target);const at=Date.now();
      if(at-(client.hitWindow??0)>=1000){client.hitWindow=at;client.hitCount=0;}
      if(config.combat.enabled&&client.hasState&&peer?.hasState&&canInteractInWorld(client.world,peer.world)&&(client.hitCount??0)<config.combat.maxHitsPerSecond){client.hitCount=(client.hitCount??0)+1;emit(peer,{type:'player_damage',source:client.game,sourceSession:client.session,seq:m.seq,amount:config.combat.damagePerHit});event('player_hit',{from:client.game,to:peer.game,amount:config.combat.damagePerHit});}
      continue;
     }
     if(m.type.startsWith('cube_')){client.seq=m.seq;if(m.type==='cube_sync')cube.welcome(client);else cube.handle(client,m);send({type:'ack',seq:m.seq});continue;}
     if(m.props!==undefined){
      if(!validProps(m.props,client.game)||m.timestamp<(client.lastPhysicsTimestamp??0)){reject('invalid physics ownership or state');return;}
      client.lastPhysicsTimestamp=m.timestamp;
     }
     if(m.type==='state'){
      if(!['B','C'].includes(config.phase)||m.entity!=='player:'+client.game||!vec(m.position)||!vec(m.rotation)||!vec(m.velocity)||typeof m.grounded!=='boolean'||m.timestamp<(client.lastTimestamp??0)){reject('invalid movement state');return;}
      if(client.game==='hl1mp'&&(!config.hl1mp.models[m.model]||(m.weapon&& !config.hl1mp.weapons[m.weapon]))){reject('invalid HL1 model or weapon');return;}
      if(client.game==='tf2'&&(!Number.isInteger(m.classId)||!config.assets.tf2Classes[m.classId]||![2,3].includes(m.team))){reject('invalid TF2 class or team');return;}
      if(client.game==='tf2'&&m.weapon!==undefined&&(typeof m.weapon!=='string'||m.weapon.length>96||!/^([a-z0-9_]+)?$/.test(m.weapon))){reject('invalid weapon name');return;}
      client.seq=m.seq;send({type:'ack',seq:m.seq});
      if(m.timestamp===client.lastTimestamp)continue;
      client.lastTimestamp=m.timestamp;client.hasState=true;client.position=m.position;client.rotation=m.rotation;client.lastStateAt=Date.now();
      const world=resolveWorld(m.position);if(world!==client.world)event('player_world',{game:client.game,from:client.world??null,to:world});client.world=world;
      const state={entity:m.entity,seq:m.seq,timestamp:m.timestamp,position:m.position,rotation:m.rotation,velocity:m.velocity,grounded:m.grounded,world};
      if(m.health!==undefined){
       if(!Number.isSafeInteger(m.health)||m.health<0||m.health>1000000){reject('invalid health telemetry');return;}
       state.health=m.health;
       if(client.health!==undefined&&client.health!==m.health)event('player_health',{game:client.game,before:client.health,after:m.health});
       client.health=m.health;
      }
      if(m.props!==undefined){
       state.props=m.props;client.physics={type:"physics",game:client.game,session:client.session,state:{seq:m.seq,timestamp:m.timestamp,props:m.props}};
      }
      if((client.game==='hl2'||client.game==='gmod')&&m.weapon!==undefined){if(typeof m.weapon!=='string'||(m.weapon!==''&&!config.assets.hl2Weapons?.[m.weapon])){reject('invalid HL2 weapon');return;}state.weapon=m.weapon;}
      if(client.game==='gmod'&&(m.model==='models/player/kleiner.mdl'||config.gmodScene.addon?.playerModels.includes(m.model))){
       state.model=m.model;if(typeof m.sequence==='string'&&/^[a-zA-Z0-9_ .-]{0,96}$/.test(m.sequence))state.sequence=m.sequence;
      }
      if(client.game==='hl1mp'){state.model=m.model;state.weapon=m.weapon??'';}
      if(client.game==='portal2'&&m.weapon==='weapon_portalgun')state.weapon=m.weapon;
      if(client.game==='cs2'){if(m.weapon==='weapon_sourcebox_portalgun')state.weapon=m.weapon;state.team=[2,3].includes(m.team)?m.team:3;}
      if(client.game==='tf2'){state.classId=m.classId;state.team=m.team;state.weapon=m.weapon??'';}
      for(const [from,to]of [...config.movement.directions,...(config.portal2?.enabled?config.portal2.peers.flatMap(p=>[['portal2',p],[p,'portal2']]):[]),...(config.cs2?.enabled?(config.cs2.peers??[]).flatMap(p=>[['cs2',p],[p,'cs2']]):[]),...(config.hl1mp?.enabled?config.hl1mp.peers.flatMap(p=>[['hl1mp',p],[p,'hl1mp']]):[]),...(config.gmod?.enabled?[...config.gmod.inbound.map(from=>[from,'gmod']),...config.gmod.outbound.map(to=>['gmod',to])]:[])])if(from===client.game){const peer=clients.get(to);if(peer){if(peer.socket.writableLength>65536)peer.socket.destroy();else peer.socket.write(JSON.stringify({type:'remote',game:client.game,session:client.session,state})+'\n');}}
      client.weapon=state.weapon??'';portals?.sample(client,state);event('state',{game:client.game,...state});
     }else{
      if(client.hasState){broadcast({type:'remove',game:client.game,entity:'player:'+client.game,scope:'player'});client.hasState=false;portals?.reset(client);}
      if(m.props!==undefined){client.physics={type:'physics',game:client.game,session:client.session,state:{seq:m.seq,timestamp:m.timestamp,props:m.props}};broadcast(client.physics);}
      client.seq=m.seq;send({type:'ack',seq:m.seq});event('heartbeat',{game:client.game,seq:m.seq,sourceTimestamp:m.timestamp,simultaneous:clients.size>=2});
     }
    }
   }
  });
 });
 return {server,clients,close:()=>{clearInterval(timer);for(const c of clients.values())c.socket.destroy();return new Promise(r=>server.close(r));}};
}
if(process.argv[1]===fileURLToPath(import.meta.url)){
 const config=preflight(),core=createCore({config});
 core.server.on('error',e=>{console.error(e.message);process.exitCode=1;});
 core.server.listen(config.runtime.port,config.runtime.host,()=>console.log(JSON.stringify({type:'listening',...config.runtime})));
 for(const signal of ['SIGINT','SIGTERM'])process.on(signal,()=>core.close());
}
