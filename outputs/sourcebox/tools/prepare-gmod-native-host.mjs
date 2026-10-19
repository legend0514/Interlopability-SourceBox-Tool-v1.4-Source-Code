import fs from 'node:fs';
const [source,target]=process.argv.slice(2);
if(!source||!target)throw Error('Expected compiled host and destination BSP');
const b=fs.readFileSync(source);
if(b.toString('ascii',0,4)!=='VBSP')throw Error('Not a Source BSP');
const offset=b.readInt32LE(8),length=b.readInt32LE(12);
let entities=b.toString('utf8',offset,offset+length).replace(/\0+$/,'');
if(!entities.includes('sourcebox_worlds_baked'))throw Error('Destination host marker missing');
const spawn=JSON.parse(fs.readFileSync(new URL('../sheets/room.json',import.meta.url))).gmodSpawn;
entities=entities.replace(/\{[^{}]*"classname"\s*"info_player_start"[^{}]*\}/g,block=>block.replace(/"origin"\s*"[^"]*"/,`"origin" "${spawn.join(' ')}"`));
// Sandbox and the GMod adapter own the loadout and shared prop replicas.
entities=entities.replace(/\{[^{}]*"classname"\s*"(?:prop_physics|logic_auto|logic_timer|point_clientcommand|point_servercommand)"[^{}]*\}/g,'');
const data=Buffer.from(entities+'\0');b.writeInt32LE(b.length,8);b.writeInt32LE(data.length,12);
fs.writeFileSync(target,Buffer.concat([b,data]));
console.log('GMod native six-map host prepared with independent spawn and sandbox loadout');
