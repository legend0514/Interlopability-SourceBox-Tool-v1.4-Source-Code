import fs from 'node:fs';
import {add,sub,scale,dot,cross,frame} from './portal-math.mjs';
export function traceTriangles(origin,direction,triangles,range){
 let nearest=null;
 for(const [a,b,c] of triangles){
  const e=sub(b,a),f=sub(c,a),p=cross(direction,f),det=dot(e,p);
  if(Math.abs(det)<1e-8)continue;
  const t=sub(origin,a),u=dot(t,p)/det;if(u< -1e-6||u>1.000001)continue;
  const q=cross(t,e),v=dot(direction,q)/det;if(v< -1e-6||u+v>1.000001)continue;
  const distance=dot(f,q)/det;if(distance<0||distance>range||nearest&&distance>=nearest.distance)continue;
  let normal=cross(e,f);normal=scale(normal,1/Math.hypot(...normal));if(dot(normal,direction)>0)normal=scale(normal,-1);
  nearest={distance,position:add(origin,scale(direction,distance)),normal};
 }
 return nearest;
}
export function createWorldPortalPlacement(definitions,read=id=>JSON.parse(fs.readFileSync(new URL(`./worlds/${id}.json`,import.meta.url)))){
 const cache=new Map();
 return (client,origin,direction,config)=>{
  const def=definitions.find(w=>w.id===client.world);if(!def||!client.worldsReady?.includes(def.id))return {error:'Source world geometry is not loaded'};
  if(!cache.has(def.id))cache.set(def.id,read(def.id).collision.flatMap(c=>Array.from({length:c.vertices.length/3},(_,i)=>c.vertices.slice(i*3,i*3+3))));
  const triangles=cache.get(def.id),hit=traceTriangles(origin,direction,triangles,config.maxRange);
  if(!hit)return {error:'No destination-world surface in range'};
  const basis=frame(hit.position,hit.normal);
  for(const x of [-config.aperture.halfWidth,config.aperture.halfWidth])for(const y of [-config.aperture.halfHeight,config.aperture.halfHeight]){
   const point=add(hit.position,add(scale(basis.right,x),scale(basis.up,y)));
   const corner=traceTriangles(add(point,scale(hit.normal,3)),scale(hit.normal,-1),triangles,6);
   if(!corner||Math.abs(corner.distance-3)>0.5||dot(corner.normal,hit.normal)<0.99)return {error:'Portal aperture does not fit this world surface'};
  }
  return {...basis,hit:hit.position,distance:hit.distance,world:def.id,surface:'world:'+def.id+':'+hit.normal.map(n=>n.toFixed(3)).join(',')+':'+dot(hit.position,hit.normal).toFixed(1)};
 };
}
