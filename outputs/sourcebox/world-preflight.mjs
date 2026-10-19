import fs from 'node:fs';
export function validateWorldData(data, definition) {
 const errors=[];
 const check=(ok,message)=>{if(!ok)errors.push(message)};
 check(data?.id===definition.id,'World identity mismatch');
 check(JSON.stringify(data?.offset)===JSON.stringify(definition.offset),'World offset mismatch');
 check(JSON.stringify(data?.spawn)===JSON.stringify(definition.spawn),'World spawn mismatch');
 const vector=v=>Array.isArray(v)&&v.length===3&&v.every(n=>Number.isFinite(n)&&Math.abs(n)<16000);
 check(Array.isArray(data?.bounds)&&data.bounds.length===2&&data.bounds.every(vector),'Invalid world bounds');
 if(Array.isArray(data?.bounds)&&data.bounds.length===2&&data.bounds.every(vector)){
  check(data.bounds[0].every((v,i)=>v<=data.bounds[1][i]),'Reversed world bounds');
  check(vector(data.spawn)&&data.spawn.every((v,i)=>v>=data.bounds[0][i]-192&&v<=data.bounds[1][i]+192),'Spawn outside world geometry bounds');
 }
 for(const [kind,width,maxChunks] of [['chunks',8,250],['collision',3,32]]) {
  const chunks=data?.[kind];
  check(Array.isArray(chunks)&&chunks.length>0&&chunks.length<=maxChunks,`${kind} exceeds world budget or is missing`);
  if(!Array.isArray(chunks))continue;
  for(const chunk of chunks) {
   const v=chunk.vertices;
   check(Array.isArray(v)&&v.length>0&&v.length%3===0&&v.length<=6000,`${kind} has invalid triangle count`);
   if(Array.isArray(v))check(v.every(p=>Array.isArray(p)&&p.length===width&&p.every(Number.isFinite)&&vector(p.slice(0,3))),`${kind} has invalid coordinates`);
   if(kind==='chunks')check(typeof chunk.material==='string'&&chunk.material.length>0,'Missing material');
  }
 }
 return errors;
}
export function preflightWorlds(config) {
 if(!config?.enabled)return;
 for(const definition of config.worlds) {
  if(!/^[a-z0-9_]+$/.test(definition.id))throw Error('Invalid world id');
  const data=JSON.parse(fs.readFileSync(new URL(`./worlds/${definition.id}.json`,import.meta.url)));
  const errors=validateWorldData(data,definition);
  if(errors.length)throw Error(`${definition.id}: ${errors.join('; ')}`);
 }
}
