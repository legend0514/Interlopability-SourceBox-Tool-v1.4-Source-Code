import {test} from 'node:test';
import assert from 'node:assert/strict';
import {sheets,validate} from '../preflight.mjs';
test('missing sheets and duplicated entity IDs block the build',()=>{
 for(const key of ['games','messages','entities','coordinates','runtime','tests']){const s=structuredClone(sheets);delete s[key];assert(validate(s).length>0,key);}
 const s=structuredClone(sheets);s.entities[1]=s.entities[0];assert(validate(s).includes('entity definitions'));
});
