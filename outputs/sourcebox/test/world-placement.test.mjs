import test from 'node:test';
import assert from 'node:assert/strict';
import {createWorldPortalPlacement,traceTriangles} from '../world-portal-placement.mjs';
import {frame,dot,transformVector} from '../portal-math.mjs';
const vertices=[[0,-100,-100],[0,100,-100],[0,100,100],[0,-100,-100],[0,100,100],[0,-100,100]];
test('world portal requires loaded geometry and rejects aperture over an edge',()=>{
 const place=createWorldPortalPlacement([{id:'test'}],()=>({collision:[{vertices}]}));
 const client={world:'test',worldsReady:['test']},cfg={maxRange:1000,aperture:{halfWidth:32,halfHeight:64}};
 assert.equal(place(client,[100,0,0],[-1,0,0],cfg).world,'test');
 assert.match(place(client,[100,90,0],[-1,0,0],cfg).error,/does not fit/);
 assert.match(place({...client,worldsReady:[]},[100,0,0],[-1,0,0],cfg).error,/not loaded/);
});
test('nearest world surface wins, and sloped frames preserve momentum magnitude',()=>{
 const tris=[vertices.slice(0,3),vertices.slice(3)];
 assert.equal(traceTriangles([100,0,0],[-1,0,0],tris,99),null);
 assert.equal(traceTriangles([100,0,0],[-1,0,0],tris,101).distance,100);
 const a=frame([0,0,0],[1,0,0]),b=frame([0,0,0],[1,1,1]);
 assert.ok(Math.abs(dot(b.normal,b.up))<1e-12);
 const v=[100,30,-20];assert.ok(Math.abs(Math.hypot(...transformVector(v,a,b))-Math.hypot(...v))<1e-9);
});
