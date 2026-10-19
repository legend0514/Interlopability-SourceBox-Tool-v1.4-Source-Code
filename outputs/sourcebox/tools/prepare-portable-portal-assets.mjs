import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
import crypto from 'node:crypto';
const {mdl}=createRequire(import.meta.url)('./installed-assets.cjs');
const request=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
const source=request.addonRoot,cache=request.cacheRoot,backup=path.join(cache,'before-portal-gun');
for(const p of [source,cache])if(typeof p!=='string'||!path.isAbsolute(p))throw Error('Absolute setup paths required');
const manifest={workshopId:'1800764828',localOnly:true,files:[],models:{}};
function save(relative,bytes){
 const dest=path.join(cache,relative),previous=path.join(backup,relative);
 if(fs.existsSync(dest)&&!fs.existsSync(previous)){fs.mkdirSync(path.dirname(previous),{recursive:true});fs.copyFileSync(dest,previous);}
 fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,bytes);
 manifest.files.push({path:relative,sha256:crypto.createHash('sha256').update(bytes).digest('hex')});
}
for(const [original,alias] of [['c_portalgun','v_portalgun'],['w_portalgun','w_portalgun']]){
 const basename='models/weapons/'+original;
 const data=fs.readFileSync(path.join(source,basename+'.mdl')),info=mdl(data);
 if(info.version!==48||info.includes.length)throw Error('Unsupported dependency model format or external animation dependency');
 const target='models/sourcebox/portal2/'+alias;
 data.fill(0,12,76);data.write(target.slice(7)+'.mdl',12,'ascii');
 save(target+'.mdl',data);
 for(const suffix of ['.vvd','.dx90.vtx','.dx80.vtx','.phy']){
  const input=path.join(source,basename+suffix);if(fs.existsSync(input))save(target+suffix,fs.readFileSync(input));
 }
 manifest.models[alias]={source:basename+'.mdl',version:info.version,sequences:info.sequences};
}
for(const folder of ['models/weapons/v_models/v_portalgun/p1','models/weapons/w_models/portalgun/p1']){
 for(const name of fs.readdirSync(path.join(source,'materials',folder))){
  const relative='materials/'+folder+'/'+name;let bytes=fs.readFileSync(path.join(source,relative));
  if(name.endsWith('.vmt')){
   // GMod's Lua material proxy is unavailable in the retail native clients.
   let text=bytes.toString('utf8');
   text=text.replace(/"Proxies"\s*\{\s*"PortalWeaponColor"\s*\{[^}]*\}\s*\}/gi,'"$color2" "[1 1 1]"');
   bytes=Buffer.from(text);
  }
  save(relative,bytes);
 }
}
fs.writeFileSync(path.join(cache,'portal-gun-manifest.json'),JSON.stringify(manifest,null,2));
console.log('Prepared local v48 successor gun models and materials; native client visual tests required.');
