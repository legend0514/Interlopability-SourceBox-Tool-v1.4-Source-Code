import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const project=fileURLToPath(new URL('../',import.meta.url));
const workspace=path.resolve(project,'../..');
const [game,gameDir]=process.argv.slice(2);
if(!['hl1mp','portal2'].includes(game)||!gameDir)throw Error('Expected native game and game directory');
const hosts=JSON.parse(fs.readFileSync(path.join(project,'sheets/native-world-hosts.json')));
const host=hosts[game];
if(!host?.enabled){console.log(`${game}: baseline room retained`);process.exit(0);}
if(!fs.existsSync(host.map))throw Error('Missing compiled destination host');
// The compiled host includes TF2 scenery omitted by runtime visibility filters. Prepare dependencies
// from the owner's installed files, never from a distributable asset bundle.
const tfRoot=path.join(path.dirname(path.dirname(gameDir)),'Team Fortress 2');
if(fs.existsSync(path.join(tfRoot,'tf/tf2_misc_dir.vpk'))){
 execFileSync(process.execPath,[path.join(project,'tools/prepare-native-tf2-scenery.cjs'),tfRoot,host.map,path.join(workspace,'work/runtime-assets/worlds-source1')],{stdio:'inherit'});
}

const infoPath=path.join(gameDir,'gameinfo.txt');
let info=fs.readFileSync(infoPath,'utf8');
const roots=['work/runtime-assets/worlds-source1','work/worlds/assets','work/worlds/scenery/source'].map(p=>path.join(workspace,p));
for(const root of roots)if(!fs.existsSync(root))throw Error('Missing destination assets: '+root);
const backup=path.join(project,'logs',game+'-world-gameinfo-original.txt');
if(!fs.existsSync(backup))fs.copyFileSync(infoPath,backup);
info=info.replace(/\s*\/\/ sourcebox-worlds-begin[\s\S]*?\/\/ sourcebox-worlds-end/g,'');
const mount='\n// sourcebox-worlds-begin\n'+roots.map(p=>'Game "'+p.replaceAll('\\','/')+'"').join('\n')+'\n// sourcebox-worlds-end\n';
const anchor=/((?:Game|mod\+mod_write\+default_write_path)\s+\|gameinfo_path\|\.)/i;
if(!anchor.test(info))throw Error('Native game search path anchor not found');
info=info.replace(anchor,'$1'+mount);
if(game==='hl1mp'){
 const compatibility=path.join(workspace,'work/runtime-assets/worlds-hl1-materials');
 if(!fs.existsSync(path.join(compatibility,'materials')))throw Error('Prepare HL1 world material compatibility before installing the map');
 info=info.replace(/\s*\/\/ sourcebox-legacy-materials-begin[\s\S]*?\/\/ sourcebox-legacy-materials-end/g,'');
 info=info.replace(/(SearchPaths\s*\{)/i,'$1\n// sourcebox-legacy-materials-begin\nGame "'+compatibility.replaceAll('\\','/')+'"\n// sourcebox-legacy-materials-end\n');
}
fs.writeFileSync(infoPath,info);
let bsp=fs.readFileSync(host.map);
if(game==='portal2'){
 const offset=bsp.readInt32LE(8),length=bsp.readInt32LE(12);
 if(offset<0||length<=0||offset+length>bsp.length)throw Error('Invalid native host entity lump');
 let entities=bsp.toString('utf8',offset,offset+length).replace(/\0+$/,'');
 let changed=false;
 entities=entities.replace(/\{[^{}]*"classname"\s*"worldspawn"[^{}]*\}/g,block=>{
  if(!/"skyname"\s*"sky_day01_01"/.test(block))return block;
  changed=true;return block.replace(/"skyname"\s*"sky_day01_01"/,'"skyname" "sky_l4d_c4m1_hdr"');
 });
 if(changed){const data=Buffer.from(entities+'\0');bsp.writeInt32LE(bsp.length,8);bsp.writeInt32LE(data.length,12);bsp=Buffer.concat([bsp,data]);}
}
if(game==='hl1mp'){
 const spawn=JSON.parse(fs.readFileSync(path.join(project,'sheets/hl1mp.json'))).spawn;
 const offset=bsp.readInt32LE(8),length=bsp.readInt32LE(12);
 let entities=bsp.toString('utf8',offset,offset+length).replace(/\0+$/,'');
 entities=entities.replace(/\{[^{}]*"classname"\s*"info_player_deathmatch"[^{}]*\}/g,b=>b.replace(/"origin"\s*"[^"]*"/,`"origin" "${spawn.join(' ')}"`));
 entities=entities.replace(/\{[^{}]*"classname"\s*"prop_physics"[^{}]*\}/g,'');
 const data=Buffer.from(entities+'\0');bsp.writeInt32LE(bsp.length,8);bsp.writeInt32LE(data.length,12);bsp=Buffer.concat([bsp,data]);
}
fs.writeFileSync(path.join(gameDir,'maps/sourcebox_room_001.bsp'),bsp);
console.log(`${game}: installed six-destination host and isolated model/material mounts`);
