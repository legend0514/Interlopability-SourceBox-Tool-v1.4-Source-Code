// Local runtime cache only. Never distribute extracted Valve content.
const fs=require('fs'),path=require('path');
const {vpk,mdl}=require('./tools/installed-assets.cjs');
const common=process.argv[2]||'C:/Program Files (x86)/Steam/steamapps/common';
const dest=path.resolve(__dirname,'../../work/runtime-assets/tf2-soldier');
const files=new Map([...vpk(path.join(common,'Team Fortress 2/tf/tf2_misc_dir.vpk')),...vpk(path.join(common,'Team Fortress 2/tf/tf2_textures_dir.vpk'))]);
const copied=new Set(),models=new Set(),materials=new Set();
function copy(n){n=n.toLowerCase().replaceAll('\\','/');if(!/^(models|materials)\/[a-z0-9_./-]+$/.test(n)||n.split('/').includes('..'))throw Error('Unsafe asset path');if(copied.has(n))return true;const read=files.get(n);if(!read)return false;const out=path.join(dest,n);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,read());copied.add(n);return true;}
function material(n){n=n.toLowerCase().replaceAll('\\','/').replace(/^materials\//,'').replace(/\.vmt$/,'');if(materials.has(n))return;materials.add(n);const key='materials/'+n+'.vmt';if(!copy(key))return;const text=files.get(key)().toString('utf8');for(const match of text.matchAll(/"([^"\r\n]+)"/g)){const value=match[1].toLowerCase().replaceAll('\\','/').replace(/^materials\//,'');if(!/^[a-z0-9_./-]+$/.test(value)||value.includes('..'))continue;const base=value.replace(/\.(vmt|vtf)$/,'');if(files.has('materials/'+base+'.vmt'))material(base);if(files.has('materials/'+base+'.vtf'))copy('materials/'+base+'.vtf');}}
function model(n){
 n=n.toLowerCase();if(models.has(n))return;models.add(n);if(!copy(n))throw Error('Missing model '+n);
 const b=files.get(n)(),m=mdl(b),base=n.slice(0,-4);
 for(const suffix of ['.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx','.ani'])copy(base+suffix);
 for(const include of m.includes)model(include);
 const str=o=>b.toString('utf8',o,b.indexOf(0,o));
 const dirs=Array.from({length:b.readInt32LE(212)},(_,i)=>str(b.readInt32LE(b.readInt32LE(216)+i*4)));
 const textures=Array.from({length:b.readInt32LE(204)},(_,i)=>{const o=b.readInt32LE(208)+i*64;return str(o+b.readInt32LE(o));});
 // Alternate skins can use bare names relative to a fully qualified base material.
 for(const name of textures)if(name.includes('/'))dirs.push(name.slice(0,name.lastIndexOf('/')+1));
 for(const name of textures)for(const dir of new Set(['',...dirs]))if(files.has('materials/'+(dir+name).toLowerCase()+'.vmt'))material(dir+name);
}
const classes=JSON.parse(fs.readFileSync(path.join(__dirname,'sheets/asset-references.json'))).tf2Classes;for(const c of Object.values(classes))model(c.model);const weapons=JSON.parse(fs.readFileSync(path.join(__dirname,'sheets/asset-references.json'))).weapons;for(const w of Object.values(weapons))model(w.model);
fs.writeFileSync(path.join(dest,'LOCAL-ONLY.txt'),'Extracted from this user\'s installed TF2 for local Sourcebox runtime. Do not redistribute.\n');
console.log(`Prepared ${copied.size} model/material files outside the release folder; no game scripts mounted.`);

// HL2 has no TF2 cloak/burn/team material proxies. Generate local compatibility materials.
for(const file of copied)if(file.endsWith('.vmt')){
 const original=files.get(file)().toString('utf8');
 const texture=original.match(/"\$basetexture"\s*"([^"\r\n]+)"/i)?.[1]||original.match(/"\$iris"\s*"([^"\r\n]+)"/i)?.[1];
 if(texture)fs.writeFileSync(path.join(dest,file),'"VertexLitGeneric"\n{\n "$basetexture" "'+texture+'"\n "$halflambert" "1"\n "$phong" "0"\n "$color" "[1 1 1]"\n}\n');
}
