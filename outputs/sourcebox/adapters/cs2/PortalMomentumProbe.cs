using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private bool momentumProbeDone;
 private long momentumProbeAt,momentumReadyAt;
 private Vector? momentumOriginal;
 private QAngle? momentumOriginalLook;
 private void TickMomentumProbe(){
  if(momentumProbeDone||Environment.GetEnvironmentVariable("SOURCEBOX_MOMENTUM_PROBE")!="1"||Server.MapName!="sourcebox_room_001")return;
  var pawn=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2)?.PlayerPawn.Value;
  if(pawn?.IsValid!=true||pawn.Health<=0||pawn.AbsOrigin==null||portalRevision<0||portalMarkers.Count<2)return;
  var now=Environment.TickCount64;
  if(momentumProbeAt==0){
   if(momentumReadyAt==0){momentumReadyAt=now;return;}
   if(now-momentumReadyAt<2000)return;
   var p=pawn.AbsOrigin;var a=pawn.EyeAngles;
   momentumOriginal=new Vector(p.X,p.Y,p.Z);momentumOriginalLook=new QAngle(a.X,a.Y,0);
   // The preceding HL2 fixture placed the canonical pair at this floor point.
   pawn.Teleport(new Vector(0,-320,150),new QAngle(0,0,0),new Vector());
   momentumProbeAt=now;Console.WriteLine("[Sourcebox test] CS2 native momentum probe begin");return;
  }
  if(now-momentumProbeAt<3000)return;
  pawn.Teleport(momentumOriginal,momentumOriginalLook,new Vector());momentumProbeDone=true;
  Console.WriteLine("[Sourcebox test] CS2 native momentum probe end; inspect core samples for actual velocity");
 }
}
