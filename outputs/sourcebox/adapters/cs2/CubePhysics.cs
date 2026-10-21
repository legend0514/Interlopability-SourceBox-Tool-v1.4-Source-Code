using System.Text.Json;
using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private readonly System.Collections.Concurrent.ConcurrentQueue<string> commands=new();
 private CPhysicsProp? ownedCube; private long cubeEpoch,lastCubeSample;private string cubeOwner="",sceneSession="";private bool useHeld;private int cubeGeneration;
 private bool cubeEnabled; private long cubeRecoveryAt;private bool cubeHandoffPending;
 private void Command(object packet){if(commands.Count<128)commands.Enqueue(JsonSerializer.Serialize(packet));else Console.WriteLine("[Sourcebox] Outbound interaction queue full; update dropped");}
 private void ClearCube(){cubeGeneration++;cubeEnabled=false;cubeEpoch=0;cubeHandoffPending=false;if(ownedCube?.IsValid==true)ownedCube.Remove();ownedCube=null;cubeOwner="";while(commands.TryDequeue(out _)){};}
 private object CubeSnapshot(){var e=ownedCube!;var p=e.AbsOrigin!;var a=e.AbsRotation??new QAngle(0,0,0);var v=cubePortalPrevious!=null&&cubePortalEpoch==cubeEpoch?CV(cubePortalVelocity):e.AbsVelocity;var w=e.AngVelocity;return new{alive=true,solid=true,position=new[]{p.X,p.Y,p.Z},rotation=new[]{a.X,a.Y,a.Z},velocity=new[]{v.X,v.Y,v.Z},angularVelocity=new[]{w.X,w.Y,w.Z},timestamp=Environment.TickCount64};}
 private bool CubeMessage(JsonElement m,string type){
  if(!cubeEnabled&&type.StartsWith("cube_"))return true;
  if(type=="cube_denied"){foreach(var c in Utilities.GetPlayers().Where(c=>c.IsValid&&!c.IsBot))c.PrintToChat("Sourcebox: "+m.GetProperty("reason").GetString());return true;}
  if(type=="cube_freeze"){
   if(cubeOwner!="cs2"||ownedCube?.IsValid!=true||m.GetProperty("epoch").GetInt64()!=cubeEpoch)return true;
   var snapshot=CubeSnapshot();var transfer=m.GetProperty("transfer").GetString();var epoch=cubeEpoch;var generation=cubeGeneration;
   cubeHandoffPending=true;ownedCube.AcceptInput("DisableMotion");ownedCube.Remove();ownedCube=null;cubeOwner="";
   AddTimer(0.1f,()=>{if(generation==cubeGeneration)Command(new{type="cube_frozen",transfer,epoch,state=snapshot});});return true;
  }
  if(type=="cube_owner"){
   var epoch=m.GetProperty("epoch").GetInt64();if(epoch<cubeEpoch)return true;cubeHandoffPending=false;cubeEpoch=epoch;cubeOwner=m.GetProperty("owner").GetString()!;
   if(cubeOwner!="cs2"){if(ownedCube?.IsValid==true)ownedCube.Remove();ownedCube=null;return false;}
   RemoveProp("sourcebox_shared_cube");if(ownedCube?.IsValid==true)return true;
   var s=m.GetProperty("state");var pos=s.GetProperty("position");var rot=s.GetProperty("rotation");
   ownedCube=Utilities.CreateEntityByName<CPhysicsProp>("prop_physics_multiplayer");if(ownedCube==null)return true;
   ownedCube.SetModel(PropModels["models/props_junk/wood_crate001a.mdl"]);ownedCube.Teleport(new Vector(pos[0].GetSingle(),pos[1].GetSingle(),pos[2].GetSingle()),new QAngle(rot[0].GetSingle(),rot[1].GetSingle(),rot[2].GetSingle()),new Vector(0,0,0));ownedCube.DispatchSpawn();ownedCube.Render=Color.DodgerBlue;ownedCube.Health=100000;ownedCube.AcceptInput("EnableMotion");ownedCube.AcceptInput("Wake");
   Command(new{type="cube_ready",epoch=cubeEpoch,created=true});Console.WriteLine("[Sourcebox] CS2 owns native physics cube, epoch "+cubeEpoch);return true;
  }
  if(type=="cube_state"&&cubeOwner=="cs2")return true;return false;
 }
 private void TickInteraction(long now){
  // Round/map startup can delete entities created before the world is ready.
  // Recover from the core's current authority record, never invent a new owner.
  if(cubeEnabled&&!cubeHandoffPending&&!string.IsNullOrEmpty(portalSession)&&Server.MapName=="sourcebox_room_001"&&now-cubeRecoveryAt>1000&&(cubeOwner==""||(cubeOwner=="cs2"&&ownedCube?.IsValid!=true))){cubeRecoveryAt=now;Command(new{type="cube_sync"});}
  TickPropPortals(now);
  var controller=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2);var pawn=controller?.PlayerPawn.Value;
  var pressed=controller!=null&&(controller.Buttons&PlayerButtons.Use)!=0;
  // E is the destination modifier while the Portal Gun is equipped.
  // Do not claim a nearby prop on the first half of the E+R chord.
  var portalEquipped=portalGun?.IsValid==true&&pawn?.WeaponServices?.ActiveWeapon.Value?.Index==portalGun.Index;
  if(pressed&&!useHeld&&!portalEquipped&&pawn?.AbsOrigin!=null){
   var p=pawn.AbsOrigin;float Distance(float[] q)=>MathF.Sqrt((p.X-q[0])*(p.X-q[0])+(p.Y-q[1])*(p.Y-q[1])+(p.Z-q[2])*(p.Z-q[2]));
   if(cubeEnabled&&props.TryGetValue("sourcebox_shared_cube",out var cube)&&Distance(cube.Position)<=160){Command(new{type="cube_take"});controller!.PrintToChat("Sourcebox: requesting cube control.");}
   else {var near=props.Where(k=>k.Key.StartsWith("scene:")&&Distance(k.Value.Position)<=160).OrderBy(k=>Distance(k.Value.Position)).FirstOrDefault();if(near.Key!=null)Command(new{type="scene_interact",id=int.Parse(near.Key[6..]),session=sceneSession,action="use"});}
  }
  useHeld=pressed;
  if(cubeOwner=="cs2"&&ownedCube?.IsValid==true&&now-lastCubeSample>=50){lastCubeSample=now;Command(new{type="cube_state",epoch=cubeEpoch,state=CubeSnapshot()});}
 }
}


