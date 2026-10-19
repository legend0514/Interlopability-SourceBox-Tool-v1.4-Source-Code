import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
test('third game receives both players without replacing legacy proxies',async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write(JSON.stringify({type:'hello',protocol:1,game,pid:123})+'\n');await p;return s;}
 try{
  const h=await join('hl2'),t=await join('tf2'),g=await join('gmod');assert.equal(c.clients.size,3);
  for(const [source,game,extra] of [[h,'hl2',{}],[t,'tf2',{classId:3,team:2}]]){
   const p=once(g,'data');source.write(JSON.stringify({type:'state',entity:'player:'+game,seq:1,timestamp:100,position:[1,2,3],rotation:[0,0,0],velocity:[0,0,0],grounded:true,...extra})+'\n');
   const m=JSON.parse((await p)[0]);assert.equal(m.game,game);assert.equal(m.type,'remote');
  }
  await new Promise(r=>setTimeout(r,20));
  const backH=once(h,'data'),backT=once(t,'data');const ack=once(g,'data');g.write(JSON.stringify({type:'state',entity:'player:gmod',seq:1,timestamp:100,position:[9,8,7],rotation:[0,0,0],velocity:[0,0,0],grounded:true})+'\n');await ack;
  assert.deepEqual(c.clients.get('gmod').position,[9,8,7]);
  for(const p of [backH,backT]){const messages=(await p)[0].toString().trim().split('\n').map(JSON.parse);assert(messages.some(m=>m.type==='remote'&&m.game==='gmod'&&m.state.entity==='player:gmod'));}
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
