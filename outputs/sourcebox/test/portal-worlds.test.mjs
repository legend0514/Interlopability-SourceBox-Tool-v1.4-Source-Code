import test from 'node:test';
import assert from 'node:assert/strict';
import {sheets} from '../preflight.mjs';
import {createPortals} from '../portals.mjs';
test('all four native adapters can select every imported world and the shared-room return',()=>{
 const config=structuredClone(sheets.portals);config.destinationWorlds=structuredClone(sheets.destinationWorlds);
 const games=['hl2','tf2','hl1mp','portal2'];
 for(const game of games)for(const destination of [...config.destinationWorlds.worlds.map(w=>w.id),'sourcebox_room_001']){
  const c={game,session:game,portalProtocol:1,hasState:true,lastStateAt:1000,position:[0,0,0],rotation:[0,0,0],world:'sourcebox_room_001',weapon:config.weaponClasses[game][0]};
  const sent=[];const p=createPortals({config,clients:new Map([[game,c]]),now:()=>1000,emit:(_,m)=>sent.push(m),log:()=>{}});
  p.handle(c,{type:'portal_world_ready',session:game,worlds:config.destinationWorlds.worlds.map(w=>w.id)});
  p.handle(c,{type:'portal_fire',session:game,endpoint:0,eye:[0,0,64],hit:[512,0,64],destination});
  assert.equal(p.inspect().pairs[0]?.destination,destination,game+' -> '+destination+' '+JSON.stringify(sent.filter(m=>m.type==='portal_notice')));
  assert.equal(p.inspect().pairs[0]?.linked,true);
 }
});
test('destination portals require loaded geometry, hide from unsupported peers and preserve return travel',()=>{
 const config=structuredClone(sheets.portals);config.destinationWorlds={enabled:true,portalGames:['cs2'],worlds:[{id:'test_world',spawn:[1000,0,3000],yaw:180}]};
 const sent=[];const cs={game:'cs2',session:'cs',portalProtocol:1,portalTraverse:true,hasState:true,lastStateAt:1000,position:[0,0,0],rotation:[0,0,0],weapon:'weapon_sourcebox_portalgun'};
 const hl={...cs,game:'hl2',session:'hl'};const clients=new Map([['cs2',cs],['hl2',hl]]);
 let now=1000;const portal=createPortals({config,clients,now:()=>now,emit:(c,m)=>sent.push({game:c.game,...m}),log:()=>{}});
 const fire={type:'portal_fire',session:'cs',endpoint:0,eye:[0,0,64],hit:[512,0,64],destination:'test_world'};
 portal.handle(cs,fire);assert.equal(portal.inspect().pairs.length,0);
 portal.handle(hl,{type:'portal_world_ready',session:'hl',worlds:['test_world']});assert.equal(hl.worldsReady,undefined);
 portal.handle(cs,{type:'portal_world_ready',session:'cs',worlds:['test_world']});portal.handle(cs,fire);
 const pair=portal.inspect().pairs[0];assert.equal(pair.linked,true);assert.equal(pair.destination,'test_world');
 assert.deepEqual(pair.endpoints[1].position,[1000,0,3000+config.aperture.halfHeight]);
 const latest=sent.filter(m=>m.game==='hl2'&&m.type==='portal_snapshot').at(-1);assert.deepEqual(latest.pairs,[]);
 const sample=(client,x,z=0)=>portal.sample(client,{position:[x,0,z],rotation:[0,0,0],velocity:[220,0,0]});
 sample(hl,450);sample(hl,498);assert.equal(sent.filter(m=>m.game==='hl2'&&m.type==='portal_traverse').length,0);
 sample(cs,450);sample(cs,498);const traversal=sent.find(m=>m.game==='cs2'&&m.type==='portal_traverse');assert.ok(traversal);assert.ok(traversal.position[2]>=3000);
 portal.handle(cs,{type:'portal_ack',session:'cs',event:traversal.event,applied:true});sample(cs,traversal.position[0],traversal.position[2]);
 sample(cs,960,3000);sample(cs,990,3000);assert.equal(sent.filter(m=>m.game==='cs2'&&m.type==='portal_traverse').length,2);
 now+=500;cs.lastStateAt=now;portal.handle(cs,{...fire,destination:'missing'});assert.equal(portal.inspect().pairs[0].destination,'test_world');
});
