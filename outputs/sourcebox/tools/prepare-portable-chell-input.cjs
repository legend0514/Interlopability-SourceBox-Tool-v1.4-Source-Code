// Local extraction only; the resulting content must never enter a release ZIP.
const fs=require('fs'),path=require('path'),{vpk,mdl}=require('./installed-assets.cjs');
const request=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
if(!path.isAbsolute(request.portalRoot)||!path.isAbsolute(request.cacheRoot))throw Error('Absolute installed paths required');
const files=new Map();
for(const folder of ['portal2','portal2_dlc1','portal2_dlc2']){
 const index=path.join(request.portalRoot,folder,'pak01_dir.vpk');
 if(fs.existsSync(index))for(const item of vpk(index))files.set(...item);
}
const copied=new Set();
function copy(name){
 name=name.toLowerCase().replaceAll('\\','/');
 if(!/^(models|materials)\/[a-z0-9_./-]+$/.test(name)||name.split('/').includes('..'))throw Error('Unsafe asset path');
 if(copied.has(name))return;const read=files.get(name);if(!read)throw Error('Missing Portal 2 dependency: '+name);
 copied.add(name);const destination=path.join(request.cacheRoot,name);fs.mkdirSync(path.dirname(destination),{recursive:true});fs.writeFileSync(destination,read());
}
const models=new Set();
function model(name){if(models.has(name))return;models.add(name);copy(name);const metadata=mdl(files.get(name)());
 for(const suffix of ['.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx','.ani','.phy']){const file=name.slice(0,-4)+suffix;if(files.has(file))copy(file);}
 for(const include of metadata.includes)model(include);
}
model('models/player/chell/player.mdl');
for(const name of files.keys())if(name.startsWith('materials/models/player/chell/')&&/\.(vmt|vtf)$/.test(name))copy(name);
fs.writeFileSync(path.join(request.cacheRoot,'LOCAL-ONLY.txt'),'Extracted from this PC’s installed Portal 2. Do not redistribute.');
console.log('Prepared Chell and '+copied.size+' local dependencies');
