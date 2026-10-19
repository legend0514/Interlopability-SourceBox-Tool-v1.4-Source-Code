import {test} from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';
import {frame,transformVector,transformPoint,wallHit,crossing,entryVelocity} from '../portal-math.mjs';
const config=JSON.parse(fs.readFileSync(new URL('../sheets/portals.json',import.meta.url)));
test('portal transforms preserve velocity magnitude and invert for every wall pair',()=>{for(const n of [[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]])for(const m of [[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]]){const a=frame([512,0,64],n),b=frame([0,-512,100],m),v=[200,-90,31],p=[500,5,67];assert.ok(Math.abs(Math.hypot(...transformVector(v,a,b))-Math.hypot(...v))<1e-8);transformPoint(transformPoint(p,a,b),b,a).forEach((x,i)=>assert.ok(Math.abs(x-p[i])<1e-8));}});
test('placement fits wall edges and supports floor and ceiling',()=>{assert.equal(wallHit([0,0,64],[1,0,0],config).surface,'wall:0:1');assert.equal(wallHit([0,500,64],[1,0,0],config).position[1],480);assert.equal(wallHit([0,0,64],[0,0,-1],config).surface,'wall:2:0');assert.equal(wallHit([0,0,64],[0,0,1],config).surface,'wall:2:1');});
test('swept player hull detects fast approaches before solid wall contact, not side entries',()=>{const p=frame([512,0,64],[-1,0,0]);assert.ok(crossing([450,0,64],[498,0,64],p,config));assert.equal(crossing([450,40,64],[498,40,64],p,config),null);assert.equal(crossing([498,0,64],[450,0,64],p,config),null);});
test('wall shots just above floor retain standing-player clearance',()=>{
 for(const eyeZ of [64,65,72,82]){const p=wallHit([0,0,eyeZ],[1,0,0],config);assert.equal(p.position[2],64);assert.ok(crossing([450,0,41.03125],[498,0,41.03125],p,config));}
 assert.equal(wallHit([0,0,100],[1,0,0],config).position[2],100);
});
test('floor and ceiling entry fires before the native collision skin stops movement',()=>{
 assert.ok(crossing([0,0,50],[0,0,41.03125],frame([0,0,0],[0,0,1]),config));
 assert.ok(crossing([0,0,205],[0,0,214.96875],frame([0,0,256],[0,0,-1]),config));
 assert.equal(crossing([0,0,41.03125],[0,0,50],frame([0,0,0],[0,0,1]),config),null);
});
test('contact band permits alignment after wall contact and retry after obstruction clears',()=>{
 const p=frame([512,0,64],[-1,0,0]);
 assert.equal(crossing([450,40,64],[488,40,64],p,config),null);
 assert.ok(crossing([488,40,64],[488,0,64],p,config));
 assert.ok(crossing([488,0,64],[488,0,64],p,config));
 assert.equal(crossing([488,0,64],[480,0,64],p,config),null);
 assert.equal(crossing([520,0,64],[521,0,64],p,config),null);
});


test('floor impact recovers fresh falling momentum and rotates it into an exit launch',()=>{
 const floor=frame([0,0,0],[0,0,1]),wall=frame([512,0,100],[-1,0,0]);
 const cfg={...config,playerHalfHeight:36,playerRadius:16};
 const previous={center:[0,0,65],velocity:[40,0,-600]};
 const state={velocity:[40,0,0]};
 const v=entryVelocity(state,previous,[0,0,36.03125],floor,cfg,50);
 assert.deepEqual(v,[40,0,-600]);
 const out=transformVector(v,floor,wall);assert.equal(out[0],-600);
 assert.ok(Math.abs(Math.hypot(...out)-Math.hypot(...previous.velocity))<1e-8);
 assert.deepEqual(entryVelocity(state,previous,[0,0,36.03125],floor,cfg,200),state.velocity);
 assert.deepEqual(entryVelocity(state,{...previous,center:[0,0,36.03125]},[0,0,36.03125],floor,cfg,50),state.velocity);
});
test('momentum recovery preserves native in-flight speed and cannot combine components into a boost',()=>{
 const floor=frame([0,0,0],[0,0,1]),cfg={...config,playerHalfHeight:36,playerRadius:16},previous={center:[0,0,65],velocity:[0,0,-600]};
 assert.deepEqual(entryVelocity({velocity:[0,0,-640]},previous,[0,0,37],floor,cfg,50),[0,0,-640]);
 const out=entryVelocity({velocity:[500,0,0]},previous,[0,0,36.03125],floor,cfg,50);
 assert.ok(Math.hypot(...out)<=600+1e-8);
});
test('high-speed swept crossing survives a skipped plane but rejects back-side starts',()=>{
 const wall=frame([512,0,64],[-1,0,0]);
 assert.ok(crossing([450,0,64],[550,0,64],wall,config));
 assert.equal(crossing([520,0,64],[550,0,64],wall,config),null);
 assert.equal(crossing([450,100,64],[550,100,64],wall,config),null);
 const floor=frame([0,0,0],[0,0,1]);
 assert.ok(crossing([0,0,100],[0,0,-30],floor,config));
 assert.equal(crossing([0,0,-10],[0,0,-30],floor,config),null);
});
