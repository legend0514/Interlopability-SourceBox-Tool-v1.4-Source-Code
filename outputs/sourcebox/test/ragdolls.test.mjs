import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
import {preflight} from '../preflight.mjs';
for(const enabled of [true,false])test(`death bodies enforce identity and freshness; enabled=${enabled}`,async()=>{
 const config=preflight();config.combat.ragdolls.enabled=enabled;
 const events=[],core=createCore({config,log:x=>events.push(JSON.parse(x))});core.server.listen(0,'127.0.0.1');await once(core.server,'listening');
 const sockets=[];
 async function join(game){const socket=net.connect(core.server.address().port,'127.0.0.1');sockets.push(socket);await once(socket,'connect');let seq=0,buffer='';const messages=[];socket.on('data',chunk=>{buffer+=chunk;let i;while((i=buffer.indexOf('\n'))>=0){messages.push(JSON.parse(buffer.slice(0,i)));buffer=buffer.slice(i+1);}});
  async function send(m){socket.write(JSON.stringify(m)+'\n');for(let i=0;i<100;i++){if(messages.some(x=>m.type==='hello'?x.type==='welcome':x.type==='ack'&&x.seq===m.seq))return;await new Promise(r=>setTimeout(r,5));}throw Error('No ack');}
  await send({type:'hello',protocol:1,game,pid:123});return{messages,send:m=>send({...m,seq:++seq,timestamp:seq})};}
 try{const a=await join('hl2'),b=await join('tf2');const pose={type:'state',position:[1,2,3],rotation:[0,90,0],velocity:[40,0,0],grounded:true,health:10};
  await a.send({...pose,entity:'player:hl2'});await b.send({...pose,entity:'player:tf2',classId:6,team:2});
  await a.send({type:'player_death'});assert.equal(events.filter(x=>x.type==='player_death').length,0,'unsolicited death ignored');
  await b.send({type:'player_hit',target:'hl2'});await a.send({type:'player_death',game:'tf2',position:[999,999,999]});
  await a.send({...pose,entity:'player:hl2',position:[-128,0,8],health:100});await a.send({type:'player_death'});
  if(!enabled){await b.send({type:'heartbeat'});assert.equal(events.filter(x=>x.type==='player_death').length,0);assert(!a.messages.some(x=>x.type==='player_ragdoll'));assert(!b.messages.some(x=>x.type==='player_ragdoll'));return;}
  assert.equal(events.filter(x=>x.type==='player_death').length,1);
  const body=a.messages.find(x=>x.type==='player_ragdoll');assert.equal(body.game,'hl2');assert.deepEqual(body.state.position,[1,2,3]);assert.equal(body.maxBodies,6);assert.equal(body.lifetimeMs,12000);
  await b.send({type:'heartbeat'});assert(b.messages.some(x=>x.type==='player_ragdoll'&&x.event===body.event));
  core.clients.get('hl2').lastDeathAt=0;core.clients.get('hl2').lastStateAt=Date.now()-3000;
  await a.send({type:'player_death'});assert.equal(events.filter(x=>x.type==='player_death').length,1);
 }finally{for(const s of sockets)s.destroy();await core.close();}
});
