// Local installed assets only. Failed conversions are recorded, never registered as supported.
const fs=require('fs'),path=require('path'),cp=require('child_process');
const project=path.resolve(__dirname,'..'),work=path.resolve(project,'../../work/cs2/catalog');fs.mkdirSync(work,{recursive:true});
const {vpk,mdl}=require('./installed-assets.cjs');const steam='C:/Program Files (x86)/Steam/steamapps/common',root=steam+'/Counter-Strike Global Offensive',bin=root+'/game/bin/win64',content=root+'/content/csgo_addons/sourcebox';
const files=vpk(steam+'/Half-Life 2/hl2/hl2_misc_dir.vpk'),cfg=JSON.parse(fs.readFileSync(project+'/sheets/gmod-scene.json')),weapons=JSON.parse(fs.readFileSync(project+'/sheets/asset-references.json')).hl2Weapons;
const popular=cfg.models.filter(n=>!/(chunk|gib|skybox|destroyed)/.test(n));
const chosen=[...new Set([...Object.values(weapons),...cfg.npcModels,...popular])].filter(n=>files.has(n));
const manifest=project+'/sheets/cs2-scene-assets.json';
const extracted=new Set(),imported=new Set(),results=[],supported=fs.existsSync(manifest)?JSON.parse(fs.readFileSync(manifest)):{};
const previous=fs.existsSync(work+'/results.json')?JSON.parse(fs.readFileSync(work+'/results.json')):[];
const knownFailures=new Map(previous.filter(r=>r.status==='failed').map(r=>[r.model,r]));
function extract(n){if(extracted.has(n)||!files.has(n))return;extracted.add(n);for(const ext of ['.mdl','.vvd','.dx90.vtx','.phy','.ani']){const k=n.slice(0,-4)+ext;if(files.has(k)){const f=work+'/source/'+k;fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,files.get(k)());}}for(const inc of mdl(files.get(n)()).includes)extract(inc);}
function run(exe,args,log,input){const r=cp.spawnSync(bin+'/'+exe,args,{cwd:work,encoding:'utf8',input,timeout:45000,windowsHide:true,maxBuffer:16*1024*1024});fs.writeFileSync(log,(r.stdout||'')+(r.stderr||''));return r.status===0&&!r.error;}
function walk(d,ext){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name),ext):e.name.endsWith(ext)?[path.join(d,e.name)]:[]);}
for(const [index,n] of chosen.entries()){
 try{
 if(supported[n]&&fs.existsSync(root+'/game/csgo_addons/sourcebox/'+supported[n]+'_c')){results.push({model:n,status:'cached',target:supported[n]});continue;}
 if(knownFailures.has(n)&&!process.argv.includes('--retry-failed')){results.push(knownFailures.get(n));continue;}
 extract(n);const base=path.basename(n,'.mdl'),local=work+'/converted/'+n.slice(7,-4);fs.mkdirSync(local,{recursive:true});
 if(!run('cs_mdl_import.exe',['-nop4','-i',work+'/source','-o',local,n],local+'/import.log')||!fs.existsSync(local+'/'+base+'.vmdl'))throw Error('Valve model importer failed');
 const b=files.get(n)(),str=o=>b.toString('utf8',o,b.indexOf(0,o));const dirs=Array.from({length:b.readInt32LE(212)},(_,i)=>str(b.readInt32LE(b.readInt32LE(216)+i*4)));const mats={};
 for(let i=0;i<b.readInt32LE(204);i++){let o=b.readInt32LE(208)+i*64,tex=str(o+b.readInt32LE(o));for(const d of ['',...dirs]){const m=('materials/'+d+tex+'.vmt').replaceAll('\\','/').toLowerCase();if(files.has(m)){mats[path.basename(tex).toLowerCase()+'.vmat']=m.slice(0,-4)+'.vmat';if(!imported.has(m)){run('source1import.exe',['-retail','-nop4','-nop4sync','-src1gameinfodir',steam+'/Half-Life 2/hl2','-s2addon','sourcebox','-game',root+'/game/csgo',m],work+'/material-last.log','y\n');imported.add(m);}break;}}}
 const relative='models/sourcebox/shared/'+n.slice(7,-4),dest=content+'/'+relative;fs.mkdirSync(dest,{recursive:true});fs.cpSync(local,dest,{recursive:true});let v=fs.readFileSync(dest+'/'+base+'.vmdl','utf8').replaceAll('"/'+base+'_refs/','"'+relative+'/'+base+'_refs/').replaceAll('"'+base+'_refs/','"'+relative+'/'+base+'_refs/');fs.writeFileSync(dest+'/'+base+'.vmdl',v);
 for(const f of walk(dest,'dmx').filter(f=>f.includes(path.sep+'mesh'+path.sep))){run('dmxconvert.exe',['-i',f,'-oe','keyvalues2'],work+'/dmx-last.log');let t=fs.readFileSync(f,'utf8').replace(/("mtlName"\s+"string"\s+")([^"/]+\.vmat)/gi,(all,p,m)=>p+(mats[m.toLowerCase()]||m.toLowerCase()));fs.writeFileSync(f,t);}
 const log=local+'/compile.log';if(!run('resourcecompiler.exe',['-retail','-nop4','-game',root+'/game/csgo','-i',dest+'/'+base+'.vmdl'],log)||!/0 failed/.test(fs.readFileSync(log,'utf8'))||/RESOURCE COMPILE ERROR/.test(fs.readFileSync(log,'utf8')))throw Error('Resource compile failed');
 supported[n]=relative+'/'+base+'.vmdl';results.push({model:n,status:'compiled',target:supported[n]});
 }catch(e){results.push({model:n,status:'failed',reason:e.message});}
 fs.writeFileSync(project+'/sheets/cs2-scene-assets.json',JSON.stringify(supported,null,2));fs.writeFileSync(work+'/results.json',JSON.stringify(results,null,2));if(index%10===0)console.log(`${index+1}/${chosen.length}: ${Object.keys(supported).length} compiled`);
}
fs.writeFileSync(work+'/results.json',JSON.stringify(results,null,2));
console.log(JSON.stringify({attempted:chosen.length,compiled:Object.keys(supported).length,failed:results.filter(r=>r.status==='failed').length}));
