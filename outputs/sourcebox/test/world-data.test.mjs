import test from 'node:test';
import assert from 'node:assert/strict';
import {validateWorldData} from '../world-preflight.mjs';
const definition={id:'example',offset:[0,0,3500],spawn:[0,0,3501]};
const vertices=[[0,0,3500],[128,0,3500],[0,128,3500]];
const sample=()=>({...definition,bounds:[[0,0,3500],[128,128,3500]],chunks:[{material:'test/floor',vertices:vertices.map(p=>[...p,0,0,1,0,0])}],collision:[{vertices:structuredClone(vertices)}]});
test('world preflight rejects corrupt triangles, wrong transforms and runaway collision bodies',()=>{
 assert.deepEqual(validateWorldData(sample(),definition),[]);
 for(const damage of [d=>d.collision[0].vertices.pop(),d=>d.chunks[0].vertices[0][0]=NaN,d=>d.offset=[0,0,0],d=>d.collision=Array.from({length:33},()=>d.collision[0])]) {
  const d=sample();damage(d);assert.ok(validateWorldData(d,definition).length>0);
 }
});
test('world preflight rejects inverted regions and spawn points outside the imported map',()=>{
 const reversed=sample();reversed.bounds.reverse();assert.ok(validateWorldData(reversed,definition).includes('Reversed world bounds'));
 const escaped=sample();escaped.spawn=[9000,9000,9000];assert.ok(validateWorldData(escaped,{...definition,spawn:escaped.spawn}).includes('Spawn outside world geometry bounds'));
});
