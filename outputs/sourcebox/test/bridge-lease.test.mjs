import {test} from 'node:test';
import assert from 'node:assert/strict';
import {acquireBridgeLease} from '../bridge-lease.mjs';
test('a second bridge cannot acquire the same lease; exit releases it',async()=>{
 const first=await acquireBridgeLease(0);const port=first.address().port;
 try{await assert.rejects(acquireBridgeLease(port),{code:'EADDRINUSE'});}finally{await new Promise(r=>first.close(r));}
 const replacement=await acquireBridgeLease(port);await new Promise(r=>replacement.close(r));
});
