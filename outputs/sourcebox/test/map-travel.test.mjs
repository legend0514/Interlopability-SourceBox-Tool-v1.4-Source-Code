import test from 'node:test';
import assert from 'node:assert/strict';
import {mapTravelPaused} from '../map-travel.mjs';
test('map excursions disconnect shared-room replication until the hub is loaded',()=>{
 assert.equal(mapTravelPaused(undefined),false);
 assert.equal(mapTravelPaused({map:'sourcebox_room_001',transitioning:false}),false);
 for(const world of [null,{}, {map:'gm_construct',transitioning:false},{map:'sourcebox_room_001',transitioning:true}])assert.equal(mapTravelPaused(world),true);
});
test('loaded same-host destination worlds retain replication; changelevel and unknown maps do not',()=>{
 const loaded=['gm_construct','gm_flatgrass'];
 assert.equal(mapTravelPaused({map:'gm_construct',nativeMap:'sourcebox_room_001',transitioning:false},'sourcebox_room_001',loaded),false);
 assert.equal(mapTravelPaused({map:'gm_construct',nativeMap:'gm_construct',transitioning:false},'sourcebox_room_001',loaded),true);
 assert.equal(mapTravelPaused({map:'ctf_2fort',nativeMap:'sourcebox_room_001',transitioning:false},'sourcebox_room_001',loaded),true);
 assert.equal(mapTravelPaused({map:'gm_construct',nativeMap:'sourcebox_room_001',transitioning:true},'sourcebox_room_001',loaded),true);
});

import {sheets,validate} from '../preflight.mjs';
test('map registry rejects injected commands and duplicate destinations',()=>{
 for(const bad of ['gm_construct;quit','../gm_construct','gm_construct\nquit']){
  const s=structuredClone(sheets);s.mapTravel.destinations[1].map=bad;assert.ok(validate(s).some(e=>e.includes('destination registry')));
 }
 const s=structuredClone(sheets);s.mapTravel.destinations.push(s.mapTravel.destinations[0]);assert.ok(validate(s).some(e=>e.includes('destination registry')));
});
