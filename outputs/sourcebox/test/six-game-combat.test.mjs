import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
test('all 30 directed game pairs route fixed damage only to the selected live target',{timeout:10000},async()=>{
 const core=createCore({log:()=>{}}),clients={};core.server.listen(0,'127.0.0.1');await once(core.server,'listening');
 async function wait(f){for(let i=0;i<200;i++){if(f())return;await new Promise(r=>setTimeout(r,5));}throw Error('message timeout');}
 try{
  for(const game of ['hl2','tf2','gmod','hl1mp','cs2','portal2']){
   const socket=net.connect(core.server.address().port,'127.0.0.1');let buffer='',seq=0;const messages=[];
   socket.on('data',b=>{buffer+=b;let i;while((i=buffer.indexOf('\n'))>=0){messages.push(JSON.parse(buffer.slice(0,i)));buffer=buffer.slice(i+1);}});
   const send=m=>socket.write(JSON.stringify({...m,seq:++seq,timestamp:seq})+'\n');clients[game]={socket,messages,send};await once(socket,'connect');send({type:'hello',protocol:1,game,pid:1});await wait(()=>messages.some(m=>m.type==='welcome'));
   send({type:'state',entity:'player:'+game,position:[0,0,8],rotation:[0,0,0],velocity:[0,0,0],grounded:true,...(game==='tf2'?{classId:6,team:2}:{}),...(game==='hl1mp'?{model:'models/player/mp/gordon/gordon.mdl'}:{})});await wait(()=>messages.some(m=>m.type==='ack'));
  }
  for(const [source,c] of Object.entries(clients))for(const [target,t] of Object.entries(clients))if(target!==source){
   const before=Object.fromEntries(Object.entries(clients).map(([id,p])=>[id,p.messages.filter(m=>m.type==='player_damage').length]));
   c.send({type:'player_hit',target,amount:10000});await wait(()=>t.messages.filter(m=>m.type==='player_damage').length===before[target]+1);
   const hit=t.messages.filter(m=>m.type==='player_damage').at(-1);assert.equal(hit.source,source);assert.equal(hit.amount,10);
   for(const [id,p] of Object.entries(clients))assert.equal(p.messages.filter(m=>m.type==='player_damage').length,before[id]+(id===target?1:0));
  }
 }finally{for(const c of Object.values(clients))c.socket.destroy();await core.close();}
});

