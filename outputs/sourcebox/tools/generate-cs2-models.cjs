const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '..');
const models = JSON.parse(fs.readFileSync(path.join(root, 'sheets/cs2-models.json')));
for (const id of ['hl2', 'tf2', 'hl1mp', 'gmod', 'goblin', 'portal2']) {
  const m = models[id];
  if (!m || !/^models\/sourcebox\/[a-z0-9_/]+\.vmdl$/.test(m.model)) throw Error('Missing CS2 model: '+id);
  for (const k of ['idle','walk','run','air']) if (!/^[a-z0-9_]+$/.test(m[k])) throw Error('Missing animation: '+id+'/'+k);
  for (const k of ['weaponAttachment','weaponBone']) if (!/^[a-zA-Z0-9_.]+$/.test(m[k])) throw Error('Missing weapon rig: '+id+'/'+k);
}
const rows = Object.entries(models).map(([id,m])=>`["${id}"]=new(${['model','idle','walk','run','air','weaponAttachment'].map(k=>JSON.stringify(m[k])).join(',')})`);
fs.writeFileSync(path.join(root, 'adapters/cs2/PlayerModels.g.cs'), '// Generated from sheets/cs2-models.json\npublic partial class SourceboxCS2 {\n private record PlayerModel(string Model,string Idle,string Walk,string Run,string Air,string WeaponAttachment);\n private static readonly Dictionary<string,PlayerModel> PlayerModels=new(){'+rows.join(',')+'};\n}\n');
console.log('Validated and generated CS2 player model mappings.');

const scenePath=path.join(root,'sheets/cs2-scene-assets.json');
const sceneryPath=path.join(root,'sheets/cs2-world-scenery-assets.json');
const scenery=fs.existsSync(sceneryPath)?JSON.parse(fs.readFileSync(sceneryPath)):{};
// Reuse already compiled native scenery when a spawn-menu model has no
// dedicated textured conversion yet. Dedicated scene assets take precedence.
const scene={...scenery,...(fs.existsSync(scenePath)?JSON.parse(fs.readFileSync(scenePath)):{})};
Object.assign(scene,{'models/props_c17/oildrum001.mdl':'models/sourcebox/props/oildrum001.vmdl','models/props_junk/wood_crate001a.mdl':'models/sourcebox/props/wood_crate001a.vmdl'});
for(const [source,target] of Object.entries(scene))if(!/^models\/[a-z0-9_/. -]+\.mdl$/.test(source)||!/^models\/sourcebox\/[a-z0-9_/. -]+\.vmdl$/.test(target))throw Error('Invalid scene asset: '+source);
const held=JSON.parse(fs.readFileSync(path.join(root,'sheets/asset-references.json'))).hl2Weapons;
const dictionary=o=>Object.entries(o).map(([k,v])=>'['+JSON.stringify(k)+']='+JSON.stringify(v)).join(',');
fs.writeFileSync(path.join(root,'adapters/cs2/SceneAssets.g.cs'),'// Generated from validated scene asset and weapon sheets.\npublic partial class SourceboxCS2 { private static readonly Dictionary<string,string> PropModels=new(){'+dictionary(scene)+'}; private static readonly Dictionary<string,string> HeldWeapons=new(){'+dictionary(held)+'}; }\n');
console.log(Object.keys(scene).length+' converted scene assets registered.');
const destinations=JSON.parse(fs.readFileSync(path.join(root,'sheets/worlds-expanded.json'))).worlds;
const bounds=Object.fromEntries(destinations.map(w=>[w.id,JSON.parse(fs.readFileSync(path.join(root,'worlds',w.id+'.json'))).bounds]));
fs.writeFileSync(path.join(root,'adapters/cs2/WorldBounds.g.cs'),'// Generated from validated world geometry.\npublic partial class SourceboxCS2 { private static readonly Dictionary<string,float[][]> WorldBounds=new(){'+Object.entries(bounds).map(([id,b])=>'['+JSON.stringify(id)+']=new float[][]{'+b.map(v=>'new float[]{'+v.map(n=>Number(n)+'f').join(',')+'}').join(',')+'}').join(',')+'}; }\n');
fs.writeFileSync(path.join(root,'adapters/cs2/WorldDestinations.g.cs'),'// Generated from sheets/worlds-expanded.json.\npublic partial class SourceboxCS2 { private static readonly (string Id,string Label)[] WorldDestinations = new[]{'+destinations.map(w=>'('+JSON.stringify(w.id)+','+JSON.stringify(w.label)+')').join(',')+'}; }\n');
