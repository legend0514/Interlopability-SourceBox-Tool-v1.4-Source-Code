import fs from 'node:fs';
import {preflight} from './preflight.mjs';
const config=preflight();
const r=JSON.parse(fs.readFileSync(new URL('./sheets/room.json',import.meta.url)));
if(!r.bounds.flat().every(Number.isFinite)||r.thickness<=0)throw Error('Invalid room');
let id=1;const kv=o=>Object.entries(o).map(([k,v])=>`"${k}" "${v}"`).join('\n');
function block(type,o,tail=''){return `${type}\n{\n${kv(o)}\n${tail}\n}\n`;}
function box(a,b,mat){let[x,y,z]=a,[X,Y,Z]=b;
 const faces=[[[x,y,z],[x,y,Z],[x,Y,Z]],[[X,Y,z],[X,Y,Z],[X,y,Z]],[[X,y,z],[X,y,Z],[x,y,Z]],[[x,Y,z],[x,Y,Z],[X,Y,Z]],[[x,Y,z],[X,Y,z],[X,y,z]],[[x,y,Z],[X,y,Z],[X,Y,Z]]];
 return block('solid',{id:id++},faces.map((f,i)=>block('side',{id:id++,plane:[...f].reverse().map(v=>'('+v.join(' ')+')').join(' '),material:mat,uaxis:i<2?'[0 1 0 0] 0.25':'[1 0 0 0] 0.25',vaxis:i<4?'[0 0 -1 0] 0.25':'[0 -1 0 0] 0.25',rotation:0,lightmapscale:16,smoothing_groups:0})).join(''));
}
for(const game of ['hl2','tf2','cs2','portal2']){
 id=1;const [a,b]=r.bounds,[x,y,z]=a,[X,Y,Z]=b,t=r.thickness;
 const solids=[box([x-t,y-t,z-t],[X+t,Y+t,z],r.floorMaterial),box([x-t,y-t,Z],[X+t,Y+t,Z+t],r.wallMaterial),box([x-t,y-t,z],[x,Y+t,Z],r.wallMaterial),box([X,y-t,z],[X+t,Y+t,Z],r.wallMaterial),box([x,y-t,z],[X,y,Z],r.wallMaterial),box([x,Y,z],[X,Y+t,Z],r.wallMaterial)];
 let text=block('versioninfo',{editorversion:400,editorbuild:1,mapversion:1,formatversion:100,prefab:0})+block('world',{id:id++,mapversion:1,classname:'worldspawn',skyname:'sky_day01_01'},solids.join(''));
 const ent=o=>{text+=block('entity',{id:id++,...o});};
 ent({classname:'light',origin:'0 0 220',_light:'255 245 230 700'});
 if(game==='hl2'){
 ent({classname:'info_player_start',origin:r.hl2Spawn.join(' '),angles:'0 0 0'});
 ent({classname:'point_clientcommand',targetname:'sourcebox_loadout',origin:'0 0 64'});
 text+=block('entity',{id:id++,classname:'logic_auto',origin:'0 0 64',spawnflags:1},block('connections',{OnMapSpawn:'sourcebox_loadout,Command,exec sourcebox_loadout.cfg,1,1'}));
 }
 else if(game==='portal2'){ent({classname:'info_player_start',origin:'0 320 8',angles:'0 -90 0'});ent({classname:'weapon_portalgun',origin:'0 320 32',CanFirePortal1:1,CanFirePortal2:1});}
 else if(game==='cs2'){ent({classname:'info_player_terrorist',origin:'320 -128 8',angles:'0 180 0'});ent({classname:'info_player_counterterrorist',origin:'320 128 8',angles:'0 180 0'});}
 else{ent({classname:'info_player_teamspawn',origin:r.tf2Spawn.join(' '),angles:'0 180 0',TeamNum:3,StartDisabled:0});ent({classname:'info_player_teamspawn',origin:'128 128 8',angles:'0 180 0',TeamNum:2,StartDisabled:0});}
 for(const p of config.physics.objects)if(p.owner===game)ent({classname:'prop_physics',targetname:p.id,model:p.model,origin:p.position.join(' '),angles:p.rotation.join(' '),spawnflags:256});
 if(game==='hl2'||game==='tf2'){ent({classname:'point_servercommand',targetname:'sourcebox_ammo',origin:'0 0 64'});
 text+=block('entity',{id:id++,classname:'logic_timer',origin:'0 0 64',RefireTime:config.physics.ammo.interval,StartDisabled:0},block('connections',{OnTimer:'sourcebox_ammo,Command,'+config.physics.ammo.command+',0,-1'}));
 }
 fs.mkdirSync(new URL('./maps/'+game+'/',import.meta.url),{recursive:true});fs.writeFileSync(new URL('./maps/'+game+'/'+r.name+'.vmf',import.meta.url),text);
}
console.log('Generated Source rooms and CS2 import-source geometry from room.json (CS2 VMF requires Source 2 import and compilation)');

