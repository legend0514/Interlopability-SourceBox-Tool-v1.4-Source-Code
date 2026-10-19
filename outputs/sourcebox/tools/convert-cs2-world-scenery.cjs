// Local installed scenery conversion. Independent manifest; never changes shared spawn-menu assets.
const fs=require('fs'),path=require('path'),cp=require('child_process');
const project=path.resolve(__dirname,'..'),work=path.resolve(project,'../../work/worlds/scenery');fs.mkdirSync(work,{recursive:true});
const {vpk,mdl}=require('./installed-assets.cjs');const steam='C:/Program Files (x86)/Steam/steamapps/common',cs=steam+'/Counter-Strike Global Offensive',bin=cs+'/game/bin/win64',content=cs+'/content/csgo_addons/sourcebox';
const catalog=JSON.parse(fs.readFileSync(path.resolve(work,'../scenery.json'))),palette=JSON.parse(fs.readFileSync(project+'/sheets/cs2-world-materials.json'));
const roots=[steam+'/Portal 2/portal2',steam+'/Team Fortress 2/tf',steam+'/GarrysMod/garrysmod',steam+'/Half-Life 2/hl2'];
const providers=roots.map(root=>({root,files:new Map(fs.readdirSync(root).filter(n=>/^(pak01|tf2_misc|garrysmod|hl2_misc)_dir\.vpk$/.test(n)).flatMap(n=>[...vpk(root+'/'+n)]))}));
const embedded=path.resolve(work,'../assets');
function source(n){for(const p of providers){if(fs.existsSync(p.root+'/'+n))return {read:()=>fs.readFileSync(p.root+'/'+n),root:p.root};if(p.files.has(n))return {read:p.files.get(n),root:p.root};}if(fs.existsSync(embedded+'/'+n))return{read:()=>fs.readFileSync(embedded+'/'+n),root:roots[0]};return null;}
const manifest=project+'/sheets/cs2-world-scenery-assets.json';const supported=fs.existsSync(manifest)?JSON.parse(fs.readFileSync(manifest)):{};const results=[];const extracted=new Set();
const chosen=[...new Set([...catalog].sort((a,b)=>(a.id==='portal_triple_laser'?-1:b.id==='portal_triple_laser'?1:0)).flatMap(w=>w.instances.map(p=>p.model)))];
function run(exe,args,log){const r=cp.spawnSync(bin+'/'+exe,args,{cwd:work,encoding:'utf8',timeout:45000,windowsHide:true,maxBuffer:16*1024*1024});fs.writeFileSync(log,(r.stdout||'')+(r.stderr||''));return r.status===0&&!r.error;}
function walk(d,ext){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name),ext):e.name.endsWith(ext)?[path.join(d,e.name)]:[]);}
function extract(n){if(extracted.has(n))return;extracted.add(n);const s=source(n);if(!s)throw Error('Missing installed model '+n);for(const ext of ['.mdl','.vvd','.dx90.vtx','.phy','.ani']){const k=n.slice(0,-4)+ext,v=source(k);if(v){const f=work+'/source/'+k;fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,v.read());}}for(const inc of mdl(s.read()).includes)extract(inc);}
for(const [index,n] of chosen.entries()){
 try{
  if(supported[n]&&fs.existsSync(cs+'/game/csgo_addons/sourcebox/'+supported[n]+'_c')){results.push({model:n,status:'cached'});continue;}
  extract(n);const base=path.basename(n,'.mdl'),local=work+'/converted/'+n.slice(7,-4);fs.mkdirSync(local,{recursive:true});
  if(!run('cs_mdl_import.exe',['-nop4','-i',work+'/source','-o',local,n],local+'/import.log')||!fs.existsSync(local+'/'+base+'.vmdl'))throw Error('Valve model import failed');
  const relative='models/sourcebox/scenery/'+n.slice(7,-4),dest=content+'/'+relative;fs.mkdirSync(dest,{recursive:true});fs.cpSync(local,dest,{recursive:true});
  let v=fs.readFileSync(dest+'/'+base+'.vmdl','utf8').replaceAll('"/'+base+'_refs/','"'+relative+'/'+base+'_refs/').replaceAll('"'+base+'_refs/','"'+relative+'/'+base+'_refs/');fs.writeFileSync(dest+'/'+base+'.vmdl',v);
  for(const f of walk(dest,'dmx').filter(f=>f.includes(path.sep+'mesh'+path.sep))){if(!run('dmxconvert.exe',['-i',f,'-oe','keyvalues2'],local+'/dmx.log'))throw Error('Mesh conversion failed');let t=fs.readFileSync(f,'utf8').replace(/("mtlName"\s+"string"\s+")([^"]+)(")/gi,(all,p,m,end)=>{const category=palette.rules.find(r=>new RegExp(r.pattern,'i').test(m+' '+n))?.material??palette.fallback;return p+palette.materials[category]+end;});fs.writeFileSync(f,t);}
  const log=local+'/compile.log';if(!run('resourcecompiler.exe',['-retail','-nop4','-game',cs+'/game/csgo','-i',dest+'/'+base+'.vmdl'],log)||!/0 failed/.test(fs.readFileSync(log,'utf8'))||/RESOURCE COMPILE ERROR|referencing missing material/.test(fs.readFileSync(log,'utf8')))throw Error('Native resource compile failed');
  supported[n]=relative+'/'+base+'.vmdl';results.push({model:n,status:'compiled',target:supported[n]});
 }catch(e){results.push({model:n,status:'failed',reason:e.message});}
 fs.writeFileSync(manifest,JSON.stringify(supported,null,2));fs.writeFileSync(work+'/results.json',JSON.stringify(results,null,2));console.log(`${index+1}/${chosen.length} ${results.at(-1).status} ${n}`);
}
fs.writeFileSync(work+'/results.json',JSON.stringify(results,null,2));console.log(JSON.stringify({attempted:chosen.length,supported:Object.keys(supported).length,failed:results.filter(r=>r.status==='failed').length}));
