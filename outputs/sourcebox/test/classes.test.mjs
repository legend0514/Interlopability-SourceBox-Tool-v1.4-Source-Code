import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
test('TF2 class changes reach HL2; invalid class is rejected',async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write(JSON.stringify({type:'hello',protocol:1,game,pid:12})+'\n');await p;return s;}
 try{
  const hl=await join('hl2'),tf=await join('tf2');
  for(let classId=1;classId<=9;classId++){
   const state={type:'state',entity:'player:tf2',seq:classId,timestamp:classId,position:[1,2,3],rotation:[20,30,0],velocity:[0,0,0],grounded:true,classId,team:classId%2?2:3,weapon:'tf_weapon_shotgun_primary'};
   const received=once(hl,'data'),ack=once(tf,'data');tf.write(JSON.stringify(state)+'\n');
   const msg=JSON.parse((await received)[0]);assert.equal(msg.game,'tf2');assert.equal(msg.state.classId,classId);assert.equal(msg.state.team,state.team);assert.equal(msg.state.weapon,state.weapon);assert.equal(JSON.parse((await ack)[0]).type,'ack');
  }
  const closed=once(tf,'close');tf.write(JSON.stringify({type:'state',entity:'player:tf2',seq:10,timestamp:10,position:[0,0,0],rotation:[0,0,0],velocity:[0,0,0],grounded:true,classId:99,team:2})+'\n');await closed;
  assert.equal(c.clients.has('tf2'),false);
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
