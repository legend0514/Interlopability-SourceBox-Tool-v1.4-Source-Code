import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
test('HL2 pose reaches TF2, preserves fields, rejects forged ownership',async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write(JSON.stringify({type:'hello',protocol:1,game,pid:123})+'\n');await p;return s;}
 try{
  const a=await join('hl2'),b=await join('tf2');
  const state={type:'state',entity:'player:hl2',seq:1,timestamp:100,position:[1,2,3],rotation:[0,359,0],velocity:[4,5,6],grounded:true};
  const received=once(b,'data'),ack=once(a,'data');a.write(JSON.stringify(state)+'\n');
  const m=JSON.parse((await received)[0]);assert.equal(m.type,'remote');assert.deepEqual(m.state.position,[1,2,3]);assert.equal(m.state.grounded,true);assert.equal(JSON.parse((await ack)[0]).type,'ack');
  const closed=once(b,'close');b.write(JSON.stringify(state)+'\n');await closed;assert.equal(c.clients.size,1);
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
