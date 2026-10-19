import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const report={generatedAt:new Date().toISOString(),scope:'Native damage callbacks and physics fixtures; not a claim of all weapon types or visual portal previews',health:[],hits:{},ownershipTransfers:[],nativePropTravel:{}};
const portal2Log=fs.readFileSync(path.join(root,'logs/portal2-adapter.log'),'utf8');
const portal2Windows=[];let drop=null;
for(const m of portal2Log.matchAll(/^(\d+) native prop probe (drop|result)(?: (.*))?$/gm)){
 if(m[2]==='drop')drop=Number(m[1]);else if(drop!==null){portal2Windows.push([drop,Number(m[1])]);drop=null;}
}
report.portal2NativePropSamples=[];
for await(const line of readline.createInterface({input:fs.createReadStream(path.join(root,'logs/core-damage-props.jsonl'))})){
 let e;try{e=JSON.parse(line);}catch{continue;}
 if(e.type==='cube_sample'&&e.owner==='portal2'&&portal2Windows.some(([a,b])=>e.state.timestamp>=a&&e.state.timestamp<=b))report.portal2NativePropSamples.push(e);
 if(e.type==='player_health')report.health.push(e);
 if(e.type==='player_hit'){const key=e.from+' -> '+e.to;report.hits[key]=(report.hits[key]??0)+1;}
 if(e.type==='cube_transferred')report.ownershipTransfers.push(e);
}
for(const [game,file,pattern] of [
 ['hl2','../../work/sourcebox-sdk/game/mod_hl2/console.log',/\[Sourcebox\] prop portal .*/g],
 ['tf2','../../work/sourcebox-sdk/game/mod_tf/console.log',/\[Sourcebox\] prop portal .*/g],
 ['cs2','logs/cs2-props-verified.log',/\[Sourcebox\] native cube portal .*/g],
 ['hl1mp','C:/Program Files (x86)/Steam/steamapps/common/Half-Life 1 Source Deathmatch/hl1mp/console.log',/\[Sourcebox\] HL1 native prop portal .*/g],
 ['portal2','logs/portal2-adapter.log',/.*native prop probe (drop|result).*/g]
]){const target=path.isAbsolute(file)?file:path.resolve(root,file);report.nativePropTravel[game]=fs.existsSync(target)?[...fs.readFileSync(target,'utf8').matchAll(pattern)].map(m=>m[0]).slice(-10):[];}
report.lethalNative = [...fs.readFileSync(path.join(root,'logs/native-animation.log'),'utf8').matchAll(/native lethal probe .*/g)].map(m=>m[0]);
report.nativePropTravel.gmodPreviouslyVerified=JSON.parse(fs.readFileSync(path.join(root,'logs/gmod-native-prop-floor-wall.json'),'utf8'));
const gmod='C:/Program Files (x86)/Steam/steamapps/common/GarrysMod/garrysmod/data/sourcebox/prop_portal_result.json';
if(fs.existsSync(gmod))report.nativePropTravel.gmod=JSON.parse(fs.readFileSync(gmod,'utf8'));
const foreign='C:/Program Files (x86)/Steam/steamapps/common/GarrysMod/garrysmod/data/sourcebox/foreign_prop_result.json';
if(fs.existsSync(foreign))report.nativePropTravel.gmodForeignCurrent=JSON.parse(fs.readFileSync(foreign,'utf8'));
report.respawnHealthRestored=Object.fromEntries(['hl2','tf2','hl1mp','gmod','cs2','portal2'].map(game=>[game,report.health.some(e=>e.game===game&&e.before<=20&&e.after>=100)]));
fs.mkdirSync(path.join(root,'evidence'),{recursive:true});
fs.writeFileSync(path.join(root,'evidence/damage-props-native.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({healthTransitions:report.health.length,hits:report.hits,propLogs:Object.fromEntries(Object.entries(report.nativePropTravel).map(([k,v])=>[k,Array.isArray(v)?v.length:v.crossed]))}));
