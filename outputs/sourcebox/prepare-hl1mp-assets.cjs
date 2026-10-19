// Local runtime cache only. Never distribute extracted Valve content.
const fs=require('fs'),path=require('path');
const {vpk,mdl}=require('./tools/installed-assets.cjs');
const common=process.argv[2]||'C:/Program Files (x86)/Steam/steamapps/common';
const dest=path.resolve(__dirname,'../../work/runtime-assets/hl1mp');
const files=new Map([...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl2/hl2_misc_dir.vpk')),...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl2/hl2_textures_dir.vpk')),...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl1/hl1_pak_dir.vpk')),...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl1mp/hl1mp_pak_dir.vpk'))]);
const copied=new Set(),models=new Set(),materials=new Set();
function copy(n){n=n.toLowerCase().replaceAll('\\','/');if(!/^(models|materials)\/[a-z0-9_./-]+$/.test(n)||n.split('/').includes('..'))throw Error('Unsafe asset path');if(copied.has(n))return true;const read=files.get(n);if(!read)return false;const out=path.join(dest,n);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,read());copied.add(n);return true;}
function material(n){n=n.toLowerCase().replaceAll('\\','/').replace(/^materials\//,'').replace(/\.vmt$/,'');if(materials.has(n))return;materials.add(n);const key='materials/'+n+'.vmt';if(!copy(key))return;const text=files.get(key)().toString('utf8');for(const match of text.matchAll(/"([^"\r\n]+)"/g)){const value=match[1].toLowerCase().replaceAll('\\','/').replace(/^materials\//,'');if(!/^[a-z0-9_./-]+$/.test(value)||value.includes('..'))continue;const base=value.replace(/\.(vmt|vtf)$/,'');if(files.has('materials/'+base+'.vmt'))material(base);if(files.has('materials/'+base+'.vtf'))copy('materials/'+base+'.vtf');}}
function model(n){
 n=n.toLowerCase();if(models.has(n))return;models.add(n);if(!copy(n))throw Error('Missing model '+n);
 const b=files.get(n)(),m=mdl(b),base=n.slice(0,-4);
 for(const suffix of ['.phy','.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx','.ani'])copy(base+suffix);
 for(const include of m.includes){if(!files.has(include)&&['models/m_gst.mdl','models/m_pst.mdl','models/m_shd.mdl','models/m_ss.mdl'].includes(include)){console.log('Legacy optional animation absent from current GMod: '+include);continue;}model(include);}
 const str=o=>b.toString('utf8',o,b.indexOf(0,o));
 const dirs=Array.from({length:b.readInt32LE(212)},(_,i)=>str(b.readInt32LE(b.readInt32LE(216)+i*4)).replaceAll('\\','/'));
 const textures=Array.from({length:b.readInt32LE(204)},(_,i)=>{const o=b.readInt32LE(208)+i*64;return str(o+b.readInt32LE(o)).replaceAll('\\','/');});
 // Alternate skins can use bare names relative to a fully qualified base material.
 for(const name of textures)if(name.includes('/'))dirs.push(name.slice(0,name.lastIndexOf('/')+1));
 for(const name of textures)for(const dir of new Set(['',...dirs]))if(files.has('materials/'+(dir+name).toLowerCase()+'.vmt'))material(dir+name);
}
const config=JSON.parse(fs.readFileSync(path.join(__dirname,'sheets/hl1mp.json')));for(const m of Object.keys(config.models))model(m);for(const w of Object.values(config.weapons))if(files.has(w.world.toLowerCase()))model(w.world);
// NPCs must use HL2 assets, not the same-named HL1 models in hl1_pak.
for(const [n,read] of new Map([...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl2/hl2_misc_dir.vpk')),...vpk(path.join(common,'Half-Life 1 Source Deathmatch/hl2/hl2_textures_dir.vpk'))]))files.set(n,read);
copied.clear();models.clear();materials.clear();
const scene=JSON.parse(fs.readFileSync(path.join(__dirname,'sheets/gmod-scene.json')));
if(scene.addon){
 for(const [n,read] of vpk(path.join(common,'GarrysMod/garrysmod/garrysmod_dir.vpk')))if(!files.has(n))files.set(n,read);
 function addLocal(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const full=path.join(dir,entry.name);if(entry.isDirectory())addLocal(full);else{const n=path.relative(scene.addon.source,full).replaceAll('\\','/').toLowerCase();if(n.startsWith('models/')||n.startsWith('materials/'))files.set(n,()=>fs.readFileSync(full));}}}
 addLocal(scene.addon.source);
 for(const n of scene.addon.playerModels)model(n);
}
for(const n of [...scene.npcModels,...(scene.addon?.npcModels??[])]){
 model(n);
 for(const suffix of ['.mdl','.phy','.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx','.ani']){const read=files.get(n.slice(0,-4)+suffix);if(read){const out=path.join(dest,'models/sourcebox/npc',n.slice(7,-4)+suffix);fs.mkdirSync(path.dirname(out),{recursive:true});const data=read();if(suffix==='.mdl'){const name='sourcebox/npc/'+n.slice(7);if(Buffer.byteLength(name,'ascii')>=64)throw Error('Model alias exceeds studio header: '+name);data.fill(0,12,76);data.write(name,12,'ascii');}fs.writeFileSync(out,data);}}
}
// Preserve the installed HL2 stock proxy under a unique path in HL1MP.
const hl2=vpk(path.join(common,'Team Fortress 2/hl2/hl2_misc_dir.vpk'));
for(const suffix of ['.mdl','.vvd','.dx90.vtx','.phy']){const read=hl2.get('models/player'+suffix);if(read){const out=path.join(dest,'models/sourcebox/hl2/player'+suffix);fs.mkdirSync(path.dirname(out),{recursive:true});const data=read();if(suffix==='.mdl'){data.fill(0,12,76);data.write('sourcebox/hl2/player.mdl',12,'ascii');}fs.writeFileSync(out,data);}}
fs.writeFileSync(path.join(dest,'LOCAL-ONLY.txt'),'Installed Valve asset cache. Never redistribute.');console.log('Prepared '+copied.size+' local HL1MP asset dependencies');

// Local-only GMod Counter-Strike team assets.
for(const [n,read] of vpk(path.join(common,'GarrysMod/garrysmod/garrysmod_dir.vpk')))files.set(n,read);
const cs2=JSON.parse(fs.readFileSync(path.join(__dirname,'sheets/cs2.json')));
for(const team of Object.values(cs2.source1Teams))model(team.model);

// Explicit stock GMod player dependencies shared with every Source 1 receiver.
model("models/player/kleiner.mdl");

