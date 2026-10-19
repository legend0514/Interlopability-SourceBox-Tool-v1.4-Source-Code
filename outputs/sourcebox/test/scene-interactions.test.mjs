import {test} from 'node:test';import assert from 'node:assert/strict';import net from 'node:net';import {once} from 'node:events';import {createCore} from '../core.mjs';import {sheets} from '../preflight.mjs';
test('NPC scenes reach all four receivers; only fresh nearby interactions reach authoritative GMod',{timeout:5000},async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');let buffer='',queue=[],seq=0;s.on('data',b=>{buffer+=b;let i;while((i=buffer.indexOf('\n'))>=0){queue.push(JSON.parse(buffer.slice(0,i)));buffer=buffer.slice(i+1);}});const send=m=>s.write(JSON.stringify({...m,seq:++seq,timestamp:seq})+'\n');send({type:'hello',protocol:1,game,pid:1});await wait(()=>queue.find(m=>m.type==='welcome'));return {send,queue};}
 async function wait(f){for(let n=0;n<200;n++){const v=f();if(v)return v;await new Promise(r=>setTimeout(r,5));}throw Error('timeout');}
 try{
 const h=await join('hl2'),t=await join('tf2'),d=await join('hl1mp'),cs=await join('cs2'),g=await join('gmod');
 const object={id:1,kind:'npc',model:sheets.gmodScene.addon.npcModels[0],sequence:'walk',health:100,position:[10,0,0],rotation:[0,0,0],velocity:[0,0,0],angularVelocity:[0,0,0]};
 g.send({type:'scene',generation:'one',live:[1],objects:[object]});
 for(const peer of [h,t,d,cs])assert.deepEqual((await wait(()=>peer.queue.find(m=>m.type==='scene'))).objects[0],{...object,renderModel:'models/sourcebox/npc/'+object.model.slice(7)});
 h.send({type:'state',entity:'player:hl2',position:[0,0,0],rotation:[0,0,0],velocity:[0,0,0],grounded:true});await wait(()=>c.clients.get('hl2').hasState);
 g.send({type:'state',entity:'player:gmod',model:sheets.gmodScene.addon.playerModels[0],sequence:'walk_all',position:[0,0,0],rotation:[0,0,0],velocity:[10,0,0],grounded:true});
 const appearance=await wait(()=>h.queue.find(m=>m.type==='remote'&&m.game==='gmod'));assert.equal(appearance.state.model,sheets.gmodScene.addon.playerModels[0]);assert.equal(appearance.state.sequence,'walk_all');
 const session=h.queue.find(m=>m.type==='scene').session;
 h.send({type:'scene_interact',session,id:1,action:'hit',amount:99999});const action=await wait(()=>g.queue.find(m=>m.type==='scene_interact'));assert.equal(action.source,'hl2');assert.equal(action.amount,undefined);assert.equal(action.generation,'one');
 const count=()=>g.queue.filter(m=>m.type==='scene_interact').length;
 h.send({type:'scene_interact',session:'stale',id:1,action:'use'});
 h.send({type:'scene_interact',session,id:99,action:'hit'});
 h.send({type:'state',entity:'player:hl2',position:[500,0,0],rotation:[0,0,0],velocity:[0,0,0],grounded:true});
 h.send({type:'scene_interact',session,id:1,action:'use'});
 await new Promise(r=>setTimeout(r,40));assert.equal(count(),1);
 cs.send({type:'state',entity:'player:cs2',position:[0,0,0],rotation:[0,0,0],velocity:[0,0,0],grounded:true,team:2});await wait(()=>c.clients.get('cs2').hasState);
 cs.send({type:'scene_interact',session,id:1,action:'use'});await wait(()=>g.queue.some(m=>m.type==='scene_interact'&&m.source==='cs2'&&m.action==='use'));
 cs.send({type:'scene_interact',session,id:1,action:'hit'});await wait(()=>g.queue.some(m=>m.type==='scene_interact'&&m.source==='cs2'&&m.action==='hit'));

 g.send({type:'scene',generation:'two',live:[],objects:[]});await wait(()=>c.clients.get('gmod').sceneGeneration==='two');assert.equal(c.clients.get('gmod').scene.size,0);
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
