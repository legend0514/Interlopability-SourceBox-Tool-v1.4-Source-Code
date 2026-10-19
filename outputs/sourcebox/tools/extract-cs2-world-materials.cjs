const fs=require('fs'),path=require('path');const {vpk}=require('./installed-assets.cjs');
const project=path.resolve(__dirname,'..'),work=path.resolve(project,'../../work/worlds'),steam='C:/Program Files (x86)/Steam/steamapps/common';
const roots=['Team Fortress 2/tf','GarrysMod/garrysmod','Half-Life 1 Source Deathmatch/hl1','Half-Life 1 Source Deathmatch/hl1mp','Portal 2/portal2','Half-Life 2/hl2'].map(p=>steam+'/'+p);
const providers=roots.map(root=>({root,files:new Map(fs.readdirSync(root).filter(n=>/^(pak01|tf2_misc|tf2_textures|garrysmod|hl1_pak|hl1mp_pak|hl2_misc|hl2_textures)_dir\.vpk$/.test(n)).flatMap(n=>[...vpk(root+'/'+n)]))}));
function read(n){const embedded=work+'/assets/'+n;if(fs.existsSync(embedded))return fs.readFileSync(embedded);for(const p of providers){if(fs.existsSync(p.root+'/'+n))return fs.readFileSync(p.root+'/'+n);if(p.files.has(n))return p.files.get(n)();}return null;}
const materials=new Set();for(const w of JSON.parse(fs.readFileSync(project+'/sheets/worlds-expanded.json')).worlds){for(const c of JSON.parse(fs.readFileSync(project+'/worlds/'+w.id+'.json')).chunks)materials.add(c.material);}
const report=[];
function values(n,visited=new Set()){n=n.replaceAll('\\','/').toLowerCase();
 if(visited.has(n))throw Error('Material include cycle '+n);visited.add(n);
 const b=read('materials/'+n.replace(/\.vmt$/,'')+'.vmt');if(!b)throw Error('Missing material '+n);
 const text=b.toString().replace(/\/\/[^\r\n]*/g,'');const out={};
 const pairs=[...text.matchAll(/(?:"([^"\r\n]+)"|([^\s{}"]+))\s+(?:"([^"\r\n]+)"|([^\s{}"]+))/g)];
 for(const m of pairs){const k=(m[1]||m[2]).toLowerCase(),v=m[3]||m[4];if(k==='include')Object.assign(out,values(v.replace(/^materials\//,''),visited));else out[k]=v;}
 return out;
}
for(const name of materials){
 try{const v=values(name),base=v['$basetexture'];if(!base){report.push({name,status:'no-base-texture'});continue;}
 const n='materials/'+base.replaceAll('\\','/').toLowerCase().replace(/\.vtf$/,'')+'.vtf';const b=read(n);if(!b)throw Error('Missing texture '+n);
 const dest=work+'/source-materials/'+n;fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,b);
 report.push({name,status:'extracted',texture:n,alphaTest:v['$alphatest']==='1',translucent:v['$translucent']==='1',normal:v['$bumpmap']||null});
 }catch(error){report.push({name,status:'failed',error:error.message});}
}
fs.writeFileSync(work+'/source-materials.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report.reduce((a,r)=>(a[r.status]=(a[r.status]||0)+1,a),{})));

