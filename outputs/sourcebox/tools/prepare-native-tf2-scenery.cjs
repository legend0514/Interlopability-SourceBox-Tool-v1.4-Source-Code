// Extract installed TF2 scenery dependencies, including runtime-visibility props. Local only.
const fs=require('fs'),path=require('path');
const {vpk,mdl}=require('./installed-assets.cjs');
const [gameRoot,mapFile,dest]=process.argv.slice(2);
if(!gameRoot||!mapFile||!dest||![gameRoot,mapFile,dest].every(path.isAbsolute))throw Error('Expected absolute TF2 root, BSP and local cache paths');
const files=new Map(['tf2_misc','tf2_textures'].flatMap(n=>[...vpk(path.join(gameRoot,'tf',n+'_dir.vpk'))]));
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

const b=fs.readFileSync(mapFile),lump=b.readInt32LE(8+35*16),count=b.readInt32LE(lump),names=new Set();
for(let i=0;i<count;i++){const h=lump+4+i*16;if(b.readUInt32LE(h)!==0x73707270)continue;if(b.readUInt16LE(h+4)&1)throw Error('Compressed host props unsupported');const off=b.readInt32LE(h+8),n=b.readInt32LE(off);for(let j=0;j<n;j++)names.add(b.toString('ascii',off+4+j*128,off+4+(j+1)*128).split('\0')[0]);}
for(const n of names)if(files.has(n))model(n);
for(const water of ['water/water_2fort','water/water_2fort_beneath','water/water_2fort_skybox'])material(water);
fs.writeFileSync(path.join(dest,'tf2-scenery-audit.json'),JSON.stringify({localOnly:true,models:[...models],files:copied.size},null,2));
console.log(JSON.stringify({models:models.size,files:copied.size}));
