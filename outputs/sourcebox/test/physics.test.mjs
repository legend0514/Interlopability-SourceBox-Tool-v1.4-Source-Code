import {test} from 'node:test';import assert from 'node:assert/strict';import net from 'node:net';import {once} from 'node:events';import {createCore} from '../core.mjs';
test('physics transforms, destruction and HL2 weapon forward; foreign physics ownership rejected',async()=>{
 const c=createCore({log:()=>{}});c.server.listen(0,'127.0.0.1');await once(c.server,'listening');const sockets=[];
 async function join(game){const s=net.connect(c.server.address().port,'127.0.0.1');sockets.push(s);await once(s,'connect');const p=once(s,'data');s.write(JSON.stringify({type:'hello',protocol:1,game,pid:12})+'\n');await p;return s;}
 try{const hl=await join('hl2'),tf=await join('tf2');let seq=0;
 for(const alive of [true,false]){const props=[{id:'sourcebox_hl2_crate',alive,...(alive?{position:[10,20,30],rotation:[0,45,0],velocity:[2,3,4],angularVelocity:[0,12,0]}:{})}];const state={type:'state',entity:'player:hl2',seq:++seq,timestamp:seq,position:[1,2,3],rotation:[0,0,0],velocity:[0,0,0],grounded:true,weapon:'weapon_physcannon',props};const received=once(tf,'data'),ack=once(hl,'data');hl.write(JSON.stringify(state)+'\n');const m=JSON.parse((await received)[0]);assert.deepEqual(m.state.props,props);assert.equal(m.state.weapon,'weapon_physcannon');await ack;}
 const closed=once(hl,'close');hl.write(JSON.stringify({type:'state',entity:'player:hl2',seq:3,timestamp:3,position:[0,0,0],rotation:[0,0,0],velocity:[0,0,0],grounded:true,props:[{id:'sourcebox_tf2_crate',alive:false}]})+'\n');await closed;assert.equal(c.clients.has('hl2'),false);
 }finally{for(const s of sockets)s.destroy();await c.close();}
});
