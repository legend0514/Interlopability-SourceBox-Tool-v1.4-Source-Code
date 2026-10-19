import fs from 'node:fs';
import {createRequire} from 'node:module';
import crypto from 'node:crypto';
import {preflight} from './preflight.mjs';
const {vpk,mdl}=createRequire(import.meta.url)('./tools/installed-assets.cjs');
const s=preflight();
for(const [name,w]of Object.entries(s.assets.weapons??{})){
 const read=vpk(w.archive).get(w.model);
 if(!read||crypto.createHash('sha256').update(read()).digest('hex')!==w.sha256)throw Error(`Weapon asset changed: ${name}`);
}
if(s.phase==='C')for(const [game,c]of Object.entries({...s.assets.proxies,...s.assets.tf2Classes})){
 const read=vpk(c.archive).get(c.model);if(!read)throw Error(`Missing installed model for ${game}`);
 const b=read();
 if(crypto.createHash('sha256').update(b).digest('hex')!==c.sha256)throw Error(`Installed model changed for ${game}: inspect sequences before rebuilding`);
 const m=mdl(b);
 if(m.bones<2)throw Error(`Model is not rigged: ${game}`);
 for(const [kind,seq]of Object.entries(c.sequences))if(m.sequences[seq.index]?.name!==seq.name)throw Error(`Invalid ${game} ${kind} sequence`);
 console.log(`${game}: installed rig and animation references verified; rendering not yet verified`);
}

if(s.gmodScene){for(const archive of s.gmodScene.archives){const files=vpk(archive);for(const model of s.gmodScene.models)if(!files.has(model)||!files.has(model.replace(/\.mdl$/,".phy")))throw Error("Missing shared spawned-prop asset: "+model);}console.log("GMod spawned-prop installed model references verified");}

if(s.gmodScene){const files=vpk("C:/Program Files (x86)/Steam/steamapps/common/Half-Life 1 Source Deathmatch/hl2/hl2_misc_dir.vpk");for(const model of s.gmodScene.npcModels){const read=files.get(model);if(!read||mdl(read()).bones<2)throw Error("Missing rigged NPC asset: "+model);}console.log("Stock NPC model rigs verified; native rendering still requires gameplay testing");}

if(s.gmodScene?.addon){const a=s.gmodScene.addon;for(const model of [...a.npcModels,...a.playerModels]){const b=fs.readFileSync(a.source+'/'+model);if(mdl(b).bones<2)throw Error('Unrigged addon model: '+model);}console.log('Installed Goblin model rigs verified');}
