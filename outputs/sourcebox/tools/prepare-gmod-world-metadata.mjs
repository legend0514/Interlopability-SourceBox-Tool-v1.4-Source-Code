import fs from 'node:fs';
const project=new URL('../',import.meta.url);
const registry=JSON.parse(fs.readFileSync(new URL('sheets/worlds.json',project)));
for(const world of registry.worlds){
 const source=JSON.parse(fs.readFileSync(new URL(`worlds/${world.id}.json`,project)));
 if(source.id!==world.id||!source.bounds?.flat().every(Number.isFinite))throw Error(`Invalid world metadata: ${world.id}`);
 const metadata=Object.fromEntries(['id','bounds','spawn','offset','yaw'].filter(k=>k in source).map(k=>[k,source[k]]));
 metadata.chunks=[];metadata.collision=[];
 fs.writeFileSync(new URL(`worlds/${world.id}.metadata.json`,project),JSON.stringify(metadata));
}
console.log('Prepared lightweight native world metadata for GMod');
