import fs from 'node:fs';
import {preflight} from './preflight.mjs';
const s=preflight();
const destinationWorlds=[...s.destinationWorlds.worlds,{id:s.destinationWorlds.nativeMap??'sourcebox_room_001',label:'Shared Room'}];
const preview=s.portals?.destinationPreview;
fs.mkdirSync(new URL('./generated/',import.meta.url),{recursive:true});
fs.writeFileSync(new URL('./generated/world_destinations.h',import.meta.url),'// Generated from destination world sheets.\nstruct WorldDestination {const char* id;const char* label;};\nstatic const WorldDestination worldDestinations[]={'+destinationWorlds.map(w=>'{'+JSON.stringify(w.id)+','+JSON.stringify(w.label)+'}').join(',')+'};\n');
fs.writeFileSync(new URL('./generated/portal_preview_config.h',import.meta.url),`// Generated from portal sheets.\n#define SOURCEBOX_PREVIEW_SIZE ${preview?.resolution??512}\n#define SOURCEBOX_PREVIEW_HALF_WIDTH ${Number(s.portals.aperture.halfWidth).toFixed(1)}f\n#define SOURCEBOX_PREVIEW_HALF_HEIGHT ${Number(s.portals.aperture.halfHeight).toFixed(1)}f\n`);
fs.writeFileSync(new URL('./generated/generated_config.h',import.meta.url),`// Generated from sheets; do not edit.\n#define SOURCEBOX_PORT ${s.runtime.port}\n#define SOURCEBOX_HEARTBEAT_MS ${s.runtime.heartbeatMs}\n#define SOURCEBOX_UPDATE_MS ${s.phase!=='A'?1000/s.movement.sendHz:1000}\n#define SOURCEBOX_INTERPOLATION_MS ${s.movement.interpolationMs}\n`);

if(s.phase==='C'){
 const p=s.assets.proxies;
 const row=c=>'{'+[c.model,c.label,...['idle','walk','run','air'].map(k=>c.sequences[k].name)].map(JSON.stringify).join(',')+','+['idle','walk','run','air'].map(k=>c.sequences[k].index).join(',')+'}';
 fs.writeFileSync(new URL('./generated/proxy_config.h',import.meta.url),'// Generated from asset references; asset hashes verified before build.\nstruct ProxyConfig {const char *model,*label,*idle,*walk,*run,*air;int idleIndex,walkIndex,runIndex,airIndex;};\nstatic const ProxyConfig soldier='+row(p.hl2)+';\nstatic const ProxyConfig gordon='+row(p.tf2)+';\nstatic const ProxyConfig tfClasses[9]={'+Object.values(s.assets.tf2Classes).map(row).join(',')+'};\n');
}

fs.appendFileSync(new URL('./generated/generated_config.h',import.meta.url),'#define SOURCEBOX_PLAYER_SOLID '+s.movement.collision.solid+'\n');
fs.appendFileSync(new URL('./generated/generated_config.h',import.meta.url),'#define SOURCEBOX_DIAGNOSTIC_PATH '+JSON.stringify(new URL('./logs/native-animation.log',import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1'))+'\n');
fs.writeFileSync(new URL('./generated/weapon_config.h',import.meta.url),'// Generated from sheets\nstruct WeaponConfig {const char *name,*model;};\nstatic const WeaponConfig weaponModels[]={'+Object.entries(s.assets.weapons).map(([n,w])=>'{'+JSON.stringify(n)+','+JSON.stringify(w.model)+'}').join(',')+'};\n');

fs.writeFileSync(new URL('./generated/physics_config.h',import.meta.url),'struct SharedProp {const char *id,*owner,*model,*spawn;};\nstatic const SharedProp sharedProps[]={'+s.physics.objects.map(p=>'{'+[p.id,p.owner,p.model,p.position.join(" ")].map(JSON.stringify).join(',')+'}').join(',')+'};\n');
fs.appendFileSync(new URL('./generated/weapon_config.h',import.meta.url),'static const WeaponConfig hl2WeaponModels[]={'+Object.entries(s.assets.hl2Weapons).map(([n,m])=>'{'+JSON.stringify(n)+','+JSON.stringify(m)+'}').join(',')+'};\n');

fs.appendFileSync(new URL('./generated/physics_config.h',import.meta.url),'\n#define SOURCEBOX_CUBE_ID '+JSON.stringify(s.physics.cube.id)+'\n#define SOURCEBOX_CUBE_MODEL '+JSON.stringify(s.physics.cube.model)+'\n');

fs.appendFileSync(new URL('./generated/physics_config.h',import.meta.url),'\n#define SOURCEBOX_RESPAWN_MS '+s.physics.respawnMs+'\n#define SOURCEBOX_CUBE_INITIAL '+JSON.stringify(JSON.stringify(s.physics.cube.position))+'\n');

fs.writeFileSync(new URL('./generated/combat_config.h',import.meta.url),'#define SOURCEBOX_RESPAWN_PROTECTION_MS '+s.combat.respawnProtectionMs+'\nstatic const char* combatSpawnHL2='+JSON.stringify(s.combat.spawns.hl2.join(' '))+';\nstatic const char* combatSpawnTF2='+JSON.stringify(s.combat.spawns.tf2.join(' '))+';\n');

if(s.hl1mp){fs.writeFileSync(new URL('./generated/hl1mp_config.h',import.meta.url),'// Generated installed HL1MP model and weapon references.\nstatic const char* hl1mpModelConfig='+JSON.stringify(JSON.stringify(s.hl1mp.models))+';\nstatic const char* hl1mpWeaponConfig='+JSON.stringify(JSON.stringify(s.hl1mp.weapons))+';\nstatic const char* combatSpawnHL1='+JSON.stringify(s.hl1mp.spawn.join(' '))+';\n');}

const cs2=JSON.parse(fs.readFileSync(new URL('./sheets/cs2.json',import.meta.url)));
for(const team of ['2','3'])if(!cs2.source1Teams[team]?.model)throw Error('Missing CS2 team model');
for(const key of ['idle','run','air'])if(!cs2.source1Sequences[key])throw Error('Missing CS2 sequence');
fs.appendFileSync(new URL('./generated/generated_config.h',import.meta.url),'static const char* cs2TeamConfig='+JSON.stringify(JSON.stringify(cs2.source1Teams))+';\nstatic const char* cs2SequenceConfig='+JSON.stringify(JSON.stringify(cs2.source1Sequences))+';\n');

if(s.portal2)fs.appendFileSync(new URL("./generated/physics_config.h",import.meta.url),"\n#define SOURCEBOX_PORTAL2_CUBE_MODEL "+JSON.stringify(s.portal2.sharedCubeModel)+"\n");
