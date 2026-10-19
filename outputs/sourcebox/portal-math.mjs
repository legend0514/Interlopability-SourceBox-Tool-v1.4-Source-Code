export const add=(a,b)=>a.map((v,i)=>v+b[i]);
export const sub=(a,b)=>a.map((v,i)=>v-b[i]);
export const scale=(a,s)=>a.map(v=>v*s);
export const dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
export const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
export function aim(rotation){const p=rotation[0]*Math.PI/180,y=rotation[1]*Math.PI/180;return [Math.cos(p)*Math.cos(y),Math.cos(p)*Math.sin(y),-Math.sin(p)];}
export function frame(position,normal){normal=scale(normal,1/Math.hypot(...normal));const reference=Math.abs(normal[2])>0.9?[-Math.sign(normal[2]),0,0]:[0,0,1];let right=cross(reference,normal);right=scale(right,1/Math.hypot(...right));return {position,normal,up:cross(normal,right),right};}
// Columns are right/up/inward-normal. Half-turn around local up flips right
// and normal; translations never affect velocities.
export function transformVector(v,entry,exit){return add(add(scale(exit.right,-dot(v,entry.right)),scale(exit.up,dot(v,entry.up))),scale(exit.normal,-dot(v,entry.normal)));}
export const transformPoint=(p,entry,exit)=>add(exit.position,transformVector(sub(p,entry.position),entry,exit));
export function transformLook(rotation,entry,exit){const d=transformVector(aim(rotation),entry,exit);return [Math.atan2(-d[2],Math.hypot(d[0],d[1]))*180/Math.PI,Math.atan2(d[1],d[0])*180/Math.PI,0];}
export const extent=(axis,config)=>Math.abs(axis[2])*config.playerHalfHeight+(Math.abs(axis[0])+Math.abs(axis[1]))*config.playerRadius;
// The receiving engine may have already removed normal velocity on contact.
// Recover only a fresh approach component, never old speed from wall-hugging.
export function entryVelocity(state,previous,center,entry,config,age){
 const current=[...state.velocity];
 if(!previous?.velocity||age<0||age>config.momentumRecoveryMs)return current;
 const before=dot(previous.velocity,entry.normal),after=dot(current,entry.normal);
 const distance=dot(sub(center,entry.position),entry.normal);
 const oldDistance=dot(sub(previous.center,entry.position),entry.normal);
 if(before>=-1||after<-1||oldDistance<=distance+0.01||distance>extent(entry.normal,config)+(config.contactMargin??2))return current;
 const restored=add(current,scale(entry.normal,before-after));
 // Steering must not combine the old normal with new tangent speed into a boost.
 const ceiling=Math.max(Math.hypot(...previous.velocity),Math.hypot(...current));
 const speed=Math.hypot(...restored);
 return speed>ceiling?scale(restored,ceiling/speed):restored;
}
export function wallHit(origin,direction,config){let nearest=null;for(let axis=0;axis<3;axis++)for(let side=0;side<2;side++){
 if(Math.abs(direction[axis])<1e-8)continue;
 const distance=(config.bounds[side][axis]-origin[axis])/direction[axis];if(distance<=0||distance>config.maxRange||nearest&&distance>=nearest.distance)continue;
 const position=add(origin,scale(direction,distance));
 if(position.some((v,i)=>i!==axis&&(v<config.bounds[0][i]||v>config.bounds[1][i])))continue;
 const normal=[0,0,0];normal[axis]=side?-1:1;nearest={...frame(position,normal),distance,surface:`wall:${axis}:${side}`,hit:[...position]};
 }if(!nearest)return {error:'No room surface in range'};
 // Bump the aperture away from an edge without placing outside the room.
 for(let axis=0;axis<3;axis++)if(!nearest.normal[axis]){
  const padding=Math.abs(nearest.right[axis])*config.aperture.halfWidth+Math.abs(nearest.up[axis])*config.aperture.halfHeight;
  const low=config.bounds[0][axis]+padding,high=config.bounds[1][axis]-padding;
  if(low>high)return {error:'Portal aperture does not fit this surface'};
  nearest.position[axis]=Math.max(low,Math.min(high,nearest.position[axis]));
  // A shot at eye height just above the floor must leave a walkable aperture.
  if(axis===2&&Math.abs(nearest.normal[2])<0.1&&nearest.position[axis]-low<=(config.floorSnapDistance??0))nearest.position[axis]=low;
 }
 return nearest;
}
export function crossing(previous,current,portal,config){
 // Native collision stops feet just above a floor. Trigger before that contact
 // skin, otherwise a floor portal never sees an actual plane crossing.
 const threshold=extent(portal.normal,config)+(config.contactMargin??2);
 const a=dot(sub(previous,portal.position),portal.normal)-threshold,b=dot(sub(current,portal.position),portal.normal)-threshold;
 // A player can hit the wall beside the opening and then slide into it.
 // Requiring another normal-plane crossing leaves that player permanently stuck.
 // Recheck the contact band, while rejecting motion away and the back side.
 // A swept approach may cross the whole plane between samples. Reject a
 // sample already behind the plane only when it did not start in front.
 if(b>0||(b < -threshold&&a<=0)||b>a+1e-6)return null;
 const t=a>0&&a-b>1e-6?a/(a-b):1;
 const point=add(previous,scale(sub(current,previous),t)),offset=sub(point,portal.position);
 if(Math.abs(dot(offset,portal.right))+extent(portal.right,config)>config.aperture.halfWidth||Math.abs(dot(offset,portal.up))+extent(portal.up,config)>config.aperture.halfHeight)return null;
 return point;
}
