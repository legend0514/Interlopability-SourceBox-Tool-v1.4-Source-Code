using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private void CreateMonitorProbe(){
  if(Environment.GetEnvironmentVariable("SOURCEBOX_PREVIEW_PROBE")!="1")return;
  Server.ExecuteCommand("sv_cheats 1");
  try{
   var camera=Utilities.CreateEntityByName<CPointCamera>("point_camera")!;
   camera.Entity!.Name="sourcebox_monitor_camera";camera.FOV=90;camera.AspectRatio=1;camera.ZNear=1;camera.ZFar=4096;camera.Active=true;
   camera.DispatchSpawn();camera.Teleport(new Vector(0,256,64),new QAngle(0,-90,0),new Vector());camera.AcceptInput("SetOn");
   var monitor=Utilities.CreateEntityByName<CFuncMonitor>("func_monitor")!;
   monitor.Entity!.Name="sourcebox_monitor_test";monitor.TargetCamera="sourcebox_monitor_camera";
   monitor.BrushModelName="models/sourcebox/portals/monitor/monitor.vmdl";monitor.StartEnabled=true;monitor.Enabled=true;monitor.UseUniqueColorTarget=true;monitor.ResolutionEnum=1;
   monitor.DispatchSpawn();
   monitor.Teleport(new Vector(128,0,80),new QAngle(0,90,0),new Vector());monitor.AcceptInput("SetCamera",value:"sourcebox_monitor_camera");monitor.AcceptInput("Enable");
   Console.WriteLine($"[Sourcebox] monitor test camera={camera.Index} monitor={monitor.Index} linked={monitor.HTargetCamera.IsValid}");
  }catch(Exception e){Console.WriteLine("[Sourcebox] monitor test failed: "+e);}
 }
}
