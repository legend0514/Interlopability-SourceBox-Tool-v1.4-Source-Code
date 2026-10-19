using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private readonly List<JsonElement> propPortalPairs=new();
 private System.Numerics.Vector3? cubePortalPrevious;
 private System.Numerics.Vector3 cubePortalVelocity;
 private long cubePortalAt,cubePortalCooldown,cubePortalEpoch=-1;
 private bool propProbeRequested;private long propProbeAt;private Vector? propProbePlayer;private QAngle? propProbeLook;
 private void TickPropProbe(long now){
  var marker=Path.Combine(ModuleDirectory,"prop-probe.txt");
  if(!File.Exists(marker))return;
  var pawn=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2)?.PlayerPawn.Value;
  if(pawn?.IsValid!=true||pawn.AbsOrigin==null||pawn.Health<=0)return;
  var entry=propPortalPairs.SelectMany(p=>p.GetProperty("endpoints").EnumerateArray()).FirstOrDefault(e=>e.ValueKind==JsonValueKind.Object&&e.GetProperty("normal")[2].GetSingle()>0.99f);
  if(entry.ValueKind!=JsonValueKind.Object)return;
  if(propProbeAt==0){
   if(!props.TryGetValue("sourcebox_shared_cube",out var cube)&&cubeOwner!="cs2")return;
   propProbePlayer=new Vector(pawn.AbsOrigin.X,pawn.AbsOrigin.Y,pawn.AbsOrigin.Z);propProbeLook=new QAngle(pawn.EyeAngles.X,pawn.EyeAngles.Y,0);propProbeAt=now;
   if(cubeOwner!="cs2")pawn.Teleport(new Vector(cube!.Position[0]+70,cube.Position[1],8),null,new Vector());return;
  }
  if(now-propProbeAt<1000)return;
  if(cubeOwner!="cs2"){
   if(!propProbeRequested){propProbeRequested=true;Command(new{type="cube_take"});}
   if(now-propProbeAt<6000)return;
   Console.WriteLine("[Sourcebox test] cube portal probe failed: ownership timeout");
  }else if(ownedCube?.IsValid==true){
   var pos=PV(entry.GetProperty("position"));ownedCube.Teleport(new Vector(pos.X,pos.Y,pos.Z+160),new QAngle(),new Vector());ownedCube.AcceptInput("Wake");cubePortalPrevious=null;
   Console.WriteLine("[Sourcebox test] native cube dropped above "+entry.GetProperty("id").GetString());
  }else return;
  pawn.Teleport(propProbePlayer,propProbeLook,new Vector());File.Delete(marker);propProbeAt=0;propProbeRequested=false;
 }
 private static System.Numerics.Vector3 NV(JsonElement v)=>new(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle());
 private static System.Numerics.Vector3 NV(Vector v)=>new(v.X,v.Y,v.Z);
 private static System.Numerics.Vector3 NV(QAngle v)=>new(v.X,v.Y,v.Z);
 private static Vector CV(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
 private static System.Numerics.Vector3 PropRotate(System.Numerics.Vector3 v,JsonElement a,JsonElement b)=>
  -NV(b.GetProperty("normal"))*System.Numerics.Vector3.Dot(v,NV(a.GetProperty("normal")))
  -NV(b.GetProperty("right"))*System.Numerics.Vector3.Dot(v,NV(a.GetProperty("right")))
  +NV(b.GetProperty("up"))*System.Numerics.Vector3.Dot(v,NV(a.GetProperty("up")));
 private static QAngle PropAngles(QAngle angle,JsonElement entry,JsonElement exit){
  float p=angle.X*MathF.PI/180,y=angle.Y*MathF.PI/180,r=angle.Z*MathF.PI/180;
  var forward=PropRotate(new(MathF.Cos(p)*MathF.Cos(y),MathF.Cos(p)*MathF.Sin(y),-MathF.Sin(p)),entry,exit);
  var up=PropRotate(new(MathF.Cos(r)*MathF.Sin(p)*MathF.Cos(y)+MathF.Sin(r)*MathF.Sin(y),MathF.Cos(r)*MathF.Sin(p)*MathF.Sin(y)-MathF.Sin(r)*MathF.Cos(y),MathF.Cos(r)*MathF.Cos(p)),entry,exit);
  var left=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(up,forward));var xy=MathF.Sqrt(forward.X*forward.X+forward.Y*forward.Y);var degrees=180/MathF.PI;
  return new(-MathF.Atan2(forward.Z,xy)*degrees,(xy>0.001f?MathF.Atan2(forward.Y,forward.X):MathF.Atan2(-left.X,left.Y))*degrees,xy>0.001f?MathF.Atan2(left.Z,left.Y*forward.X-left.X*forward.Y)*degrees:0);
 }
 private void TickPropPortals(long now){
  TickPropProbe(now);
  if(cubeOwner!="cs2"||ownedCube?.IsValid!=true||ownedCube.AbsOrigin==null||!portalsEnabled){cubePortalPrevious=null;return;}
  var e=ownedCube;var pos=NV(e.AbsOrigin);var velocity=NV(e.AbsVelocity);
  var half=(NV(e.Collision.Maxs)-NV(e.Collision.Mins))*0.5f;
  // The shipped cube is symmetric; a bounding sphere also covers its rotation.
  var center=pos+(NV(e.Collision.Mins)+NV(e.Collision.Maxs))*0.5f;
  var previous=cubePortalPrevious;var oldVelocity=cubePortalVelocity;var elapsed=now-cubePortalAt;
  // Source 2 does not consistently mirror a VPhysics body's velocity into
  // CBaseEntity.AbsVelocity. Use its measured native displacement when needed.
  if(previous!=null&&elapsed>0&&elapsed<=250&&velocity.LengthSquared()<1){velocity=(center-previous.Value)/(elapsed/1000f);}
  cubePortalPrevious=center;cubePortalVelocity=velocity;cubePortalAt=now;
  if(cubePortalEpoch!=cubeEpoch){cubePortalEpoch=cubeEpoch;return;}
  if(previous==null||elapsed>250||now<cubePortalCooldown)return;
  foreach(var pair in propPortalPairs)for(int side=0;side<2;side++){
   var ends=pair.GetProperty("endpoints");if(ends[0].ValueKind==JsonValueKind.Null||ends[1].ValueKind==JsonValueKind.Null)continue;
   var entry=ends[side];var exit=ends[1-side];var n=NV(entry.GetProperty("normal"));var r=NV(entry.GetProperty("right"));var u=NV(entry.GetProperty("up"));
   float Extent(System.Numerics.Vector3 axis)=>System.Numerics.Vector3.Dot(System.Numerics.Vector3.Abs(axis),half);
   var origin=NV(entry.GetProperty("position"));var radius=Extent(n);var a=System.Numerics.Vector3.Dot(previous.Value-origin,n)-radius-2;var b=System.Numerics.Vector3.Dot(center-origin,n)-radius-2;
   if(b>0||(b<-radius-2&&a<=0)||b>a+0.001f)continue;
   var incoming=System.Numerics.Vector3.Dot(oldVelocity,n)<System.Numerics.Vector3.Dot(velocity,n)?oldVelocity:velocity;
   if(System.Numerics.Vector3.Dot(incoming,n)>-1)continue;
   var point=System.Numerics.Vector3.Lerp(previous.Value,center,a>0?Math.Clamp(a/(a-b),0,1):1);var offset=point-origin;
   var aperture=entry.GetProperty("aperture");
   if(MathF.Abs(System.Numerics.Vector3.Dot(offset,r))+Extent(r)>aperture.GetProperty("halfWidth").GetSingle()||MathF.Abs(System.Numerics.Vector3.Dot(offset,u))+Extent(u)>aperture.GetProperty("halfHeight").GetSingle())continue;
   var clearance=half.Length();var dest=NV(exit.GetProperty("position"))-NV(exit.GetProperty("right"))*System.Numerics.Vector3.Dot(offset,r)+NV(exit.GetProperty("up"))*System.Numerics.Vector3.Dot(offset,u)+NV(exit.GetProperty("normal"))*(clearance+4);
   var bound=new Vector(clearance,clearance,clearance);var trace=Trace.TraceHullShape(CV(dest),CV(dest),new Vector(-clearance,-clearance,-clearance),bound,e,PortalTraceOptions());
   if(trace.IsAllSolid||trace.DidHit())continue;
   var outVelocity=PropRotate(incoming,entry,exit);var spin=PropRotate(NV(e.AngVelocity),entry,exit);
   e.Teleport(CV(dest-PropRotate(center-pos,entry,exit)),PropAngles(e.AbsRotation??new QAngle(0,0,0),entry,exit),CV(outVelocity));
   e.AngVelocity.X=spin.X;e.AngVelocity.Y=spin.Y;e.AngVelocity.Z=spin.Z;e.AcceptInput("Wake");
   cubePortalPrevious=dest;cubePortalVelocity=outVelocity;cubePortalCooldown=now+250;
   Console.WriteLine($"[Sourcebox] native cube portal epoch={cubeEpoch} speed={incoming.Length()} -> {outVelocity.Length()}");return;
  }
 }
}


