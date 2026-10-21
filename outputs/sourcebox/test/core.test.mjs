import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
import {validate,sheets} from '../preflight.mjs';
test('preflight rejects unsupported entities and invalid mapping',()=>{
 assert.deepEqual(validate(sheets),[]);const s=structuredClone(sheets);s.entities.push({id:'cube'});s.coordinates.hl2.scale=null;assert.equal(validate(s).length,2);
});
test('TCP two identities, split framing, stale rejection and reconnect',async()=>{
 const events=[],c=createCore({log:x=>events.push(JSON.parse(x))});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');
 const sockets=[];
 const connect=async(game)=>{const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write('{"type":"hel');s.write(`lo","protocol":1,"game":"${game}","pid":123}\n`);const welcome=JSON.parse((await p)[0]);assert.equal(welcome.type,'welcome');assert.equal(welcome.cubeEnabled,false);return s;};
 try{
  const a=await connect('hl2'),b=await connect('tf2');assert.equal(c.clients.size,2);
  const ack=once(a,'data');a.write('{"type":"heartbeat","seq":1,"timestamp":10}\n');assert.equal(JSON.parse((await ack)[0]).seq,1);
  const closed=once(a,'close');a.write('{"type":"heartbeat","seq":1,"timestamp":10}\n');await closed;
  await connect('hl2');assert.equal(c.clients.size,2);assert(events.some(e=>e.reason==='invalid or stale heartbeat'));
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
