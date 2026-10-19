import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import {randomUUID} from 'node:crypto';
import {preflight} from './preflight.mjs';
import {createPortalSnapshotReader} from './portal-snapshot.mjs';
import {pendingCommands} from './gmod-commands.mjs';
import {mapTravelPaused} from './map-travel.mjs';
import {acquireBridgeLease} from './bridge-lease.mjs';
const portalSnapshots=createPortalSnapshotReader();let coreSession=null;
const config=preflight(),directory=process.argv[2];
const startedAt=Date.now();
if(!directory)throw Error('GMod data/sourcebox directory required');
const bridgeLease=await acquireBridgeLease(config.gmod.bridgeLeasePort);
fs.mkdirSync(directory,{recursive:true});
let socket,connected=false,seq=0,lastCounter='',fresh=0,latest=null,buffer='',peers={},physics={},cube=null,freeze=null,notice='',commandAck=0,commandSession='',connection=randomUUID(),sceneActions=[],playerDamage=[],portalEvents=[];
function send(m){if(!connected||socket.destroyed)return false;if(socket.writableLength>65536){socket.destroy();return false;}socket.write(JSON.stringify({...m,seq:++seq,timestamp:Math.floor(os.uptime()*1000)})+'\n');return true;}
let worldPaused=false,worldResumedAt=startedAt;
let worldReadErrorAt=0,worldReadySignature='';
function connect(){
 if(worldPaused){setTimeout(connect,1000);return;}
 socket=net.connect(config.runtime.port,config.runtime.host,()=>socket.write(JSON.stringify({type:'hello',protocol:1,game:'gmod',pid:process.pid,cubeProtocol:1,portalProtocol:1,portalTraverse:config.portals.gmodBackend?.traversal!=='native-addon'})+'\n'));
 socket.on('error',e=>console.log(JSON.stringify({type:'bridge_error',code:e.code})));
 socket.on('close',()=>{connected=false;peers={};physics={};cube=null;freeze=null;notice='';sceneActions=[];playerDamage=[];buffer='';coreSession=null;portalEvents=[];portalSnapshots.reset(null);setTimeout(connect,1000)});
 socket.on('data',b=>{buffer+=b.toString();if(buffer.length>65536){socket.destroy();return;}let i;while((i=buffer.indexOf('\n'))>=0){let m;try{m=JSON.parse(buffer.slice(0,i))}catch{socket.destroy();return;}buffer=buffer.slice(i+1);
 if(m.type==='welcome'){connected=true;seq=0;connection=randomUUID();coreSession=m.session;worldReadySignature='';portalSnapshots.reset(coreSession);console.log(JSON.stringify({type:'gmod_bridge_connected',session:m.session}));}
 if(m.type==='portal_snapshot')portalSnapshots.accept(m);
 if(m.type==='portal_notice'||m.type==='portal_traverse'){if((m.type==='portal_notice'||m.session===coreSession)&&portalEvents.length<128)portalEvents.push({...m,event:m.event??randomUUID(),received:Date.now(),localSession:latest?.session});}
 if(m.type==='remote')peers[m.game]={...m,received:Date.now()};
 if((m.type==='remote'||m.type==='physics')&&m.state.props)physics[m.game]={props:m.state.props,received:Date.now()};
 if(m.type==='remove'){delete peers[m.game];if(m.scope!=='player')delete physics[m.game];}
 if(m.type==='player_damage'){if(playerDamage.length<128)playerDamage.push({...m,event:randomUUID(),received:Date.now(),session:latest?.session});}
 if(m.type==='scene_interact'){if(sceneActions.length<128)sceneActions.push({...m,received:Date.now()});}
 if(m.type==='cube_owner'){cube=m;freeze=null;}
 if(m.type==='cube_state')cube=m;
 if(m.type==='cube_freeze')freeze=m;
 if(m.type==='cube_denied')notice=m.reason;
 }});
}
connect();
setInterval(()=>{
 const now=Date.now();
 let world;try{const file=path.join(directory,'world.json');if(fs.existsSync(file))world=JSON.parse(fs.readFileSync(file,'utf8'));worldReadErrorAt=0;}catch{
  // GMod file.Write briefly truncates the record. Send nothing during that
  // window, but do not tear down all proxies for a single incomplete read.
  if(!worldReadErrorAt)worldReadErrorAt=now;
  if(now-worldReadErrorAt<250)return;
  world=null;
 }
 const pause=mapTravelPaused(world,config.mapTravel.hub,config.worlds?.enabled?config.worlds.worlds.map(w=>w.id):[]);
 if(pause!==worldPaused){worldPaused=pause;if(!pause)worldResumedAt=now;latest=null;fresh=0;lastCounter='';console.log(JSON.stringify({type:'map_travel_isolation',paused:pause,map:world?.map}));if(pause&&socket&&!socket.destroyed)socket.destroy();}

 portalEvents=portalEvents.filter(m=>now-m.received<1500&&!latest?.portalAck?.includes(m.event)&&m.localSession===latest?.session);
 sceneActions=sceneActions.filter(m=>now-m.received<2000);
 playerDamage=playerDamage.filter(m=>now-m.received<2000&&!latest?.damageAck?.includes(m.event));
 if(latest?.sceneAck)sceneActions=sceneActions.filter(m=>!latest.sceneAck.includes(m.event));
 try{if(worldPaused)throw Error('map excursion');const file=path.join(directory,'outgoing.json');if(fs.statSync(file).mtimeMs>=worldResumedAt){const sample=JSON.parse(fs.readFileSync(file,'utf8'));const key=sample.session+':'+sample.counter;if(key!==lastCounter){lastCounter=key;fresh=now;latest=sample;}}}catch{}
 for(const [id,p]of Object.entries(peers))if(now-p.received>config.gmod.timeoutMs)delete peers[id];
 for(const [id,p]of Object.entries(physics))if(now-p.received>config.gmod.timeoutMs)delete physics[id];
 try{fs.writeFileSync(path.join(directory,'incoming.tmp'),JSON.stringify({connected,connection,coreSession,portals:portalSnapshots.read(),portalsEnabled:config.portals?.enabled===true,revision:now,peers:Object.values(peers),physics,cube,freeze,notice,commandAck,commandSession,sceneActions,playerDamage,portalEvents}));fs.renameSync(path.join(directory,'incoming.tmp'),path.join(directory,'incoming.json'));}catch(e){if(e.code!=='EPERM'&&e.code!=='EBUSY')console.error(e.message);}
 if(worldPaused||!connected||socket.destroyed)return;
 if(Array.isArray(world?.ready)){
  const ids=world.ready.filter(id=>config.destinationWorlds.worlds.some(w=>w.id===id)).sort(),signature=JSON.stringify(ids);
  if(signature!==worldReadySignature&&send({type:'portal_world_ready',session:coreSession,worlds:ids}))worldReadySignature=signature;
 }
 if(latest&&now-fresh<config.gmod.timeoutMs){
  if(commandSession!==latest.session){commandSession=latest.session;commandAck=0;}
  const pending=pendingCommands(latest,connection,commandAck);commandAck=pending.acknowledged;
  for(const command of pending.commands)if(['cube_take','cube_frozen','cube_ready','cube_sync','player_hit','portal_fire','portal_clear','portal_ack','portal_sync'].includes(command.message?.type)){const m=structuredClone(command.message);if(m.state)m.state.timestamp=Math.floor(os.uptime()*1000);if(send(m))commandAck=command.id;}
  if(latest.connection===connection&&latest.cube&&latest.cube.epoch===cube?.epoch&&cube?.owner==='gmod')send({type:'cube_state',epoch:latest.cube.epoch,state:{...latest.cube.state,timestamp:Math.floor(os.uptime()*1000)}});
  if(latest.scene){const objects=latest.scene.objects??[],live=objects.map(o=>o.id);for(let i=0;i<Math.max(1,objects.length);i+=config.gmodScene.batchSize)send({type:'scene',generation:latest.session,live,objects:objects.slice(i,i+config.gmodScene.batchSize)});}
  send(latest.state?{...latest.state,type:'state',entity:'player:gmod'}:{type:'heartbeat'});
 }else send({type:'heartbeat'});
},config.gmod.pollMs);

