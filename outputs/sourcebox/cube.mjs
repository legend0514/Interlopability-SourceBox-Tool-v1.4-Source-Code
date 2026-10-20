// Engine-independent, fail-closed ownership handoff for the prototype cube.
import {randomUUID} from 'node:crypto';
export function createCube({definition,clients,emit,log,now=Date.now}){
 if(definition.enabled===false)return {welcome(){},disconnect(){},tick(){},handle(c,m){return m.type?.startsWith('cube_')===true;},inspect(){return {enabled:false};}};
 let owner=definition.owner,epoch=1,pending=null,lastTransfer=-Infinity,lastTelemetry=0;
 let state={alive:true,position:definition.position,rotation:[0,0,0],velocity:[0,0,0],angularVelocity:[0,0,0],solid:true,timestamp:0};
 const vector=v=>Array.isArray(v)&&v.length===3&&v.every(n=>Number.isFinite(n)&&Math.abs(n)<1000000);
 const valid=s=>s&&typeof s.alive==='boolean'&&typeof s.solid==='boolean'&&Number.isFinite(s.timestamp)&&s.timestamp>=0&&['position','rotation','velocity','angularVelocity'].every(k=>vector(s[k]));
 const packet=()=>({type:'cube_owner',id:definition.id,owner,epoch,state});
 const broadcast=m=>{for(const c of clients.values())if(c.cubeProtocol===1)emit(c,m);};
 const deny=(c,reason)=>{emit(c,{type:'cube_denied',reason});log('cube_denied',{game:c.game,reason});};
 function cancel(reason){if(!pending)return;pending=null;epoch++;broadcast(packet());log('cube_transfer_cancelled',{owner,epoch,reason});}
 return {
  welcome(c){if(c.cubeProtocol===1)emit(c,packet());},
  disconnect(c){if(pending&&(pending.from===c.game||pending.to===c.game))cancel('participant disconnected');},
  tick(){if(pending&&now()>pending.deadline)cancel('freeze acknowledgement timeout');},
  handle(c,m){
   if(c.cubeProtocol!==1)return false;
   if(m.type==='cube_ready'){if(c.game===owner&&m.epoch===epoch)log('cube_native_ready',{game:c.game,epoch,created:m.created===true});return true;}
   if(m.type==='cube_take'){
    if(c.game===owner){deny(c,'already owner');return true;}
    if(pending||now()-lastTransfer<definition.cooldownMs){deny(c,'handoff busy or cooling down');return true;}
    if(!clients.get(owner)||clients.get(owner).cubeProtocol!==1){deny(c,'owner offline');return true;}
    if(!state.alive||!c.hasState||!vector(c.position)||Math.hypot(...c.position.map((v,i)=>v-state.position[i]))>definition.claimDistance){deny(c,'stand near the live cube');return true;}
    pending={id:randomUUID(),from:owner,to:c.game,deadline:now()+definition.freezeTimeoutMs};
    emit(clients.get(owner),{type:'cube_freeze',transfer:pending.id,epoch});log('cube_freeze_requested',{...pending,epoch});return true;
   }
   if(m.type==='cube_frozen'){
    if(!pending||c.game!==owner||m.transfer!==pending.id||m.epoch!==epoch)return true;
    if(!valid(m.state)){deny(c,'invalid frozen snapshot');return true;}
    state={...m.state,velocity:[0,0,0],angularVelocity:[0,0,0]};owner=pending.to;epoch++;pending=null;lastTransfer=now();broadcast(packet());log('cube_transferred',{owner,epoch,state});return true;
   }
   if(m.type==='cube_state'){
    if(c.game!==owner||m.epoch!==epoch||pending)return true; // Late old-owner traffic cannot regain authority.
    if(!valid(m.state)||m.state.timestamp<state.timestamp){deny(c,'invalid cube snapshot');return true;}
    state=m.state;if(now()-lastTelemetry>=1000){log('cube_sample',{owner,epoch,state});lastTelemetry=now();}for(const peer of clients.values())if(peer!==c&&peer.cubeProtocol===1)emit(peer,{type:'cube_state',owner,epoch,state});return true;
   }
   return false;
  },
  inspect(){return {owner,epoch,pending,state};}
 };
}
