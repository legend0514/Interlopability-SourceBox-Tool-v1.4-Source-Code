using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private readonly List<(CFuncMonitor Surface,CPointCamera Camera)> portalPreviews=new();
 private long previewTransmitReport;
 private void TransmitPortalPreviews(CCheckTransmitInfoList recipients){
  if(Server.MapName!="sourcebox_room_001"||portalPreviews.Count==0)return;
  int missingCameras=0,missingSurfaces=0,clients=0;
  foreach(var (info,client) in recipients){
   if(client==null||!client.IsValid||client.IsBot)continue;
   clients++;
   foreach(var p in portalPreviews){
    if(p.Camera.IsValid){if(!info.TransmitEntities.Contains(p.Camera))missingCameras++;info.TransmitEntities.Add(p.Camera);}
    if(p.Surface.IsValid){if(!info.TransmitEntities.Contains(p.Surface))missingSurfaces++;info.TransmitEntities.Add(p.Surface);}
   }
  }
  // Point cameras at exits can be outside the player's normal visibility set.
  // A valid server handle does not establish that its entity reached the client.
  var now=Environment.TickCount64;
  if(clients>0&&now-previewTransmitReport>10000){previewTransmitReport=now;Console.WriteLine($"[Sourcebox] preview transmit clients={clients} pairs={portalPreviews.Count} addedCameras={missingCameras} addedSurfaces={missingSurfaces}");}
 }
 private void ClearPortalPreviews(){foreach(var p in portalPreviews){if(p.Surface.IsValid)p.Surface.Remove();if(p.Camera.IsValid)p.Camera.Remove();}portalPreviews.Clear();}
 private void CreatePortalPreviews(IEnumerable<JsonElement> parts,long revision){
  ClearPortalPreviews();
  // A native camera preview is live, but is not an off-axis recursive portal renderer.
  foreach(var part in parts)foreach(var pair in part.EnumerateArray()){
   var ends=pair.GetProperty("endpoints");if(ends.GetArrayLength()!=2||ends[0].ValueKind==JsonValueKind.Null||ends[1].ValueKind==JsonValueKind.Null)continue;
   for(int side=0;side<2;side++){
    CPointCamera? camera=null;CFuncMonitor? surface=null;
    try{
     var entry=ends[side];var exit=ends[1-side];var n=PV(entry.GetProperty("normal"));var pos=PV(entry.GetProperty("position"));var N=PV(exit.GetProperty("normal"));var dest=PV(exit.GetProperty("position"));
     var name=$"sb_exit_{revision}_{portalPreviews.Count}";
     camera=Utilities.CreateEntityByName<CPointCamera>("point_camera")!;camera.Entity!.Name=name;
     camera.FOV=75;camera.AspectRatio=.5f;camera.UseScreenAspectRatio=false;camera.ZNear=1;camera.ZFar=4096;camera.Active=true;camera.Brightness=1;
     camera.DispatchSpawn();camera.Teleport(new Vector(dest.X+N.X*4,dest.Y+N.Y*4,dest.Z+N.Z*4),new QAngle(-MathF.Asin(N.Z)*180/MathF.PI,MathF.Atan2(N.Y,N.X)*180/MathF.PI,0),new Vector());camera.AcceptInput("SetOn");
     surface=Utilities.CreateEntityByName<CFuncMonitor>("func_monitor")!;surface.Entity!.Name=name+"_surface";surface.TargetCamera=name;
     surface.BrushModelName="models/sourcebox/portals/monitor/monitor.vmdl";surface.UseUniqueColorTarget=true;surface.StartEnabled=true;surface.Enabled=true;surface.ResolutionEnum=1;
     surface.Solidity=(BrushSolidities_e)1; // Native BRUSHSOLID_NEVER: the view must not block travel.
     // C_FuncMonitor builds its camera-textured scene object from BrushModelName.
     // SetModel also creates the ordinary base-model surface at the same depth,
     // which can cover that camera view. Leave the base model unset.
     surface.DispatchSpawn();surface.Collision.SolidType=SolidType_t.SOLID_NONE;surface.Teleport(new Vector(pos.X+n.X*1.2f,pos.Y+n.Y*1.2f,pos.Z+n.Z*1.2f),PortalRingAngles(n),new Vector());surface.AcceptInput("SetCamera",value:name);surface.AcceptInput("Enable");
     portalPreviews.Add((surface,camera));

     Console.WriteLine($"[Sourcebox] live preview surface={surface.Index} camera={camera.Index} owner={pair.GetProperty("owner")} side={side} linked={surface.HTargetCamera.IsValid}");
    }catch(Exception e){if(surface?.IsValid==true)surface.Remove();if(camera?.IsValid==true)camera.Remove();Console.WriteLine("[Sourcebox] preview creation failed: "+e.Message);}
   }
  }
 }
}

