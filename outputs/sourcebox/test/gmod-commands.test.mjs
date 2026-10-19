import {test} from 'node:test';
import assert from 'node:assert/strict';
import {pendingCommands} from '../gmod-commands.mjs';
test('GMod reconnect retires old hits and handoffs, then accepts fresh commands',()=>{
 const old={connection:'old',commands:[{id:4,message:{type:'player_hit'}},{id:5,message:{type:'cube_take'}}]};
 const retired=pendingCommands(old,'new',3);
 assert.equal(retired.acknowledged,5);assert.deepEqual(retired.commands,[]);
 const fresh={connection:'new',commands:[...old.commands,{id:6,message:{type:'cube_sync'}}]};
 assert.deepEqual(pendingCommands(fresh,'new',retired.acknowledged).commands,[fresh.commands[2]]);
 assert.deepEqual(pendingCommands(fresh,'new',6).commands,[]);
});
test('GMod startup without a connection cannot send queued interactions',()=>{
 assert.deepEqual(pendingCommands({commands:[{id:1,message:{type:'player_hit'}}]},'new',0),{acknowledged:1,commands:[]});
});
