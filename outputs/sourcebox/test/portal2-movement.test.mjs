import {test} from 'node:test';import assert from 'node:assert/strict';import net from 'node:net';import {once} from 'node:events';import {createCore} from '../core.mjs';
test('Portal 2 exchanges states with all five existing game identities',{timeout:5000},async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const socket=net.connect(c.server.address().port,'127.0.0.1');sockets.push(socket);await once(socket,'connect');let buffer='',messages=[],seq=0;socket.on('data',b=>{buffer+=b;let i;while((i=buffer.indexOf('\n'))>=0){messages.push(JSON.parse(buffer.slice(0,i)));buffer=buffer.slice(i+1);}});const send=m=>socket.write(JSON.stringify({...m,seq:++seq,timestamp:seq})+'\n');send({type:'hello',protocol:1,game,pid:1});await wait(()=>messages.some(m=>m.type==='welcome'));return {send,messages};}
 async function wait(f){for(let i=0;i<150;i++){if(f())return;await new Promise(r=>setTimeout(r,5));}throw Error('timed out');}
 try{const cs=await join('portal2'),peers={};for(const id of ['hl2','tf2','gmod','hl1mp','cs2'])peers[id]=await join(id);
 const state={type:'state',position:[0,0,0],rotation:[0,90,0],velocity:[10,0,0],grounded:true};cs.send({...state,entity:'player:portal2'});
 for(const [id,p]of Object.entries(peers)){await wait(()=>p.messages.some(m=>m.type==='remote'&&m.game==='portal2'));p.send({...state,entity:'player:'+id,...(id==='tf2'?{classId:3,team:2}:{}),...(id==='hl1mp'?{model:'models/player/mp/gordon/gordon.mdl'}:{})});await wait(()=>cs.messages.some(m=>m.type==='remote'&&m.game===id));}
 assert.equal(cs.messages.filter(m=>m.type==='remote').length,5);
 // A death removes only the player; a later spawn in the same session must replicate to all receivers.
 const recipients=[cs,peers.hl2,peers.tf2,peers.hl1mp];
 peers.gmod.send({type:'heartbeat',playerUnavailable:true});
 for(const p of recipients)await wait(()=>p.messages.some(m=>m.type==='remove'&&m.game==='gmod'&&m.scope==='player'));
 peers.gmod.send({...state,entity:'player:gmod',model:'models/player/kleiner.mdl',sequence:'idle_physgun',weapon:'weapon_physcannon',position:[123,12,8]});
 for(const p of recipients){await wait(()=>p.messages.some(m=>m.type==='remote'&&m.game==='gmod'&&m.state.position[0]===123));const restored=p.messages.find(m=>m.type==='remote'&&m.game==='gmod'&&m.state.position[0]===123);assert.equal(restored.state.model,'models/player/kleiner.mdl');assert.equal(restored.state.sequence,'idle_physgun');assert.equal(restored.state.weapon,'weapon_physcannon');}

 }finally{for(const s of sockets)s.destroy();await c.close();}
});
