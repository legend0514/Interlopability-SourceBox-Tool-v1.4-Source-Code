import {test} from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import {once} from 'node:events';
import {createCore} from '../core.mjs';
test('oversized frames and malformed JSON disconnect without killing service',async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');
 try{
  for(const payload of ['x'.repeat(5000),'null\n','{broken}\n']){
   const s=net.connect(c.server.address().port,'127.0.0.1');s.on('error',()=>{});await once(s,'connect');
   const closed=new Promise(resolve=>s.once('close',resolve));s.write(payload);await closed;assert(c.server.listening);
  }
 }finally{await c.close();}
});
