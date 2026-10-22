import fs from 'node:fs';
import path from 'node:path';
import {validateWorldData} from '../world-preflight.mjs';
const result=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
for(const world of result.registry.worlds){
 if(!/^[a-z0-9_]+$/.test(world.id))throw Error('Invalid destination ID');
 const data=JSON.parse(fs.readFileSync(path.join(result.metadata,world.id+'.json')));
 const errors=validateWorldData(data,world);if(errors.length)throw Error(world.id+': '+errors.join('; '));
}
console.log('Validated destination bounds, spawns and portal-placement collision geometry.');
