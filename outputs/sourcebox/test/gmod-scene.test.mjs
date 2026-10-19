import {test} from 'node:test';import assert from 'node:assert/strict';import net from 'node:net';import {once} from 'node:events';import {createCore} from '../core.mjs';import {sheets} from '../preflight.mjs';
test('GMod spawned props route to both games; undo and unknown models are enforced',{timeout:5000},async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write(JSON.stringify({type:'hello',protocol:1,game,pid:1})+'\n');await p;return s;}
 try{const h=await join('hl2'),t=await join('tf2'),g=await join('gmod');let seq=0;
 const send=(live,objects)=>g.write(JSON.stringify({type:'scene',generation:'one',seq:++seq,timestamp:seq,live,objects})+'\n');
 let hp=once(h,'data'),tp=once(t,'data');const object={id:1,model:sheets.gmodScene.models[0],position:[1,2,3],rotation:[4,5,6],velocity:[0,0,0],angularVelocity:[0,0,0]};send([1],[object]);
 for(const p of [hp,tp]){const m=JSON.parse((await p)[0]);assert.equal(m.type,'scene');assert.deepEqual(m.objects[0],object);assert(m.session.endsWith(':one'));}
 hp=once(h,'data');send([],[]);assert.deepEqual(JSON.parse((await hp)[0]).live,[]);
 const closed=once(g,'close');send([1],[{...object,model:'models/not-installed.mdl'}]);await closed;assert(!c.clients.has('gmod'));
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
