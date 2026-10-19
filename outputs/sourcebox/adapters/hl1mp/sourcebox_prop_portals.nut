// Runs on the one authoritative native cube; remote replicas have no script.
SBPortalPairs <- [];
SBPrevious <- null;
SBVelocity <- Vector(0,0,0);
SBTime <- 0.0;
SBCooldown <- 0.0;
function SBRotate(v,a,b) {return b[1]*(-v.Dot(a[1]))+b[2]*(-v.Dot(a[2]))+b[3]*v.Dot(a[3]);}
function SBPortalThink() {
 if(self.GetClassname()!="prop_physics")return 0.02;
 local center=self.GetCenter(),velocity=self.GetPhysVelocity(),now=Time();
 local previous=SBPrevious,oldVelocity=SBVelocity,dt=now-SBTime;
 SBPrevious=center;SBVelocity=velocity;SBTime=now;
 if(previous==null||dt>0.25||now<SBCooldown)return 0.02;
 local half=(self.GetBoundingMaxs()-self.GetBoundingMins())*0.5;
 local forward=self.GetForwardVector(),left=self.GetLeftVector(),up=self.GetUpVector();
 local extent=function(axis){return fabs(axis.Dot(forward))*half.x+fabs(axis.Dot(left))*half.y+fabs(axis.Dot(up))*half.z;};
 foreach(pair in SBPortalPairs)for(local side=0;side<2;side++){
  local a=pair[side],b=pair[1-side],radius=extent(a[1]);
  local before=(previous-a[0]).Dot(a[1])-radius-2,after=(center-a[0]).Dot(a[1])-radius-2;
  if(after>0||(after < -radius-2 && before<=0)||after>before+0.001)continue;
  local incoming=oldVelocity.Dot(a[1])<velocity.Dot(a[1])?oldVelocity:velocity;
  if(incoming.Dot(a[1])>-1)continue;
  local t=before>0?before/(before-after):1,offset=previous+(center-previous)*t-a[0];
  if(fabs(offset.Dot(a[2]))+extent(a[2])>a[4]||fabs(offset.Dot(a[3]))+extent(a[3])>a[5])continue;
  local radiusOut=half.Length(),dest=b[0]-b[2]*offset.Dot(a[2])+b[3]*offset.Dot(a[3])+b[1]*(radiusOut+4);
  local bounds=Vector(radiusOut,radiusOut,radiusOut);
  local trace={start=dest,end=dest,hullmin=bounds*-1,hullmax=bounds,ignore=self,mask=33570827};
  if(!TraceHull(trace)||trace.hit)continue;
  local outForward=SBRotate(forward,a,b),outUp=SBRotate(up,a,b),outLeft=outUp.Cross(outForward);
  outLeft=outLeft*(1.0/outLeft.Length());local xy=sqrt(outForward.x*outForward.x+outForward.y*outForward.y),deg=180.0/3.141592653589793;
  local pitch=atan2(-outForward.z,xy)*deg,yaw=(xy>0.001?atan2(outForward.y,outForward.x):atan2(-outLeft.x,outLeft.y))*deg;
  local roll=xy>0.001?atan2(outLeft.z,outLeft.y*outForward.x-outLeft.x*outForward.y)*deg:0;
  local pos=dest-SBRotate(center-self.GetOrigin(),a,b),spin=SBRotate(self.GetPhysAngularVelocity(),a,b),out=SBRotate(incoming,a,b);
  self.SetOrigin(pos);self.SetAngles(pitch,yaw,roll);self.SetPhysVelocity(out);self.SetPhysAngularVelocity(spin);
  SBPrevious=dest;SBVelocity=out;SBCooldown=now+0.25;
  printl("[Sourcebox] HL1 native prop portal speed="+incoming.Length()+" -> "+out.Length());return 0.02;
 }
 return 0.02;
}
AddThinkToEnt(self,"SBPortalThink");

