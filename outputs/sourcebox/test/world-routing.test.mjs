import test from 'node:test';
import assert from 'node:assert/strict';
import {createWorldResolver,canInteractInWorld} from '../world-routing.mjs';
import {sheets} from '../preflight.mjs';
test('world identity follows loaded geometry and prevents damage between separated maps',()=>{
 const resolve=createWorldResolver(sheets);
 assert.equal(resolve([0,0,0]),'sourcebox_room_001');
 for(const world of sheets.destinationWorlds.worlds)assert.equal(resolve(world.spawn),world.id);
 assert.equal(resolve([50000,0,0]),'outside_loaded_worlds');
 assert.equal(canInteractInWorld('gm_construct','gm_construct'),true);
 assert.equal(canInteractInWorld('gm_construct','gm_flatgrass'),false);
 assert.equal(canInteractInWorld('outside_loaded_worlds','outside_loaded_worlds'),false);
 assert.equal(canInteractInWorld(undefined,undefined),false);
});
