import {randomUUID} from 'node:crypto';
import {createWorldPortalPlacement} from './world-portal-placement.mjs';
import {add,sub,scale,dot,aim,frame,wallHit,crossing,transformVector,transformLook,extent,entryVelocity} from './portal-math.mjs';
export function createPortals({config,clients,emit,log,now=Date.now}){
 const pairs=new Map(),pending=new Map(),history=new Map(),arrivals=new Map();let revision=0;
 const destinations=config.destinationWorlds?.enabled?config.destinationWorlds.worlds:[];
 const hub=config.destinationWorlds?.nativeMap??'sourcebox_room_001';
 const hubDestination={id:hub,spawn:[0,400,0],yaw:-90};
 const worldPlacement=createWorldPortalPlacement(destinations);
 const supports=(c,pair)=>(!pair.destination||pair.destination===hub||c.worldsReady?.includes(pair.destination))&&pair.endpoints.every(e=>!e?.world||e.world===hub||c.worldsReady?.includes(e.world));
 const snapshot=()=>({type:'portal_snapshot',revision,environment:config.environment,pairs:[...pairs.values()]});
 // One pair per frame keeps a full six-player snapshot below transport limits.
 const sendSnapshot=c=>{const state=snapshot();state.pairs=state.pairs.filter(pair=>supports(c,pair));const count=Math.max(1,state.pairs.length);for(let index=0;index<count;index++)emit(c,{...state,session:c.session,index,count,pairs:state.pairs.slice(index,index+1)});};
 const broadcast=()=>{for(const c of clients.values())if(c.portalProtocol===1)sendSnapshot(c);};
 const deny=(c,reason)=>{emit(c,{type:'portal_notice',reason});log('portal_rejected',{game:c.game,reason});};
 const remove=c=>{history.delete(c.game);pending.delete(c.game);arrivals.delete(c.game);if(pairs.delete(c.game)){revision++;broadcast();}};
 function blocked(position,actor){const h=config.playerHalfHeight,r=config.playerRadius;for(const c of clients.values())if(c!==actor&&c.hasState&&now()-c.lastStateAt<2000){if(Math.abs(position[2]-c.position[2])<2*h&&Math.hypot(position[0]-c.position[0],position[1]-c.position[1])<2*r)return true;}return false;}
 return {
  welcome(c){if(config.enabled&&c.portalProtocol===1)sendSnapshot(c);},disconnect:remove,reset:remove,
  handle(c,m){
   if(!config.enabled||c.portalProtocol!==1){deny(c,'Portal adapter is not enabled');return;}
   if(m.session!==c.session){deny(c,'Stale portal connection');return;}
   if(m.type==='portal_world_ready'){
    if(!config.destinationWorlds?.portalGames?.includes(c.game)||!Array.isArray(m.worlds)||m.worlds.length>32||new Set(m.worlds).size!==m.worlds.length||m.worlds.some(id=>!destinations.some(d=>d.id===id))){deny(c,'Invalid loaded destination list');return;}
    c.worldsReady=[...m.worlds];revision++;broadcast();log('portal_worlds_ready',{game:c.game,worlds:c.worldsReady});return;
   }
   if(m.type==='portal_sync'){sendSnapshot(c);return;}
   if(m.type==='portal_ack'){const p=pending.get(c.game);if(p&&p.event===m.event&&p.session===c.session){pending.delete(c.game);history.delete(c.game);if(m.applied===true)arrivals.set(c.game,p);log('portal_applied',{game:c.game,event:m.event,applied:m.applied===true});}return;}
   if(!c.hasState||now()-c.lastStateAt>500){deny(c,'No fresh live player state');return;}
   if(!config.weaponClasses?.[c.game]?.includes(c.weapon)){deny(c,'Equip the Portal Gun first');return;}
   if(m.type==='portal_clear'){remove(c);return;}
   if(m.type!=='portal_fire'||![0,1].includes(m.endpoint)){deny(c,'Invalid endpoint');return;}
   const destination=m.destination==null?null:m.destination===hub?hubDestination:destinations.find(d=>d.id===m.destination);
   if(m.destination!=null&&(!destination||(destination.id!==hub&&!c.worldsReady?.includes(destination.id)))){deny(c,'Destination geometry is not loaded in this game');return;}
   if(now()-(c.portalFired??-Infinity)<config.fireIntervalMs){deny(c,'Portal Gun cooling down');return;}c.portalFired=now();
   if(!c.rotation){deny(c,'No authoritative aim');return;}
   const vector=a=>Array.isArray(a)&&a.length===3&&a.every(Number.isFinite);
   if(!vector(m.hit)||!vector(m.eye)||Math.hypot(...sub(m.eye,add(c.position,[0,0,64])))>40){deny(c,'Missing native placement trace');return;}
   const delta=sub(m.hit,m.eye),length=Math.hypot(...delta);if(length<1||length>config.maxRange+8){deny(c,'Invalid native shot range');return;}
   const hit=destinations.some(w=>w.id===c.world)?worldPlacement(c,m.eye,scale(delta,1/length),config):wallHit(m.eye,scale(delta,1/length),config);if(hit.error){deny(c,hit.error);return;}
   if(Math.hypot(...sub(hit.hit,m.hit))>8){deny(c,'Shot blocked or native trace disagrees');return;}
   for(const pair of pairs.values())for(const endpoint of pair.endpoints.filter(Boolean))if(!(pair.owner===c.game&&endpoint.endpoint===m.endpoint)&&endpoint.surface===hit.surface){const d=sub(hit.position,endpoint.position);if(Math.abs(dot(d,hit.right))<config.aperture.halfWidth*2+2&&Math.abs(dot(d,hit.up))<config.aperture.halfHeight*2+2){deny(c,'Portal overlaps an existing aperture');return;}}
   let pair=pairs.get(c.game);if(!pair||pair.session!==c.session||pair.destination!==(destination?.id))pair={id:'pair:'+c.game,owner:c.game,session:c.session,endpoints:[null,null]};
   if(destination){
    const yaw=destination.yaw*Math.PI/180;
    pair.destination=destination.id;
    pair.endpoints[1-m.endpoint]={...frame(add(destination.spawn,[0,0,config.aperture.halfHeight]),[Math.cos(yaw),Math.sin(yaw),0]),id:`portal:${c.game}:${1-m.endpoint}`,endpoint:1-m.endpoint,surface:'destination:'+destination.id,world:destination.id,aperture:config.aperture};
   }
   const {distance,hit:rawHit,...basis}=hit;pair.endpoints[m.endpoint]={...basis,id:`portal:${c.game}:${m.endpoint}`,endpoint:m.endpoint,aperture:config.aperture};pair.revision=++revision;pair.linked=pair.endpoints.every(Boolean);pairs.set(c.game,pair);broadcast();log('portal_placed',{owner:c.game,endpoint:m.endpoint,revision,portal:pair.endpoints[m.endpoint]});
  },
  sample(c,state){
   if(!config.enabled||c.portalProtocol!==1||c.portalTraverse!==true)return;
   const hull={...config,...config.playerHulls?.[c.game]};
   // Acknowledgements and movement use separate queues. Old entry-side samples
   // can arrive after a successful teleport. Do not rotate/teleport again until
   // the authoritative player is observed on the destination side, clear of it.
   const arrival=arrivals.get(c.game);
   if(arrival){
    const center=add(state.position,[0,0,hull.playerHalfHeight]);
    const destinationDistance=Math.hypot(...sub(state.position,arrival.position));
    const sourceDistance=Math.hypot(...sub(state.position,arrival.source));
    const clear=dot(sub(center,arrival.exit.position),arrival.exit.normal)>extent(arrival.exit.normal,hull)+(config.contactMargin??2);
    if(!clear||destinationDistance>=sourceDistance||destinationDistance>64+Math.hypot(...arrival.velocity)*0.25){
     // A lost destination sample or a subsequent respawn must not disable
     // portals for the rest of this game session. After the bounded recovery
     // window, seed from the current position without traversing this sample.
     if(now()<=arrival.deadline+config.ackTimeoutMs)return;
     arrivals.delete(c.game);history.set(c.game,{center,time:now(),velocity:[...state.velocity]});
     log('portal_arrival_recovered',{game:c.game,event:arrival.event,position:state.position});return;
    }
    arrivals.delete(c.game);history.set(c.game,{center,time:now(),velocity:[...state.velocity]});return;
   }
   const center=add(state.position,[0,0,hull.playerHalfHeight]),previous=history.get(c.game);history.set(c.game,{center,time:now(),velocity:[...state.velocity]});
   const wait=pending.get(c.game);if(wait){if(now()>wait.deadline){pending.delete(c.game);history.delete(c.game);deny(c,'Traversal acknowledgement timed out');}return;}
   if(!previous||now()-previous.time>250)return;
   for(const pair of pairs.values())if(pair.linked&&supports(c,pair))for(let i=0;i<2;i++){
    const entry=pair.endpoints[i],exit=pair.endpoints[1-i],point=crossing(previous.center,center,entry,hull);if(!point)continue;
    const relative=sub(point,entry.position);const position=add(add(exit.position,add(scale(exit.right,-dot(relative,entry.right)),scale(exit.up,dot(relative,entry.up)))),scale(exit.normal,extent(exit.normal,hull)+config.exitMargin));position[2]-=hull.playerHalfHeight;
    if(blocked(position,c)){deny(c,'Exit blocked by a player');return;}
    // Dynamic-object clearance is not yet implemented: activation is gated by
    // configuration until all authoritative adapters can validate the exit hull.
    const incoming=entryVelocity(state,previous,center,entry,hull,now()-previous.time);
    const event=randomUUID(),packet={type:'portal_traverse',event,session:c.session,pair:pair.id,revision:pair.revision,expiresAt:now()+config.ackTimeoutMs,position,rotation:transformLook(state.rotation,entry,exit),velocity:transformVector(incoming,entry,exit)};
    pending.set(c.game,{event,session:c.session,deadline:now()+config.ackTimeoutMs,position,source:[...state.position],exit,velocity:packet.velocity});emit(c,packet);history.delete(c.game);log('portal_crossing',{game:c.game,event,pair:pair.id,incomingVelocity:incoming,outgoingVelocity:packet.velocity,momentumRecovered:incoming.some((v,i)=>Math.abs(v-state.velocity[i])>0.01)});return;
   }
  },inspect:()=>snapshot()
 };
}


