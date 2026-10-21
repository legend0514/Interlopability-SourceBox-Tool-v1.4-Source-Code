using System.Text.Json;
using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private string portalSession="";private bool portalsEnabled;private long portalRevision=-1,portalPending=-1;private int portalCount;private PlayerButtons portalButtons;
 private readonly Dictionary<int,JsonElement> portalParts=new();private readonly HashSet<string> portalApplied=new();private readonly Queue<string> portalOrder=new();
 private readonly List<CDynamicProp> portalMarkers=new();private CBasePlayerWeapon? portalGun;
 private const string PortalWeaponName="sourcebox_portalgun";
 private void ProbePortalRendering(){
  // A schema or Hammer definition alone does not prove the retail entity exists.
  foreach(var name in new[]{"point_camera","func_monitor","info_camera_link"}){
   CBaseEntity? entity=null;
   try{entity=Utilities.CreateEntityByName<CBaseEntity>(name);Console.WriteLine($"[Sourcebox] portal render factory {name} available={entity?.IsValid==true}");}
   catch(Exception ex){Console.WriteLine($"[Sourcebox] portal render factory {name}: {ex.Message}");}
   finally{if(entity?.IsValid==true)entity.Remove();}
  }
 }
 // The FBX ring has local +Y normal and +Z up after Source 2 import.
 private static QAngle PortalRingAngles(Vector n)=>MathF.Abs(n.Z)>0.9f?new QAngle(0,n.Z>0?-90:90,n.Z>0?90:-90):new QAngle(0,MathF.Atan2(n.Y,n.X)*180/MathF.PI-90,0);
 private static Vector PV(JsonElement a)=>new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());
 private void ClearPortals(){worldReadySent=false;nextWorldReadyProbe=0;propPortalPairs.Clear();cubePortalPrevious=null;ClearPortalPreviews();foreach(var e in portalMarkers)if(e.IsValid)e.Remove();portalMarkers.Clear();portalParts.Clear();portalApplied.Clear();portalOrder.Clear();portalRevision=portalPending=-1;portalSession="";portalsEnabled=false;}
 private static TraceOptions PortalTraceOptions()=>new(){InteractsWith=Contents.Solid|Contents.Window|Contents.PlayerClip|Contents.Player|Contents.Npc|Contents.PhysicsProp|Contents.WorldGeometry|Contents.StaticLevel};
 private bool PortalMessage(JsonElement m,string? type){
  if(type=="welcome"){ClearPortals();cubeEnabled=m.TryGetProperty("cubeEnabled",out var cubeFlag)&&cubeFlag.GetBoolean();portalSession=m.GetProperty("session").GetString()!;portalsEnabled=m.TryGetProperty("portalsEnabled",out var enabled)&&enabled.GetBoolean();return true;}
  if(type==null||!type.StartsWith("portal_"))return false;
  if(type=="portal_notice"){foreach(var c in Utilities.GetPlayers().Where(c=>!c.IsBot))c.PrintToChat("Portal Gun: "+m.GetProperty("reason").GetString());return true;}
  if(m.GetProperty("session").GetString()!=portalSession)return true;
  if(type=="portal_snapshot"){
   var revision=m.GetProperty("revision").GetInt64();var count=m.GetProperty("count").GetInt32();var index=m.GetProperty("index").GetInt32();
   if(revision<=portalRevision||revision<portalPending||count<1||count>6||index<0||index>=count)return true;
   if(revision>portalPending){portalParts.Clear();portalPending=revision;portalCount=count;}if(count!=portalCount)return true;
   portalParts[index]=m.GetProperty("pairs").Clone();if(portalParts.Count!=count)return true;
   foreach(var e in portalMarkers)if(e.IsValid)e.Remove();portalMarkers.Clear();
   foreach(var part in portalParts.Values)foreach(var pair in part.EnumerateArray())foreach(var end in pair.GetProperty("endpoints").EnumerateArray()){
    if(end.ValueKind==JsonValueKind.Null)continue;
    var e=Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");if(e==null)continue;
    e.DispatchSpawn();e.SetModel("models/sourcebox/portals/ring/ring.vmdl");e.UseAnimGraph=false;e.Render=end.GetProperty("endpoint").GetInt32()==0?Color.DodgerBlue:Color.Orange;
    var n=PV(end.GetProperty("normal"));var pos=PV(end.GetProperty("position"));var angles=PortalRingAngles(n);e.Teleport(new Vector(pos.X+n.X,pos.Y+n.Y,pos.Z+n.Z),angles,new Vector(0,0,0));portalMarkers.Add(e);
    Console.WriteLine($"[Sourcebox] portal ring entity={e.Index} position={e.AbsOrigin} normal={n} angles={angles}");
   }propPortalPairs.Clear();foreach(var part in portalParts.Values)foreach(var pair in part.EnumerateArray())propPortalPairs.Add(pair.Clone());CreatePortalPreviews(portalParts.Values,revision);portalRevision=revision;portalParts.Clear();return true;
  }
  if(type!="portal_traverse")return true;
  var id=m.GetProperty("event").GetString()!;if(!portalApplied.Add(id))return true;portalOrder.Enqueue(id);if(portalOrder.Count>128)portalApplied.Remove(portalOrder.Dequeue());
  var pawn=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2)?.PlayerPawn.Value;var applied=false;
  if(m.GetProperty("expiresAt").GetInt64()>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()&&pawn?.IsValid==true&&pawn.Health>0&&Server.MapName=="sourcebox_room_001"){
   var dest=PV(m.GetProperty("position"));var trace=Trace.TraceHullShape(dest,dest,pawn.Collision.Mins,pawn.Collision.Maxs,pawn,PortalTraceOptions());
   // CS2 applies Teleport angles to the player body as well as the view.
   // Keep native gravity upright across wall/floor/ceiling exits. Momentum still
   // receives the full portal rotation; the player receives only exit yaw.
   if(!trace.IsAllSolid&&!trace.DidHit()){var a=m.GetProperty("rotation");pawn.Teleport(dest,new QAngle(0,a[1].GetSingle(),0),PV(m.GetProperty("velocity")));applied=true;}
  }
  Command(new{type="portal_ack",session=portalSession,@event=id,applied});Console.WriteLine($"[Sourcebox] Portal travel {id} applied={applied}");return true;
 }
 private void TickPortals(){
  if(!portalsEnabled||Server.MapName!="sourcebox_room_001")return;
  TickWorldReadiness();
  var c=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2);var p=c?.PlayerPawn.Value;if(p?.IsValid!=true||p.Health<=0)return;
  var weapons=p.WeaponServices;if(weapons==null)return;
  if(portalGun?.IsValid!=true||!weapons.MyWeapons.Any(h=>h.Value?.Index==portalGun.Index)){
   portalGun=weapons.MyWeapons.Select(h=>h.Value).FirstOrDefault(w=>w?.IsValid==true&&w.DesignerName=="weapon_healthshot"&&w.Entity?.Name==PortalWeaponName);
   // Do not replace a healthshot already present in the player's loadout.
   if(portalGun?.IsValid!=true&&!weapons.MyWeapons.Any(h=>h.Value?.DesignerName=="weapon_healthshot")){portalGun=c!.GiveNamedItem<CBasePlayerWeapon>("weapon_healthshot");if(portalGun?.IsValid==true){portalGun.Entity!.Name=PortalWeaponName;portalGun.SetModel("models/sourcebox/portals/world/world.vmdl");c.PrintToChat("Sourcebox Portal Gun: select the extra healthshot inventory entry. Primary/secondary place portals.");}}
  }
  if(portalGun?.IsValid==true){portalGun.NextPrimaryAttackTick=Server.TickCount+128;portalGun.NextSecondaryAttackTick=Server.TickCount+128;Utilities.SetStateChanged(portalGun,"CBasePlayerWeapon","m_nNextPrimaryAttackTick");Utilities.SetStateChanged(portalGun,"CBasePlayerWeapon","m_nNextSecondaryAttackTick");}
  var buttons=c!.Buttons;var pressed=buttons&~portalButtons;portalButtons=buttons;
  if(portalGun?.IsValid!=true||weapons.ActiveWeapon.Value?.Index!=portalGun.Index)return;
  if(SelectDestination(c,pressed))return;
  if((pressed&PlayerButtons.Reload)!=0)Command(new{type="portal_clear",session=portalSession});
  for(var endpoint=0;endpoint<2;endpoint++)if((pressed&(endpoint==0?PlayerButtons.Attack:PlayerButtons.Attack2))!=0){
   var pos=p.AbsOrigin!;var eye=new Vector(pos.X+p.ViewOffset.X,pos.Y+p.ViewOffset.Y,pos.Z+p.ViewOffset.Z);var trace=Trace.TraceShape(eye,p.EyeAngles,p,PortalTraceOptions());
   if(trace.DidHit()&&!trace.IsAllSolid){var hit=trace.EndPos;Command(new{type="portal_fire",session=portalSession,endpoint,destination=ShotDestination(),eye=new[]{eye.X,eye.Y,eye.Z},hit=new[]{hit.X,hit.Y,hit.Z}});}
  }
 }
}



