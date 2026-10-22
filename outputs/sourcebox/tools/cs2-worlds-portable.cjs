// Adapt locally imported topology to available CS2 materials. Preserve imported
// sunlight and sky, and add a separate lighting probe for each destination.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const content=process.argv[2],worlds=JSON.parse(fs.readFileSync(process.argv[3],'utf8'));
function walk(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){
 const f=path.join(dir,entry.name);if(entry.isDirectory()){walk(f);continue;}if(!f.endsWith('.vmap'))continue;
 let text=fs.readFileSync(f,'utf8').replaceAll('materials/dev/dev_measuregeneric01b.vmat','materials/sourcebox/room/dev_measuregeneric01b.vmat').replaceAll('materials/dev/dev_measurewall01a.vmat','materials/sourcebox/room/dev_measurewall01a.vmat');
 text=text.replace(/materials\/skybox\/sky_day01_01[^"\s]*\.vmat/g,'materials/skybox/sky_de_dust2.vmat');
 if(f.endsWith('_lighting_prefab.vmap')){
  const start=text.indexOf('"CMapEntity"');if(start<0)throw Error('Missing lighting entity template');
  const open=text.indexOf('{',start);let depth=0,end=open;for(;end<text.length;end++){if(text[end]==='{')depth++;if(text[end]==='}'&&!--depth)break;}
  const template=text.slice(start,end+1),probes=[];
  for(const [i,w] of [{bounds:[[-512,-512,0],[512,512,256]]},...worlds].entries()){
   const center=w.bounds[0].map((n,a)=>(n+w.bounds[1][a])/2),half=w.bounds[0].map((n,a)=>(w.bounds[1][a]-n)/2+128);
   if(![...center,...half].every(Number.isFinite))throw Error('Invalid world lighting bounds');
   let probe=template.replace(/"elementid" "[^"]+"/g,()=>`"elementid" "${crypto.randomUUID()}"`).replace(/"nodeID" "int" "[^"]+"/,`"nodeID" "int" "${900+i}"`);
   probe=probe.replace(/"classname" "string" "[^"]+"/,`"classname" "string" "env_combined_light_probe_volume"\n"box_mins" "string" "${half.map(n=>-n).join(' ')}"\n"box_maxs" "string" "${half.join(' ')}"\n"voxel_size" "string" "256"\n"flood_fill" "string" "1"\n"voxelize" "string" "1"`).replace(/"origin" "vector3" "[^"]+"/,`"origin" "vector3" "${center.join(' ')}"`);
   probes.push(probe);
  }
  text=text.slice(0,end+1)+',\n'+probes.join(',\n')+text.slice(end+1);
 }
 fs.writeFileSync(f,text);
}}
walk(path.join(content,'maps'));
