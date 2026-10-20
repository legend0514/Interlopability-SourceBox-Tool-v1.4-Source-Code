import fs from 'node:fs';
import {preflightWorlds} from './world-preflight.mjs';
import {fileURLToPath} from 'node:url';
const manifest=JSON.parse(fs.readFileSync(new URL('./sheets/connectivity.json',import.meta.url)));
export const sheets={...manifest};
if(manifest.sheets)for(const [key,path]of Object.entries(manifest.sheets)){
 if(!/^[a-z0-9-]+\.json$/.test(path))throw Error('Invalid sheet path');
 sheets[key]=JSON.parse(fs.readFileSync(new URL('./sheets/'+path,import.meta.url)));
}
export function validate(s){
 const errors=[];
 if(s.physics?.cube?.enabled!==undefined&&typeof s.physics.cube.enabled!=='boolean')errors.push('physics cube enabled must be boolean');
 if(s.runtime?.playerGraceMs!==undefined&&(!Number.isInteger(s.runtime.playerGraceMs)||s.runtime.playerGraceMs<0||s.runtime.playerGraceMs>s.runtime.timeoutMs))errors.push('player grace must be bounded by connection timeout');
 const check=(v,m)=>{if(!v)errors.push(m)};
 if(!s||typeof s!=='object')return ['Missing sheet configuration'];
 if(s.destinationWorlds){const w=s.destinationWorlds;check(typeof w.enabled==='boolean'&&Array.isArray(w.portalGames)&&w.portalGames.every(g=>['hl2','tf2','hl1mp','gmod','cs2','portal2'].includes(g)),'Destination capability registry');check(Array.isArray(w.worlds)&&w.worlds.length>0&&w.worlds.length<=32&&new Set(w.worlds.map(d=>d.id)).size===w.worlds.length&&w.worlds.every(d=>/^[a-z0-9_]+$/.test(d.id)&&typeof d.label==='string'&&d.label.length<80&&Number.isFinite(d.yaw)&&d.spawn?.length===3&&d.spawn.every(v=>Number.isFinite(v)&&Math.abs(v)<16000)),'Destination portal definitions');}
 check(s.protocol===1,'protocol must be 1'); check(['A','B','C'].includes(s.phase),'Only phases A/B/C are implemented');
 check(s.runtime?.host==='127.0.0.1','loopback required');
 for(const k of ['port','maxFrameBytes','timeoutMs','heartbeatMs'])check(Number.isInteger(s.runtime?.[k])&&s.runtime[k]>0,`runtime.${k}`);
 check(s.runtime?.port<=65535,'port range'); check(s.runtime?.timeoutMs>s.runtime?.heartbeatMs,'heartbeat timeout');
 if(s.cs2)check(s.cs2.id==='cs2'&&s.cs2.appId===730&&['connectivity-probe','bidirectional-movement-candidate'].includes(s.cs2.stage)&&s.cs2.serverPort===27019,'CS2 local probe configuration');
 if(s.portal2){check(s.portal2.sharedCubeModel==='models/props/metal_box.mdl','Portal 2 cube must reference its installed native physics model');check(s.portal2.id==='portal2'&&s.portal2.appId===620&&s.portal2.architecture==='x86'&&s.portal2.port===27020&&s.portal2.scale===1,'Portal 2 adapter configuration');check(s.portal2.rendering?.fastPath===0&&Number.isInteger(s.portal2.rendering?.stencilDepth)&&s.portal2.rendering.stencilDepth>=1&&s.portal2.rendering.stencilDepth<=2,'Portal 2 requires multi-pair stencil rendering with bounded recursion');}
 if(s.worlds){const w=s.worlds;check(w.host==='gmod'&&w.nativeMap==='sourcebox_room_001'&&typeof w.enabled==='boolean','World host configuration');check(Array.isArray(w.worlds)&&w.worlds.length>0&&w.worlds.every(d=>/^[a-z0-9_]+$/.test(d.id)&&typeof d.source==='string'&&d.offset?.length===3&&d.offset.every(Number.isFinite)&&d.spawn?.length===3&&d.spawn.every(n=>Number.isFinite(n)&&Math.abs(n)<16000)&&d.sourceZ?.length===2&&d.sourceZ[0]<d.sourceZ[1])&&new Set(w.worlds.map(d=>d.id)).size===w.worlds.length,'World geometry registry');}
 if(s.mapTravel){const m=s.mapTravel;check(typeof m.enabled==='boolean'&&m.adapter==='gmod'&&m.hub==='sourcebox_room_001'&&m.mode==='isolated-native-map-excursion','Map travel requires isolated native GMod worlds');check(Array.isArray(m.destinations)&&m.destinations.length>0&&m.destinations.length<=32&&m.destinations.every(d=>/^[a-z0-9_]+$/.test(d.map)&&typeof d.label==='string'&&d.label.length>0&&d.label.length<64)&&new Set(m.destinations.map(d=>d.map)).size===m.destinations.length&&m.destinations.some(d=>d.map===m.hub),'Map travel destination registry');}
 if(s.roster)check(s.roster.length===6&&new Set(s.roster.map(r=>r.id)).size===6&&s.roster.every(r=>r.entity==='player:'+r.id)&&s.roster.find(r=>r.id==='gmod')?.displayName==='DeGroot (Goblin)','six character roster');
 const games=Array.isArray(s.games)?s.games:[];
 if(s.gmod?.performance){const p=s.gmod.performance;check(Number.isFinite(p.portalTextureScale)&&p.portalTextureScale>=0.25&&p.portalTextureScale<=1&&typeof p.dynamicPortalLights==='boolean','Invalid GMod performance configuration');}
 if(s.portals){
  check(s.portals.inventoryMode==='native-slot'&&s.portals.preserveLoadout===true,'Portal Gun requires native inventory slots and preserved loadouts');
  check(['hl2','tf2','hl1mp','cs2','gmod','portal2'].every(id=>Array.isArray(s.portals.weaponClasses?.[id])&&s.portals.weaponClasses[id].every(name=>/^(weapon_|tf_weapon_)[a-z0-9_]+$/.test(name))),'Portal Gun weapon class registry');
 }
 if(s.portals)check(Number.isFinite(s.portals.momentumRecoveryMs)&&s.portals.momentumRecoveryMs>=0&&s.portals.momentumRecoveryMs<=150,'Portal momentum recovery window');
 if(s.portals?.playerHulls)for(const [game,hull] of Object.entries(s.portals.playerHulls)){check(s.portals.owners.includes(game)&&Number.isFinite(hull.playerRadius)&&hull.playerRadius>0&&hull.playerRadius<s.portals.aperture.halfWidth&&Number.isFinite(hull.playerHalfHeight)&&hull.playerHalfHeight>0&&hull.playerHalfHeight<s.portals.aperture.halfHeight,'Invalid native portal hull: '+game);}
 if(s.portals?.destinationPreview){const p=s.portals.destinationPreview;check([256,512,1024].includes(p.resolution)&&p.maxRecursion===0&&p.drawLocalPlayer===true&&p.hl1==='sdk-tool-linked-camera','Invalid destination preview renderer configuration');}
 if(s.portals){check(s.portals.environment==='sourcebox_room_001'&&s.portals.owners.length===6&&s.portals.aperture.halfWidth>s.portals.playerRadius&&s.portals.aperture.halfHeight>s.portals.playerHalfHeight,'portal geometry');check(typeof s.portals.enabled==='boolean','Portal enabled flag');if(s.portals.enabled){check(s.portals.activationPolicy==='capability-gated'&&s.portals.requireNativeTrace===true&&s.portals.requireExitClearance===true,'Enabled portals require native placement traces and advertised exit-clearance capability');check(Number.isInteger(s.portals.ackTimeoutMs)&&s.portals.ackTimeoutMs>0&&s.portals.ackTimeoutMs<=1500,'Portal traversal deadline');}}
 check(games.length===2,'two games required');
 for(const id of ['hl2','tf2']){
  const g=games.filter(g=>g?.id===id);check(g.length===1,`unique game ${id}`);
  check(g?.[0]?.appId===(id==='hl2'?220:440),`appid ${id}`);
  check(g?.[0]?.architecture===(id==='hl2'?'x86':'x64'),`architecture ${id}`);
  check(g?.[0]?.adapter===`sourcebox_${id}.dll`,`adapter ${id}`);
  check(s.coordinates?.[id]?.scale===1&&s.coordinates[id].origin?.length===3&&s.coordinates[id].origin.every(Number.isFinite),`coordinate mapping ${id}`);
 }
 check(JSON.stringify(s.messages?.hello)===JSON.stringify({protocol:'integer',game:'gameId',pid:'positiveInteger'})&&JSON.stringify(s.messages?.heartbeat)===JSON.stringify({playerUnavailable:'optionalBooleanExplicitDeathOrObserver',seq:'positiveInteger',timestamp:'nonnegativeNumber',props:'optionalOwnedPropArray'}),'message schema mismatch');
 check(Array.isArray(s.entities)&&(s.phase==='A'?s.entities.length===0:s.entities.length===2&&new Set(s.entities.map(e=>e?.id)).size===2&&s.entities.every(e=>e&&e.id==='player:'+e.owner&&games.some(g=>g.id===e.owner)&&e.kind==='player')),'entity definitions');
 check(s.assets?.redistribute===false,'asset redistribution disabled');
 check(s.physics?.rule==='exactly one owner; transfer requires old-owner freeze acknowledgement before grant','ownership rule');
 check(Array.isArray(s.tests)&&s.tests.length>=3&&s.tests.every(t=>t&&typeof t.id==='string'&&typeof t.description==='string'&&typeof t.status==='string')&&new Set(s.tests.map(t=>t.id)).size===s.tests.length,'test definitions');
 if(['B','C'].includes(s.phase)){
  check(s.movement?.sendHz===20&&s.movement?.interpolationMs===100,'movement timing');
  check(JSON.stringify(s.movement?.directions)===JSON.stringify(s.phase==='B'?[['hl2','tf2']]:[['hl2','tf2'],['tf2','hl2']]),'Movement directions');
  check(JSON.stringify(s.messages?.state)===JSON.stringify({entity:'ownedEntityId',seq:'positiveInteger',timestamp:'nonnegativeNumber',position:'vector3',rotation:'vector3',velocity:'vector3',grounded:'boolean',classId:'tf2ClassIdWhenTF2',team:'tf2TeamWhenTF2',weapon:'optionalNativeWeaponName',props:'optionalOwnedPropArray',health:'optionalNonnegativeIntegerTelemetry',world:'serverDerivedWorldIdInReplicatedState'}),'movement message schema');
 }
 if(s.phase==='C')for(const id of ['hl2','tf2']){
  const p=s.assets?.proxies?.[id];
  check(typeof p?.model==='string'&&/^models\/[a-z0-9_/.]+\.mdl$/.test(p.model),'proxy model '+id);
  check(typeof p?.archive==='string'&&/^[a-f0-9]{64}$/.test(p?.sha256??''),'proxy asset reference '+id);
  for(const k of ['idle','walk','run','air'])check(Number.isInteger(p?.sequences?.[k]?.index)&&p.sequences[k].index>=0&&typeof p.sequences[k].name==='string','proxy sequence '+id+' '+k);
 }
 if(s.phase==='C')for(let id=1;id<=9;id++){
  const c=s.assets?.tf2Classes?.[id];check(c&&/^models\/player\/[a-z]+\.mdl$/.test(c.model)&&/^[a-f0-9]{64}$/.test(c.sha256),'TF2 class asset '+id);
  for(const kind of ['idle','walk','run','air'])check(Number.isInteger(c?.sequences?.[kind]?.index)&&typeof c.sequences[kind].name==='string','TF2 class animation '+id+' '+kind);
 }
 check(s.movement?.collision?.enabled===true&&s.movement.collision.solid===2,'player collision configuration');
 for(const [name,w]of Object.entries(s.assets?.weapons??{}))check(/^tf_weapon_[a-z_]+$/.test(name)&&/^models\/weapons\/[a-z0-9_/.]+\.mdl$/.test(w.model),'weapon reference '+name);
 check(s.hl2Gameplay?.mode==='native-hl2'&&s.hl2Gameplay.loadoutCommand==='impulse 101','HL2 native loadout');
 check(Array.isArray(s.hl2Gameplay?.commands)&&s.hl2Gameplay.commands.includes('impulse 101')&&s.hl2Gameplay.commands.includes('sv_infinite_aux_power 0'),'HL2 gameplay commands');
 check(s.hl2Gameplay?.bindings?.SHIFT==='+speed'&&s.hl2Gameplay?.bindings?.f==='impulse 100','HL2 suit controls');
 check(s.physics?.objects?.length===4&&new Set(s.physics.objects.map(p=>p.id)).size===4&&s.physics.objects.every(p=>/^sourcebox_(hl2|tf2)_(barrel|crate)$/.test(p.id)&&['hl2','tf2'].includes(p.owner)&&/^models\/[a-z0-9_/.]+\.mdl$/.test(p.model)&&p.position?.length===3&&p.position.every(Number.isFinite)),'fixed physics ownership definitions');
 check(s.physics?.ammo?.command==='givecurrentammo'&&s.physics.ammo.interval===0.5,'ammo refill configuration');
 const cube=s.physics?.cube;
 check(cube?.id==='sourcebox_shared_cube'&&cube.owner==='hl2'&&cube.model==='models/props_junk/wood_crate001a.mdl'&&cube.position?.length===3&&cube.position.every(Number.isFinite)&&cube.claimDistance===160&&cube.cooldownMs>=2000&&cube.freezeTimeoutMs===3000,'shared cube ownership configuration');
 check(s.messages?.cube?.capability===1&&s.messages.cube.acknowledge==='cube_frozen'&&s.messages.cube.grant==='cube_owner','cube handoff protocol');
 check(s.combat?.enabled===true&&Number.isInteger(s.combat.damagePerHit)&&s.combat.damagePerHit>0&&s.combat.damagePerHit<=100&&s.combat.maxHitsPerSecond===20&&s.combat.respawnProtectionMs===1000&&['hl2','tf2'].every(g=>s.combat.spawns?.[g]?.length===3&&s.combat.spawns[g].every(Number.isFinite)),'combat damage and respawn configuration');
 check(Number.isInteger(s.physics?.respawnMs)&&s.physics.respawnMs>=1000&&s.physics.respawnMs<=60000,'prop respawn interval');
 check(s.hl2Gameplay?.bindings?.e==='+sourcebox_use'&&s.hl2Gameplay.commands.includes('alias +sourcebox_use "+use; cmd sourcebox_take"')&&s.hl2Gameplay.commands.includes('alias -sourcebox_use "-use"'),'native Use and ownership interaction binding');
 if(s.gmod)check(s.gmod.id==='gmod'&&s.gmod.appId===4000&&s.gmod.pollMs===50&&s.gmod.timeoutMs===2000&&s.gmod.scale===1&&JSON.stringify(s.gmod.origin)==='[0,0,0]'&&JSON.stringify(s.gmod.inbound)==='["hl2","tf2"]'&&JSON.stringify(s.gmod.outbound)==='["hl2","tf2"]'&&s.gmod.physicsOwnership===true,'GMod staged adapter configuration');
 if(s.gmodScene?.addon){const a=s.gmodScene.addon;check(a.id==='3597824658'&&a.redistribute===false&&typeof a.source==='string'&&[...a.npcModels,...a.playerModels].every(m=>/^models\/[a-z0-9_/-]+\.mdl$/.test(m)),'local addon references');}
 if(s.gmodScene)check(Array.isArray(s.gmodScene.npcModels)&&s.gmodScene.npcModels.every(m=>/^models\/[a-z0-9_/-]+\.mdl$/.test(m))&&s.gmodScene.interactions?.authority==='gmod'&&s.gmodScene.interactions.useDistance===160&&s.gmodScene.interactions.hitDistance===2048&&s.gmodScene.interactions.maxPerSecond===20&&s.gmodScene.interactions.damage===10&&s.gmodScene.interactions.pushSpeed===180,'scene interaction rules');
 if(s.gmodScene)check(s.gmodScene.owner==='gmod'&&s.gmodScene.maxEntities===32&&s.gmodScene.batchSize===4&&s.gmodScene.timeoutMs===2000&&Array.isArray(s.gmodScene.models)&&s.gmodScene.models.length>0&&new Set(s.gmodScene.models).size===s.gmodScene.models.length&&s.gmodScene.models.every(m=>/^models\/props[a-z0-9_/-]+\.mdl$/.test(m)),'GMod scene bounds and installed model references');
 check(Number.isInteger(s.gmod?.bridgeLeasePort)&&s.gmod.bridgeLeasePort>1024&&s.gmod.bridgeLeasePort<=65535&&s.gmod.bridgeLeasePort!==s.runtime?.port,'GMod bridge lease port');
 check(JSON.stringify(s.combat?.gmodTargets)==='["hl2","tf2","hl1mp","cs2","portal2"]','GMod damage targets');
 if(s.hl1mp)check(s.hl1mp.id==='hl1mp'&&s.hl1mp.appId===360&&s.hl1mp.architecture==='x64'&&s.hl1mp.scale===1&&s.hl1mp.origin?.length===3&&Object.keys(s.hl1mp.models??{}).length===9&&JSON.stringify(s.hl1mp.peers)==='["hl2","tf2","gmod"]','HL1MP adapter references');
 return errors;
}
export function preflight(){const e=validate(sheets);if(e.length)throw Error(e.join('\n'));preflightWorlds(sheets.worlds);preflightWorlds(sheets.destinationWorlds);return sheets;}
if(process.argv[1]===fileURLToPath(import.meta.url)){preflight();console.log(`Phase ${sheets.phase} configuration preflight passed. Runtime acceptance is a separate check.`);}







