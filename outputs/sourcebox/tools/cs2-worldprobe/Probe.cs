using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public class SourceboxWorldProbe:BasePlugin {
 public override string ModuleName=>"Sourcebox isolated world collision probe";
 public override string ModuleVersion=>"0.1";
 private int index=-1;private bool attempted,playerStarted;private long started,placed;private readonly List<object> results=new();
 private JsonElement[] worlds=Array.Empty<JsonElement>();
 public override void Load(bool hotReload){
  Console.WriteLine("[WorldProbe] Loaded map="+Server.MapName+" hotReload="+hotReload);
  var request=Path.Combine(ModuleDirectory,"start.txt");
  if(File.Exists(request)){File.Delete(request);Server.ExecuteCommand("sv_hibernate_when_empty 0; changelevel sourcebox_worlds_probe");}
  RegisterListener<Listeners.OnMapStart>(map=>{if(map=="sourcebox_worlds_probe")AddTimer(3,Begin);});
  RegisterListener<Listeners.OnTick>(Tick);
  if(Server.MapName=="sourcebox_worlds_probe"){
   Server.ExecuteCommand("sv_hibernate_when_empty 0");
   AddTimer(3,Begin);
  }
 }
 private void Begin(){
  if(Server.MapName!="sourcebox_worlds_probe"||attempted)return;
  attempted=true;
  using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(ModuleDirectory,"worlds.json")));
  worlds=data.RootElement.EnumerateArray().Select(e=>e.Clone()).ToArray();
  index=0;started=Environment.TickCount64;placed=0;results.Clear();
  Server.ExecuteCommand("sv_cheats 1; mp_warmup_pausetimer 1; mp_warmuptime 9999; mp_warmup_start; mp_ignore_round_win_conditions 1; mp_limitteams 0; mp_autoteambalance 0; bot_join_after_player 0; bot_quota 1; bot_stop 1; bot_add_t");
  Console.WriteLine("[WorldProbe] Begin native bot collision test");
 }
 private void Finish(string status){
  File.WriteAllText(Path.Combine(ModuleDirectory,"result.json"),JsonSerializer.Serialize(new{status,results,nativeMap=Server.MapName,portalTraversalTested=false}));
  Console.WriteLine("[WorldProbe] Finished "+status);index=-1;
  Server.ExecuteCommand("bot_kick; bot_quota 0; bot_join_after_player 1; bot_stop 0; mp_warmup_pausetimer 0; mp_ignore_round_win_conditions 0; sv_cheats 0; changelevel sourcebox_room_001");
 }
 private void Tick(){
  if(!attempted&&Server.MapName=="sourcebox_worlds_probe")Begin();
  if(index<0||Server.MapName!="sourcebox_worlds_probe")return;
  var now=Environment.TickCount64;
  if(now-started>120000){Finish("timeout");return;}
  var controller=Utilities.GetPlayers().Where(p=>p.IsValid).OrderByDescending(p=>p.IsBot).FirstOrDefault();
  if(controller!=null&&controller.TeamNum<2){controller.ChangeTeam(CsTeam.Terrorist);controller.Respawn();return;}
  var bot=controller?.PlayerPawn.Value;
  if(bot==null||!bot.IsValid||bot.Health<=0||bot.AbsOrigin==null)return;
  if(!playerStarted){playerStarted=true;started=now;}
  if(index>=worlds.Length){Finish("completed");return;}
  var w=worlds[index];var spawn=w.GetProperty("spawn");
  if(placed==0){bot.Teleport(new Vector(spawn[0].GetSingle(),spawn[1].GetSingle(),spawn[2].GetSingle()+8),new QAngle(0,w.GetProperty("yaw").GetSingle(),0),new Vector());placed=now;return;}
  if(now-placed<8000)return;
  var p=bot.AbsOrigin;var v=bot.AbsVelocity;bool ground=(bot.Flags&1)!=0;
  bool near=Math.Abs(p.X-spawn[0].GetSingle())<64&&Math.Abs(p.Y-spawn[1].GetSingle())<64&&Math.Abs(p.Z-spawn[2].GetSingle())<192;
  var result=new{id=w.GetProperty("id").GetString(),actor=controller!.IsBot?"native bot":"native client player",position=new[]{p.X,p.Y,p.Z},velocity=new[]{v.X,v.Y,v.Z},grounded=ground,nearSpawn=near,passed=ground&&near&&Math.Abs(v.Z)<1};
  results.Add(result);Console.WriteLine("[WorldProbe] "+JsonSerializer.Serialize(result));index++;placed=0;
 }
}
