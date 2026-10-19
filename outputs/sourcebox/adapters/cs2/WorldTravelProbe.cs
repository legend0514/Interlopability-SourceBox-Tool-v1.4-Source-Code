using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private int worldProbeIndex,worldProbeStage;private long worldProbeAt;private Vector? worldProbeOriginal;private QAngle? worldProbeLook;
 private readonly List<object> worldProbeResults=new();
 private void TickWorldTravelProbe(){
  var marker=Path.Combine(ModuleDirectory,"world-travel-probe.txt");
  if(!File.Exists(marker)||!worldReadySent||Server.MapName!="sourcebox_room_001")return;
  var c=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot);if(c==null)return;
  if(c.TeamNum<2){c.ChangeTeam(CsTeam.Terrorist);c.Respawn();return;}
  var p=c.PlayerPawn.Value;if(p?.IsValid!=true||p.AbsOrigin==null||p.Health<=0||portalGun?.IsValid!=true)return;
  var now=Environment.TickCount64;
  void Finish(string status){
   File.WriteAllText(Path.Combine(ModuleDirectory,"world-travel-result.json"),JsonSerializer.Serialize(new{status,results=worldProbeResults,nativeMap=Server.MapName,fixture="Native pawn motion, adapter placement, core crossing and native teleport; not manual controls or preview acceptance"}));
   if(worldProbeOriginal!=null)p.Teleport(worldProbeOriginal,worldProbeLook,new Vector());
   Command(new{type="portal_clear",session=portalSession});selectedWorld=-1;File.Delete(marker);worldProbeStage=worldProbeIndex=0;worldProbeResults.Clear();
   Server.ExecuteCommand("mp_warmup_pausetimer 0; mp_ignore_round_win_conditions 0");
  }
  if(worldProbeStage==0){
   if(worldProbeIndex==0){worldProbeOriginal=new Vector(p.AbsOrigin.X,p.AbsOrigin.Y,p.AbsOrigin.Z);worldProbeLook=new QAngle(p.EyeAngles.X,p.EyeAngles.Y,0);Server.ExecuteCommand("mp_warmup_pausetimer 1; mp_warmuptime 9999; mp_warmup_start; mp_ignore_round_win_conditions 1");}
   selectedWorld=worldProbeIndex;p.Teleport(new Vector(400,0,0),new QAngle(0,0,0),new Vector());worldProbeStage=1;worldProbeAt=now;return;
  }
  if(selectedWorld!=worldProbeIndex){Finish("cancelled-destination-selection-changed");return;}
  if(now-worldProbeAt>15000){Finish("timeout-stage-"+worldProbeStage);return;}
  if(worldProbeStage==1&&now-worldProbeAt>1500){
   if(p.WeaponServices?.ActiveWeapon.Value?.Index!=portalGun.Index){p.WeaponServices!.ActiveWeapon.Raw=p.WeaponServices.MyWeapons.First(h=>h.Value?.Index==portalGun.Index).Raw;Utilities.SetStateChanged(p,"CBasePlayerPawn","m_pWeaponServices");p.Teleport(new Vector(400,0,0),new QAngle(),new Vector());worldProbeAt=now;return;}
   var eye=new Vector(p.AbsOrigin.X+p.ViewOffset.X,p.AbsOrigin.Y+p.ViewOffset.Y,p.AbsOrigin.Z+p.ViewOffset.Z);
   var trace=Trace.TraceShape(eye,p.EyeAngles,p,PortalTraceOptions());if(!trace.DidHit()||trace.IsAllSolid){Finish("native-shot-missed");return;}
   Command(new{type="portal_fire",session=portalSession,endpoint=0,destination=ShotDestination(),eye=new[]{eye.X,eye.Y,eye.Z},hit=new[]{trace.EndPos.X,trace.EndPos.Y,trace.EndPos.Z}});worldProbeAt=now;worldProbeStage=2;return;
  }
  var pair=propPortalPairs.FirstOrDefault(q=>q.TryGetProperty("destination",out var d)&&d.GetString()==WorldDestinations[worldProbeIndex].Id);
  if(worldProbeStage==2&&pair.ValueKind==JsonValueKind.Object&&now-worldProbeAt>500){p.Teleport(new Vector(450,0,0),new QAngle(),new Vector(220,0,0));worldProbeStage=3;worldProbeAt=now;return;}
  var expectedBounds=WorldBounds[WorldDestinations[worldProbeIndex].Id];
  var actual=new[]{p.AbsOrigin.X,p.AbsOrigin.Y,p.AbsOrigin.Z};
  var inExpectedWorld=Enumerable.Range(0,3).All(i=>actual[i]>=expectedBounds[0][i]-192&&actual[i]<=expectedBounds[1][i]+192);
  if(worldProbeStage==3&&inExpectedWorld){
   worldProbeResults.Add(new{id=WorldDestinations[worldProbeIndex].Id,direction="outbound",position=new[]{p.AbsOrigin.X,p.AbsOrigin.Y,p.AbsOrigin.Z},passed=true});Console.WriteLine("[Sourcebox world travel] viewing "+WorldDestinations[worldProbeIndex].Id);worldProbeStage=4;worldProbeAt=now;return;
  }
  if(worldProbeStage==3){p.Teleport(null,null,new Vector(220,0,p.AbsVelocity.Z));return;}
  if(worldProbeStage==4&&now-worldProbeAt>8000&&pair.ValueKind==JsonValueKind.Object){
   if(peers.TryGetValue("gmod",out var remote)&&remote.Entity?.IsValid==true&&remote.Entity.AbsOrigin!=null){var q=remote.Entity.AbsOrigin;worldProbeResults.Add(new{id=WorldDestinations[worldProbeIndex].Id,check="native-gmod-proxy",position=new[]{q.X,q.Y,q.Z},distance=Math.Sqrt(Math.Pow(q.X-p.AbsOrigin.X,2)+Math.Pow(q.Y-p.AbsOrigin.Y,2)+Math.Pow(q.Z-p.AbsOrigin.Z,2))});}
   var e=pair.GetProperty("endpoints")[1];var o=PV(e.GetProperty("position"));var n=PV(e.GetProperty("normal"));
   p.Teleport(new Vector(o.X+n.X*65,o.Y+n.Y*65,o.Z-36),null,new Vector(-n.X*220,-n.Y*220,0));worldProbeStage=5;worldProbeAt=now;return;
  }
  if(worldProbeStage==5&&p.AbsOrigin.Z<256&&Math.Abs(p.AbsOrigin.X)<512&&Math.Abs(p.AbsOrigin.Y)<512){
   worldProbeResults.Add(new{id=WorldDestinations[worldProbeIndex].Id,direction="return",position=new[]{p.AbsOrigin.X,p.AbsOrigin.Y,p.AbsOrigin.Z},passed=true});
   if(++worldProbeIndex>=WorldDestinations.Length){Finish("completed");return;}worldProbeStage=0;return;
  }
 }
}


