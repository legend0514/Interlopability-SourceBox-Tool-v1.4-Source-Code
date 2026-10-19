// Extract only required model/material dependencies from the recipient's TF2.
// Outputs are a local cache and must never be placed in a release archive.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {vpk,mdl}=require('./installed-assets.cjs');
function prepare({tf2Root,cacheRoot,sheetPath}){
 for(const p of [tf2Root,cacheRoot,sheetPath])if(typeof p!=='string'||!path.isAbsolute(p))throw Error('Setup paths must be absolute');
 const config=JSON.parse(fs.readFileSync(sheetPath,'utf8').replace(/^\uFEFF/,''));
 const archives=['tf2_misc_dir.vpk','tf2_textures_dir.vpk'].map(n=>path.join(tf2Root,'tf',n));
 const files=new Map(archives.flatMap(p=>[...vpk(p)]));
 const copied=new Set(),models=new Set(),materials=new Set(),missing=new Set();
 const root=path.resolve(cacheRoot);
 function safe(n){n=n.toLowerCase().replaceAll('\\','/');if(!/^(models|materials)\/[a-z0-9_./-]+$/.test(n)||n.split('/').includes('..'))throw Error('Invalid installed asset reference: '+n);return n;}
 function copy(n,required=false){
  n=safe(n);if(copied.has(n))return true;const read=files.get(n);
  if(!read){if(required)missing.add(n);return false;}
  const out=path.join(root,n);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,read());copied.add(n);return true;
 }
 function material(n){
  n=n.toLowerCase().replaceAll('\\','/').replace(/^materials\//,'').replace(/\.vmt$/,'');
  if(materials.has(n))return;materials.add(n);const key='materials/'+n+'.vmt';
  if(!copy(key,true))return;const text=files.get(key)().toString('utf8');
  for(const match of text.matchAll(/"([^"\r\n]+)"/g)){
   const value=match[1].toLowerCase().replaceAll('\\','/').replace(/^materials\//,'').replace(/\.(vmt|vtf)$/,'');
   if(!/^[a-z0-9_./-]+$/.test(value)||value.split('/').includes('..'))continue;
   if(files.has('materials/'+value+'.vmt'))material(value);
   if(files.has('materials/'+value+'.vtf'))copy('materials/'+value+'.vtf');
  }
  const texture=text.match(/"\$basetexture"\s*"([^"\r\n]+)"/i)?.[1]||text.match(/"\$iris"\s*"([^"\r\n]+)"/i)?.[1];
  if(texture){copy('materials/'+texture.replace(/\.vtf$/i,'')+'.vtf',true);fs.writeFileSync(path.join(root,key),'"VertexLitGeneric"\n{\n "$basetexture" "'+texture+'"\n "$halflambert" "1"\n "$phong" "0"\n "$color" "[1 1 1]"\n}\n');}
 }
 function model(n){
  n=safe(n);if(models.has(n))return;models.add(n);if(!copy(n,true))return;
  const b=files.get(n)();if(b.length<344||b.toString('ascii',0,4)!=='IDST')throw Error('Invalid studio model: '+n);
  const version=b.readInt32LE(4);if(version<46||version>48)throw Error('Model needs conversion before deployment: '+n+' (version '+version+')');
  for(const suffix of ['.phy','.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx','.ani'])copy(n.slice(0,-4)+suffix);
  for(const include of mdl(b).includes)model(include);
  const str=o=>{if(o<0||o>=b.length)throw Error('Invalid model string');const end=b.indexOf(0,o);if(end<0)throw Error('Unterminated model string');return b.toString('utf8',o,end).replaceAll('\\','/');};
  const dirs=Array.from({length:b.readInt32LE(212)},(_,i)=>str(b.readInt32LE(b.readInt32LE(216)+i*4)));
  const textures=Array.from({length:b.readInt32LE(204)},(_,i)=>{const o=b.readInt32LE(208)+i*64;return str(o+b.readInt32LE(o));});
  for(const name of textures)if(name.includes('/'))dirs.push(name.slice(0,name.lastIndexOf('/')+1));
  for(const name of textures){let found=false;for(const dir of new Set(['',...dirs])){const candidate=path.posix.normalize(dir.replace(/^\/+|\/+$/g,'')+'/'+name.replace(/^\/+/,'')).replace(/^\/+/,'').toLowerCase();if(!candidate.split('/').includes('..')&&files.has('materials/'+candidate+'.vmt')){material(candidate);found=true;}}if(!found)missing.add('material for '+n+': '+name);}
 }
 fs.mkdirSync(root,{recursive:true});
 for(const c of Object.values(config.tf2Classes??{}))model(c.model);
 for(const w of Object.values(config.weapons??{}))model(w.model);
 if(missing.size)throw Error('Installed dependencies missing:\n'+[...missing].join('\n'));
 const manifest={localOnly:true,redistribute:false,provider:'tf2',archives:archives.map(file=>({name:path.basename(file),sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')})),files:[...copied].sort().map(name=>({name,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,name))).digest('hex')}))};
 fs.writeFileSync(path.join(root,'local-assets.json'),JSON.stringify(manifest,null,2));
 return {models:models.size,materials:materials.size,files:copied.size};
}
module.exports={prepare};
if(require.main===module){const request=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));console.log(JSON.stringify(prepare(request)));}

