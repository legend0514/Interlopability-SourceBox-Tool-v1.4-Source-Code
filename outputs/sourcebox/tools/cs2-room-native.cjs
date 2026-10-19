// Adapt Valve's imported room to installed CS2 materials and a native baked light.
const fs=require('fs'),path=require('path');
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){
 const f=path.join(dir,e.name);if(e.isDirectory()){walk(f);continue;}if(!f.endsWith('.vmap'))continue;
 let t=fs.readFileSync(f,'utf8').replaceAll('materials/dev/dev_measurewall01a.vmat','materials/sourcebox/room/dev_measurewall01a.vmat').replaceAll('materials/dev/dev_measuregeneric01b.vmat','materials/sourcebox/room/dev_measuregeneric01b.vmat').replaceAll('materials/skybox/sky_day01_01_hdr.vmat','materials/skybox/sky_de_dust2.vmat');
 if(f.endsWith('_lighting_prefab.vmap')){
  if(!t.includes('"classname" "string" "env_sky"'))throw Error('Expected imported sky entity');
  const start=t.indexOf('"CMapEntity"'),open=t.indexOf('{',start);let depth=0,end=open;for(;end<t.length;end++){if(t[end]==='{')depth++;if(t[end]==='}'&&!--depth)break;}
  let probe=t.slice(start,end+1).replace(/"elementid" "[^"]+"/g,()=> '"elementid" "'+require('crypto').randomUUID()+'"').replace(/"nodeID" "int" "2"/,'"nodeID" "int" "900"').replace('"classname" "string" "env_sky"','"classname" "string" "env_combined_light_probe_volume"\n"box_mins" "string" "-512 -512 -128"\n"box_maxs" "string" "512 512 128"\n"voxel_size" "string" "48"\n"flood_fill" "string" "1"\n"voxelize" "string" "1"').replace('"origin" "vector3" "0 0 0"','"origin" "vector3" "0 0 128"');
  t=t.slice(0,end+1)+',\n'+probe+t.slice(end+1);
  t=t.replace('"classname" "string" "env_sky"',Object.entries({classname:'light_omni2',enabled:'1',directlight:'1',color:'255 245 230',brightness_units:'0',brightness:'4',range:'1600',bouncelight:'1'}).map(([k,v])=>`"${k}" "string" "${v}"`).join('\n'));
  t=t.replace('"origin" "vector3" "0 0 0"','"origin" "vector3" "0 0 220"');
 }
 fs.writeFileSync(f,t);
}}
walk(path.join(process.argv[2],'maps'));
