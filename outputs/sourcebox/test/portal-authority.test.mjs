import {test} from 'node:test';
import assert from 'node:assert/strict';
import {sheets,validate} from '../preflight.mjs';
import {wallHit,aim,add} from '../portal-math.mjs';
import {createPortals} from '../portals.mjs';
import {createPortalSnapshotReader} from '../portal-snapshot.mjs';
function fixture(){
 let time=1000;const config=structuredClone(sheets.portals);config.enabled=true;
 const clients=new Map(),sent=[],events=[];
 const portal=createPortals({config,clients,now:()=>time,emit:(c,m)=>sent.push({game:c.game,...structuredClone(m)}),log:(type,data)=>events.push({type,...data})});
 const client=(game='gmod',y=0)=>{const c={game,session:'session:'+game,portalProtocol:1,portalTraverse:true,hasState:true,lastStateAt:time,position:[0,y,0],rotation:[0,0,0],weapon:'weapon_sourcebox_portalgun'};config.weaponClasses[game]=['weapon_sourcebox_portalgun'];clients.set(game,c);return c;};
 const advance=()=>{time+=300;for(const c of clients.values())c.lastStateAt=time;};
 const fire=(c,endpoint,yaw)=>{c.rotation=[0,yaw,0];const eye=add(c.position,[0,0,64]);portal.handle(c,{type:'portal_fire',session:c.session,endpoint,eye,hit:wallHit(eye,aim(c.rotation),config).position});advance();};
 return {portal,config,clients,sent,events,client,advance,fire};
}
test('placement requires fresh state, equipped native weapon, current session, and respects rate limits',()=>{
 const f=fixture(),c=f.client();c.weapon='weapon_crowbar';f.fire(c,0,0);assert.equal(f.portal.inspect().pairs.length,0);
 c.weapon='weapon_sourcebox_portalgun';f.portal.handle(c,{type:'portal_fire',session:'old',endpoint:0});assert.equal(f.portal.inspect().pairs.length,0);
 c.lastStateAt=0;f.portal.handle(c,{type:'portal_fire',session:c.session,endpoint:0});assert.equal(f.portal.inspect().pairs.length,0);
 f.advance();f.fire(c,0,0);const rev=f.portal.inspect().revision;
 f.portal.handle(c,{type:'portal_fire',session:c.session,endpoint:1});assert.equal(f.portal.inspect().revision,rev);
});
test('walking through a floor-level wall portal emits one traversal and ignores stale acknowledgements',()=>{
 const f=fixture(),c=f.client();f.fire(c,0,0);f.fire(c,1,180);
 const sample=x=>f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[220,0,0]});
 sample(450);sample(498);const packet=f.sent.find(m=>m.type==='portal_traverse');assert.ok(packet);assert.ok(Math.abs(packet.position[2])<1e-6);assert.ok(Math.abs(Math.hypot(...packet.velocity)-220)<1e-6);
 sample(450);sample(498);assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,1);
 f.portal.handle(c,{type:'portal_ack',session:'old',event:packet.event,applied:true});assert.equal(f.events.filter(e=>e.type==='portal_applied').length,0);
 f.portal.handle(c,{type:'portal_ack',session:c.session,event:packet.event,applied:true});assert.equal(f.events.filter(e=>e.type==='portal_applied').length,1);
 f.portal.handle(c,{type:'portal_ack',session:c.session,event:packet.event,applied:true});assert.equal(f.events.filter(e=>e.type==='portal_applied').length,1);
});
test('six linked pairs fit bounded frames and snapshots commit atomically across reordering and reconnect',()=>{
 const f=fixture();for(const [i,id] of f.config.owners.entries()){const c=f.client(id,-400+i*160);f.fire(c,0,0);f.fire(c,1,180);}
 assert.equal(f.portal.inspect().pairs.length,6);f.sent.length=0;const c=f.clients.get('gmod');f.portal.welcome(c);
 assert.equal(f.sent.length,6);for(const m of f.sent)assert.ok(Buffer.byteLength(JSON.stringify(m))<4096);
 const r=createPortalSnapshotReader();r.reset(c.session);const frames=f.sent.toReversed();for(const m of frames.slice(0,-1)){r.accept(m);assert.equal(r.read(),null);}assert.ok(r.accept(frames.at(-1)));assert.equal(r.read().pairs.length,6);
 assert.equal(r.accept(frames[0]),false);r.reset('replacement');assert.equal(r.accept(frames[0]),false);assert.equal(r.read(),null);
 f.portal.disconnect(c);assert.equal(f.portal.inspect().pairs.length,5);
});
test('successful travel ignores queued entry states until destination arrival, then permits return',()=>{
 const f=fixture(),c=f.client('cs2');f.fire(c,0,0);f.fire(c,1,180);
 const sample=x=>f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[220,0,0]});
 sample(450);sample(498);const packet=f.sent.find(m=>m.type==='portal_traverse');assert.ok(packet);
 f.portal.handle(c,{type:'portal_ack',session:c.session,event:packet.event,applied:true});
 for(const x of [498,498,450,498,498])sample(x);
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,1);
 sample(packet.position[0]);sample(-450);sample(-498);
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,2);
});
test('an occupied exit rejects crossing without moving the player',()=>{
 const f=fixture(),c=f.client();f.fire(c,0,0);f.fire(c,1,180);const blocker=f.client('hl2');blocker.position=[-490,0,0];
 for(const x of [450,498])f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[200,0,0]});
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,0);assert.ok(f.sent.some(m=>m.reason==='Exit blocked by a player'));
});
test('all six actors can use their own and every other owner pair in both directions without equipping a gun',()=>{
 const games=sheets.portals.owners;
 for(const owner of games)for(const traveler of games)for(const direction of [1,-1]){
  const f=fixture(),creator=f.client(owner);f.fire(creator,0,0);f.fire(creator,1,180);
  const actor=owner===traveler?creator:f.client(traveler,250);
  actor.weapon='weapon_crowbar';
  for(const x of [450,498])f.portal.sample(actor,{position:[direction*x,0,0],rotation:[0,direction===1?0:180,0],velocity:[direction*220,0,0]});
  const packets=f.sent.filter(m=>m.type==='portal_traverse');
  assert.equal(packets.length,1,`${traveler} through ${owner}, direction ${direction}`);
  assert.equal(packets[0].game,traveler);assert.equal(packets[0].pair,'pair:'+owner);
 }
});
test('build preflight rejects replacing native inventory with a toggle',()=>{const s=structuredClone(sheets);s.portals.inventoryMode='F9';assert.ok(validate(s).includes('Portal Gun requires native inventory slots and preserved loadouts'));});

