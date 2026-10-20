import {test} from 'node:test';
import assert from 'node:assert/strict';
import {acquireBridgeLease} from '../bridge-lease.mjs';
import {randomUUID} from 'node:crypto';
test('lease proves the live bridge directory without needing game data or a coordinator',async()=>{
 const server=await acquireBridgeLease(0,{directory:'C:/Games/GMod/garrysmod/data/sourcebox',startedAt:123});
 try{
  const nonce=randomUUID(),url=`http://127.0.0.1:${server.address().port}/sourcebox-bridge`;
  const response=await fetch(url+'?nonce='+nonce),body=await response.json();
  assert.equal(body.nonce,nonce);assert.equal(body.pid,process.pid);assert.equal(body.directory,'C:/Games/GMod/garrysmod/data/sourcebox');
  assert.equal(body.component,'sourcebox-gmod-bridge');assert.equal((await fetch(url)).status,400);
 }finally{server.closeAllConnections();await new Promise(r=>server.close(r));}
});
test('a second bridge cannot acquire the same lease; exit releases it',async()=>{
 const first=await acquireBridgeLease(0);const port=first.address().port;
 try{await assert.rejects(acquireBridgeLease(port),{code:'EADDRINUSE'});}finally{await new Promise(r=>first.close(r));}
 const replacement=await acquireBridgeLease(port);await new Promise(r=>replacement.close(r));
});
