import {test} from 'node:test';import assert from 'node:assert/strict';import net from 'node:net';import {once} from 'node:events';import {createCore} from '../core.mjs';
test('CS2 probe accepts its own native movement and rejects foreign entity claims',{timeout:5000},async()=>{
 const logs=[],c=createCore({log:l=>logs.push(JSON.parse(l))});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const socket=net.connect(c.server.address().port,'127.0.0.1');await once(socket,'connect');
 try{let response=once(socket,'data');socket.write(JSON.stringify({type:'hello',protocol:1,game:'cs2',pid:1})+'\n');assert.equal(JSON.parse((await response)[0]).type,'welcome');
 const state={type:'state',seq:1,timestamp:1,entity:'player:cs2',position:[1,2,3],rotation:[0,0,0],velocity:[4,5,0],grounded:true};response=once(socket,'data');socket.write(JSON.stringify(state)+'\n');await response;assert.deepEqual(logs.find(m=>m.type==='state').position,[1,2,3]);
 const closed=once(socket,'close');socket.write(JSON.stringify({...state,seq:2,timestamp:2,entity:'player:hl2'})+'\n');await closed;assert(logs.some(m=>m.type==='rejected'));
 }finally{socket.destroy();await c.close();}
});