test('adapters without clearance capability never receive traversal commands',()=>{
 const f=fixture(),c=f.client();f.fire(c,0,0);f.fire(c,1,180);c.portalTraverse=false;
 for(const x of [450,498])f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[200,0,0]});
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,0);
});
test('native obstruction prevents placement and traversal events have a deadline',()=>{
 const f=fixture(),c=f.client();f.portal.handle(c,{type:'portal_fire',session:c.session,endpoint:0,eye:[0,0,64],hit:[100,0,64]});
 assert.equal(f.portal.inspect().pairs.length,0);f.advance();f.fire(c,0,0);f.fire(c,1,180);
 for(const x of [450,498])f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[200,0,0]});
 assert.ok(f.sent.find(m=>m.type==='portal_traverse').expiresAt>0);
});

test('HL1 and CS2 native hulls can walk through an aperture without exact center alignment',()=>{
 for(const game of ['hl1mp','cs2']){
  const f=fixture(),c=f.client(game);f.fire(c,0,0);f.fire(c,1,180);
  for(const x of [450,496])f.portal.sample(c,{position:[x,12,0.03125],rotation:[0,0,0],velocity:[200,0,0]});
  const packet=f.sent.find(m=>m.type==='portal_traverse');assert.ok(packet,game);assert.ok(packet.position[0]>-496,game+' exit clears 16-unit native hull');
 }
});
test('missing arrival sample cannot permanently disable later traversal',()=>{
 const f=fixture(),c=f.client('cs2');f.fire(c,0,0);f.fire(c,1,180);
 const sample=x=>f.portal.sample(c,{position:[x,0,0],rotation:[0,0,0],velocity:[220,0,0]});
 sample(450);sample(498);const packet=f.sent.find(m=>m.type==='portal_traverse');
 f.portal.handle(c,{type:'portal_ack',session:c.session,event:packet.event,applied:true});
 for(let i=0;i<11;i++)f.advance();
 sample(0); // recovered after an unobserved arrival / respawn, no teleport
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,1);
 sample(450);sample(498);
 assert.equal(f.sent.filter(m=>m.type==='portal_traverse').length,2);
 assert.equal(f.events.filter(e=>e.type==='portal_arrival_recovered').length,1);
});
