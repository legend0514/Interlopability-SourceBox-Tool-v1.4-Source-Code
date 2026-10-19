import fs from 'node:fs';
export function createWorldResolver(config){
 const hub=config.mapTravel?.hub??'sourcebox_room_001';
 const worlds=config.destinationWorlds?.enabled?config.destinationWorlds.worlds.map(w=>({id:w.id,bounds:JSON.parse(fs.readFileSync(new URL(`./worlds/${w.id}.json`,import.meta.url))).bounds})):[];
 const inside=(p,b,margin)=>p.every((v,i)=>v>=b[0][i]-margin&&v<=b[1][i]+margin);
 return position=>{
  if(inside(position,config.portals?.bounds??[[-512,-512,0],[512,512,256]],100))return hub;
  return worlds.find(w=>inside(position,w.bounds,100))?.id??'outside_loaded_worlds';
 };
}
export function canInteractInWorld(a,b){return Boolean(a&&b&&a===b&&a!=='outside_loaded_worlds');}
