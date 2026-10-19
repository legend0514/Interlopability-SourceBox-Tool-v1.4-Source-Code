// Local installed-map conversion. Keep exact surface positions and interiors;
// only CS2's materials are simplified. Never package these extracted assets.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {validateWorldData} from '../world-preflight.mjs';
import assetTools from './installed-assets.cjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const registry=JSON.parse(fs.readFileSync(path.join(root,'sheets/worlds-expanded.json')));
const palette=JSON.parse(fs.readFileSync(path.join(root,'sheets/cs2-world-materials.json')));
const installed=assetTools.vpk('C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive/game/csgo/pak01_dir.vpk');
for(const material of Object.values(palette.materials))if(!installed.has(material+'_c'))throw Error('Missing installed CS2 material: '+material);
for(const rule of palette.rules){new RegExp(rule.pattern);if(!palette.materials[rule.material])throw Error('Unknown material category');}
if(!palette.materials[palette.fallback])throw Error('Missing fallback material');
const output=path.resolve(root,'../../work/worlds/cs2-content');
const header='<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:modeldoc41:version{12fc9d44-453a-4ae4-b4d9-7e2ac0bbd4e0} -->\n';
const report=[];
// Validate every map before emitting any build input.
const worlds=registry.worlds.map(def=>{
 const data=JSON.parse(fs.readFileSync(path.join(root,'worlds',def.id+'.json')));
 const errors=validateWorldData(data,def);
 if(errors.length)throw Error(def.id+': '+errors.join('; '));
 return {def,data};
});
for(const {def,data} of worlds){
 const relative='models/sourcebox/worlds/'+def.id;
 const dir=path.join(output,relative);fs.mkdirSync(dir,{recursive:true});
 // FBX conversion applies the importer basis, verified against compiled PHYS.
 const triangles=data.chunks.reduce((sum,chunk)=>sum+chunk.vertices.length/3,0);
 fs.writeFileSync(path.join(dir,def.id+'.vmdl'),header+`{ rootNode = { _class = "RootNode" children = [
 { _class = "RenderMeshList" children = [{ _class = "RenderMeshFile" name = "${def.id}" filename = "${relative}/mesh.fbx" import_scale = 1 }] },
 { _class = "PhysicsShapeList" children = [{ _class = "PhysicsMeshFile" name = "world_collision" filename = "${relative}/collision.fbx" import_scale = 1 surface_prop = "concrete" collision_prop = "default" }] }
 ] } }\n`);
 const collisionBounds=[[Infinity,Infinity,Infinity],[-Infinity,-Infinity,-Infinity]];
 for(const chunk of data.collision)for(const v of chunk.vertices)for(let a=0;a<3;a++){collisionBounds[0][a]=Math.min(collisionBounds[0][a],v[a]);collisionBounds[1][a]=Math.max(collisionBounds[1][a],v[a]);}
 report.push({id:def.id,model:relative+'/'+def.id+'.vmdl',triangles,bounds:data.bounds,collisionBounds,spawn:def.spawn,yaw:def.yaw});
}
const matdir=path.join(output,'materials/sourcebox/worlds');fs.mkdirSync(matdir,{recursive:true});
for(const [name,tint] of Object.entries({ground:'0.35 0.48 0.25',metal:'0.4 0.45 0.5',concrete:'0.65 0.61 0.53'})){
 fs.writeFileSync(path.join(matdir,name+'.vmat'),`"Layer0" { "Shader" "csgo_unlitgeneric.vfx" "TextureColor" "materials/default/default_color.tga" "g_vColorTint" "[${tint} 1]" "F_RENDER_BACKFACES" "1" }\n`);
}
fs.writeFileSync(path.join(output,'worlds.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({output,worlds:report},null,2));
