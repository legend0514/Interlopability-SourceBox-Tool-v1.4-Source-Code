import fs from 'node:fs';
import path from 'node:path';
import {randomUUID} from 'node:crypto';
const content=process.argv[2];
if(!content)throw Error('Provide the existing Sourcebox CS2 addon content directory');
const worlds=JSON.parse(fs.readFileSync(new URL('../../../work/worlds/cs2-content/worlds.json',import.meta.url)));
const base=new URL('../../../work/worlds/base-room.vmap',import.meta.url);
if(!fs.existsSync(base)){
 const original=fs.readFileSync(path.join(content,'maps/sourcebox_room_001.vmap'),'utf8');
 if(original.includes('sourcebox_world_gm_construct'))throw Error('Preserved base room is missing; refusing duplicate world geometry');
 fs.writeFileSync(base,original);
}
let map=fs.readFileSync(base,'utf8');
const mapName=process.argv[3]??'sourcebox_worlds_probe';
if(!['sourcebox_worlds_probe','sourcebox_room_001'].includes(mapName))throw Error('Unsupported native map output');
const world=map.indexOf('"world" "CMapWorld"');
const children=map.indexOf('[',map.indexOf('"children" "element_array"',world));
if(world<0||children<0)throw Error('Native map structure changed');
const entities=worlds.map((w,i)=>`"CMapEntity" {
 "id" "elementid" "${randomUUID()}"
 "nodeID" "int" "${10000+i}"
 "referenceID" "uint64" "0x0"
 "children" "element_array" []
 "entity_properties" "EditGameClassProps" {
  "id" "elementid" "${randomUUID()}"
  "classname" "string" "prop_static"
  "targetname" "string" "sourcebox_world_${w.id}"
  "model" "string" "${w.model}"
  "solid" "string" "6"
  "baketoworld" "string" "1"
  "bakelightdoublesided" "string" "1"
  "lightmapscalebias" "string" "-2"
 }
 "origin" "vector3" "0 0 0"
 "angles" "qangle" "0 0 0"
 "scales" "vector3" "1 1 1"
 "force_hidden" "bool" "0"
 "editorOnly" "bool" "0"
}`);
const lights=JSON.parse(fs.readFileSync(new URL('../../../work/worlds/cs2-content/lights.json',import.meta.url)));
function entity(node,origin,properties,angles='0 0 0'){
 return `"CMapEntity" { "id" "elementid" "${randomUUID()}" "nodeID" "int" "${node}" "referenceID" "uint64" "0x0" "children" "element_array" []
 "entity_properties" "EditGameClassProps" { "id" "elementid" "${randomUUID()}" ${Object.entries(properties).map(([k,v])=>`"${k}" "string" "${v}"`).join('\n')} }
 "origin" "vector3" "${origin.join(' ')}" "angles" "qangle" "${angles}" "scales" "vector3" "1 1 1" }`;
}
const skyPrefab='maps/sourcebox_world_skies.vmap';
if(!fs.existsSync(path.join(content,skyPrefab)))throw Error('Prepare native sky enclosures before compiling destination worlds');
entities.push(`"CMapPrefab" { "id" "elementid" "${randomUUID()}" "nodeID" "int" "12003" "referenceID" "uint64" "0x0" "children" "element_array" [] "origin" "vector3" "0 0 0" "angles" "qangle" "0 0 0" "scales" "vector3" "1 1 1" "targetMapPath" "string" "${skyPrefab}" "fixupEntityNames" "bool" "0" "force_hidden" "bool" "0" "editorOnly" "bool" "0" }`);
entities.push(entity(12000,[0,0,0],{classname:'env_sky',targetname:'sourcebox_world_sky',skyname:'materials/skybox/sky_de_dust2.vmat',tint_color:'255 255 255',brightnessscale:'1',StartDisabled:'0'}));
entities.push(entity(12004,[0,0,1024],{classname:'light_environment',targetname:'sourcebox_world_daylight',enabled:'1',color:'255 244 230',brightness:'1.5',directlight:'3',castshadows:'1',skytexture:'sourcebox_world_sky',skytexturescale:'0.65',skybouncescale:'1',bouncescale:'1'},'45 135 0'));
entities.push(entity(12002,[0,0,32],{classname:'info_target',targetname:'sourcebox_worlds_ready'}));
const sceneryFile=new URL('../../../work/worlds/scenery.json',import.meta.url);
const assetsFile=new URL('../sheets/cs2-world-scenery-assets.json',import.meta.url);
const sceneryReport={placed:0,sharedPhysics:[],unsupportedEffects:[],missing:[]};
if(fs.existsSync(sceneryFile)){
 const assets=JSON.parse(fs.readFileSync(assetsFile));let node=20000;
 for(const world of JSON.parse(fs.readFileSync(sceneryFile)))for(const p of world.instances){
  if(p.kind.startsWith('prop_physics')){sceneryReport.sharedPhysics.push(p);continue;}
  if(/light_cone|lightcone/.test(p.model)){sceneryReport.unsupportedEffects.push(p.id);continue;}
  const model=assets[p.model];
  if(!model){sceneryReport.missing.push({id:p.id,model:p.model});continue;}
  if(!p.position.every(Number.isFinite)||!p.rotation.every(Number.isFinite)||!Number.isFinite(p.scale)||p.scale<=0||p.scale>16)throw Error('Invalid scenery transform: '+p.id);
  const props={classname:p.kind==='static'?'prop_static':'prop_dynamic',targetname:'sourcebox_arch_'+p.id.replaceAll(':','_'),model,skin:String(p.skin),solid:p.solid?'6':'0',disableshadows:'0',renderamt:'255'};
  if(p.kind==='static'){props.baketoworld='1';props.lightmapscalebias='-2';}
  if(p.animation)props.DefaultAnim=p.animation;
  entities.push(entity(node++,p.position,props,p.rotation.join(' ')).replace('"scales" "vector3" "1 1 1"',`"scales" "vector3" "${p.scale} ${p.scale} ${p.scale}"`));sceneryReport.placed++;
 }
 fs.writeFileSync(new URL('../../../work/worlds/scenery-map-report.json',import.meta.url),JSON.stringify(sceneryReport,null,2));
 if(sceneryReport.missing.length)throw Error(`${sceneryReport.missing.length} scenery instances lack converted models; inspect scenery-map-report.json`);
}
// Native sky enclosures separate stacked worlds. Use shadowed daylight and
// baked sky illumination; retain local interior and arrival fill lights.
for(const [i,w] of worlds.entries()){
 const center=w.bounds[0].map((n,a)=>(n+w.bounds[1][a])/2);
 const half=w.bounds[0].map((n,a)=>(w.bounds[1][a]-n)/2+128);
 entities.push(entity(12100+i,center,{classname:'env_combined_light_probe_volume',targetname:'sourcebox_world_lpv_'+w.id,box_mins:half.map(n=>-n).join(' '),box_maxs:half.join(' '),voxel_size:'512',flood_fill:'1',voxelize:'1'}));
}
for(const [i,l] of lights.entries()){
 if(!l.origin.every(Number.isFinite)||l.origin.length!==3||!Number.isFinite(l.range))throw Error('Invalid native light');
 entities.push(`"CMapEntity" {
 "id" "elementid" "${randomUUID()}" "nodeID" "int" "${11000+i}" "referenceID" "uint64" "0x0"
 "children" "element_array" []
 "entity_properties" "EditGameClassProps" {
 "id" "elementid" "${randomUUID()}" "classname" "string" "light_omni2"
 "enabled" "string" "1" "directlight" "string" "1" "bouncelight" "string" "1"
 "castshadows" "string" "2" "colormode" "string" "0"
 "color" "string" "${l.color.join(' ')}" "brightness_units" "string" "0"
 "brightness" "string" "${l.brightness}" "range" "string" "${l.range}" }
 "origin" "vector3" "${l.origin.join(' ')}" "angles" "qangle" "0 0 0" "scales" "vector3" "1 1 1" }`);
}
map=map.slice(0,children+1)+'\n'+entities.join(',\n')+',\n'+map.slice(children+1);
fs.writeFileSync(path.join(content,'maps/'+mapName+'.vmap'),map);
console.log('Created separate native probe map with five static world collision models.');
