using System.Collections.Concurrent;
using System.Drawing;
using System.Net.Sockets;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 : BasePlugin {
 public override string ModuleName => "Sourcebox CS2 Movement Bridge";
 public override string ModuleVersion => "0.0.3";
 private CancellationTokenSource stop=new();
 private readonly ConcurrentQueue<string> incoming=new();
 private readonly Dictionary<string,Peer> peers=new();
 private string? latest;
 private long frame;


 private long protectedUntil;private readonly Dictionary<string,long> lastHits=new();
 private Vector? anchor;
 private const string Model="characters/models/ctm_sas/ctm_sas.vmdl";
 private class Peer {public string Session="";public long Seq,Received;public float[] Position=new float[3];public float Yaw,Speed;public bool Grounded=true;public string ModelKey="",Clip="",Weapon="",WeaponModel="";public CDynamicProp? WeaponEntity;public CDynamicProp? Entity;public CPointWorldText? Label;}
 public override void Load(bool hotReload) {
  Console.WriteLine("[Sourcebox] CS2 movement bridge loaded; converted player models; shared-room identity coordinates when sourcebox_room_001 is loaded");
  RegisterListener<Listeners.OnServerPrecacheResources>(manifest=>{foreach(var kind in new[]{"view","world","ring","monitor"})manifest.AddResource($"models/sourcebox/portals/{kind}/{kind}.vmdl");foreach(var model in PlayerModels.Values)if(HasLocalModel(model.Model))manifest.AddResource(model.Model);foreach(var model in PropModels.Values)if(HasLocalModel(model))manifest.AddResource(model);});
  RegisterListener<Listeners.OnMapStart>(_=>{Clear();anchor=null;AddTimer(2.0f,()=>{ApplyIdlePolicy();ProbePortalRendering();CreateMonitorProbe();});});
  RegisterListener<Listeners.OnTick>(Tick);
  RegisterListener<Listeners.OnServerPrecacheResources>(manifest=>{
   foreach(var model in new[]{Model,"characters/models/tm_phoenix/tm_phoenix.vmdl","models/sourcebox/ragdolls/hl2.vmdl","models/sourcebox/ragdolls/chell.vmdl"})
    if(!model.StartsWith("models/sourcebox/")||HasLocalModel(model))manifest.AddResource(model);
  });
  RegisterListener<Listeners.CheckTransmit>(TransmitPortalPreviews);
  HookEntityOutput("prop_dynamic","OnTakeDamage",(output,name,activator,caller,value,delay)=>{
   if(caller==null||activator==null||!activator.DesignerName.Contains("player"))return HookResult.Continue;
   foreach(var peer in peers)if(peer.Value.Entity?.IsValid==true&&peer.Value.Entity.Index==caller.Index){var tick=Server.TickCount;if(!lastHits.TryGetValue(peer.Key,out var previous)||previous!=tick){lastHits[peer.Key]=tick;Command(new{type="player_hit",target=peer.Key});}return HookResult.Continue;}
   foreach(var entry in props)if(entry.Key.StartsWith("scene:")&&entry.Value.Entity?.IsValid==true&&entry.Value.Entity.Index==caller.Index){Command(new{type="scene_interact",id=int.Parse(entry.Key[6..]),session=sceneSession,action="hit"});break;}
   return HookResult.Continue;
  });
  
  AddTimer(3.0f,ApplyIdlePolicy);
  _=Task.Run(NetworkLoop);
 }
 private void ApplyIdlePolicy(){
  if(Server.MapName!="sourcebox_room_001")return;
  // Game-mode configs load after command-line cvars; apply after map startup.
  Server.ExecuteCommand("mp_autokick 0; mp_autokick");
  Console.WriteLine("[Sourcebox] Idle kick disabled for local crossplay");
 }
 private void Clear(){ClearRagdolls();ClearPortals();ClearCube();ClearProps();foreach(var id in peers.Keys.ToArray())Remove(id);}
 private void Remove(string id){if(!peers.Remove(id,out var p))return;if(p.WeaponEntity?.IsValid==true)p.WeaponEntity.Remove();if(p.Entity?.IsValid==true)p.Entity.Remove();if(p.Label?.IsValid==true)p.Label.Remove();}
 private void Tick(){
  var now=Environment.TickCount64;
  for(int i=0;i<64&&incoming.TryDequeue(out var line);i++){
   try{using var doc=JsonDocument.Parse(line);var m=doc.RootElement;var type=m.GetProperty("type").GetString();
    if(PortalMessage(m,type))continue;
    if(type=="disconnect"){Clear();continue;}
    if(type=="player_ragdoll"){DeathMessage(m);continue;}
    if(type=="player_damage"){ApplyPlayerDamage(m);continue;}
    if(CubeMessage(m,type!))continue;
    ReadProps(m,type!);
    if(type=="remove"){Remove(m.GetProperty("game").GetString()!);continue;}
    if(type!="remote")continue;
    var id=m.GetProperty("game").GetString()!;if(!new[]{"hl2","tf2","gmod","hl1mp","portal2"}.Contains(id))continue;
    var session=m.GetProperty("session").GetString()!;var s=m.GetProperty("state");var seq=s.GetProperty("seq").GetInt64();
    if(peers.TryGetValue(id,out var old)&&old.Session!=session)Remove(id);
    if(!peers.TryGetValue(id,out var p))peers[id]=p=new Peer{Session=session};if(seq<=p.Seq)continue;
    var key=id=="gmod"&&s.TryGetProperty("model",out var sourceModel)&&sourceModel.GetString()=="models/goblin/playerm/goblin.mdl"?"goblin":id;if(key=="goblin"&&!HasLocalModel(PlayerModels[key].Model))key="gmod";if(p.ModelKey!=key){if(p.Entity?.IsValid==true)p.Entity.Remove();if(p.WeaponEntity?.IsValid==true)p.WeaponEntity.Remove();if(p.Label?.IsValid==true)p.Label.Remove();p.Label=null;p.Entity=null;p.WeaponEntity=null;p.Clip="";p.ModelKey=key;}
    p.Seq=seq;p.Received=now;p.Weapon=s.TryGetProperty("weapon",out var held)?held.GetString()??"":"";p.Grounded=!s.TryGetProperty("grounded",out var grounded)||grounded.GetBoolean();p.Position=s.GetProperty("position").EnumerateArray().Select(v=>v.GetSingle()).ToArray();p.Yaw=s.GetProperty("rotation")[1].GetSingle();var vel=s.GetProperty("velocity");p.Speed=MathF.Sqrt(vel[0].GetSingle()*vel[0].GetSingle()+vel[1].GetSingle()*vel[1].GetSingle());
   }catch(Exception ex){Console.WriteLine("[Sourcebox] packet rejected: "+ex.Message);}
  }
  var pawn=Utilities.GetPlayers().FirstOrDefault(p=>p.IsValid&&!p.IsBot&&p.TeamNum>=2&&p.PlayerPawn.IsValid)?.PlayerPawn.Value;
  if(pawn!=null&&pawn.IsValid&&pawn.Health>0&&pawn.AbsOrigin!=null){
   var p=pawn.AbsOrigin;var a=pawn.EyeAngles;var v=pawn.AbsVelocity;
   if(anchor==null){anchor=Server.MapName=="sourcebox_room_001"?new Vector(0,0,0):new Vector(p.X,p.Y,p.Z);Console.WriteLine($"[Sourcebox] coordinate anchor {p.X} {p.Y} {p.Z}");}
   if(++frame%4==0)Volatile.Write(ref latest,JsonSerializer.Serialize(new {type="state",entity="player:cs2",health=pawn.Health,position=new[]{p.X-anchor.X,p.Y-anchor.Y,p.Z-anchor.Z},rotation=new[]{a.X,a.Y,a.Z},velocity=new[]{v.X,v.Y,v.Z},weapon=portalGun?.IsValid==true&&pawn.WeaponServices?.ActiveWeapon.Value?.Index==portalGun.Index?"weapon_sourcebox_portalgun":"",grounded=(pawn.Flags&1)!=0,team=(int)pawn.TeamNum}));
  }else Volatile.Write(ref latest,null);
  TickPortals();

  TickMomentumProbe();
  TickWorldTravelProbe();
  TickInteraction(now);
  TickProps(now);
  foreach(var id in peers.Keys.ToArray()){
   var p=peers[id];if(now-p.Received>2000){Remove(id);continue;}if(anchor==null)continue;
   if(p.Entity?.IsValid!=true){
    var e=Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");if(e==null)continue;
    e.DispatchSpawn();e.SetModel(PlayerModels[p.ModelKey].Model);e.UseAnimGraph=false;e.Render=Color.White;
    e.Collision.SolidType=SolidType_t.SOLID_BBOX;e.Collision.SolidFlags=0;e.Collision.CollisionGroup=0;e.TakesDamage=true;e.Health=100000;e.Collision.Mins.X=-16;e.Collision.Mins.Y=-16;e.Collision.Mins.Z=0;e.Collision.Maxs.X=16;e.Collision.Maxs.Y=16;e.Collision.Maxs.Z=72;
    e.AcceptInput("EnableCollision");p.Entity=e;
    var label=Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");if(label!=null){label.MessageText=id.ToUpper()+" PLAYER";label.FontSize=24;label.Enabled=true;label.DispatchSpawn();p.Label=label;}
    Console.WriteLine("[Sourcebox] spawned converted player for "+id+" model="+p.ModelKey);
   }
   {var model=PlayerModels[p.ModelKey];var clip=!p.Grounded?model.Air:p.Speed>150?model.Run:p.Speed>8?model.Walk:model.Idle;if(p.Clip!=clip){p.Entity.AcceptInput("SetAnimation",value:clip);p.Clip=clip;}}
   var target=new Vector(anchor.X+p.Position[0],anchor.Y+p.Position[1],anchor.Z+p.Position[2]);var old=p.Entity.AbsOrigin;
   if(old!=null&&now-p.Received<500)target=new Vector(old.X+(target.X-old.X)*0.3f,old.Y+(target.Y-old.Y)*0.3f,old.Z+(target.Z-old.Z)*0.3f);
   p.Entity.Teleport(target,new QAngle(0,p.Yaw,0),new Vector(0,0,0));
   UpdateHeldWeapon(id,p);
   if(p.Label?.IsValid==true)p.Label.Teleport(new Vector(target.X,target.Y,target.Z+90),new QAngle(0,p.Yaw,0),new Vector(0,0,0));
  }
 }
 private void ApplyPlayerDamage(JsonElement message){
  var now=Environment.TickCount64;if(now<protectedUntil)return;
  var pawn=Utilities.GetPlayers().FirstOrDefault(c=>c.IsValid&&!c.IsBot&&c.TeamNum>=2)?.PlayerPawn.Value;
  if(pawn?.IsValid!=true||pawn.Health<=0)return;
  var damage=Math.Clamp(message.GetProperty("amount").GetInt32(),0,100);
  if(damage>=pawn.Health){Command(new{type="player_death"});pawn.Health=Math.Max(100,pawn.MaxHealth);pawn.Teleport(new Vector(320,pawn.TeamNum==2?-128:128,8),new QAngle(0,180,0),new Vector(0,0,0));protectedUntil=now+1000;}else pawn.Health-=damage;
  Utilities.SetStateChanged(pawn,"CBaseEntity","m_iHealth");
 }
 private async Task NetworkLoop(){
  while(!stop.IsCancellationRequested){try{
   using var client=new TcpClient();await client.ConnectAsync("127.0.0.1",27841,stop.Token);
   using var stream=client.GetStream();using var reader=new StreamReader(stream);using var writer=new StreamWriter(stream){AutoFlush=true};
   await writer.WriteLineAsync(JsonSerializer.Serialize(new{type="hello",protocol=1,game="cs2",pid=Environment.ProcessId,cubeProtocol=1,portalProtocol=1,portalTraverse=true}));
   var welcome=await reader.ReadLineAsync(stop.Token);if(welcome==null)throw new IOException("No welcome");
   incoming.Enqueue(welcome);Console.WriteLine("[Sourcebox] Core connected "+welcome);
   var drain=Task.Run(async()=>{while(!stop.IsCancellationRequested){var line=await reader.ReadLineAsync(stop.Token);if(line==null)break;using(var parsed=JsonDocument.Parse(line)){var kind=parsed.RootElement.GetProperty("type").GetString();if(!(kind?.StartsWith("portal_")??false)&&kind!="player_ragdoll"&&kind!="player_damage"&&kind!="remote"&&kind!="remove"&&kind!="physics"&&kind!="scene"&&kind!="cube_state"&&kind!="cube_owner"&&kind!="cube_freeze"&&kind!="cube_denied")continue;}if(incoming.Count>=256)throw new IOException("Incoming queue exceeded");incoming.Enqueue(line);}},stop.Token);
   long seq=0;
   while(!stop.IsCancellationRequested&&!drain.IsCompleted){
    for(int n=0;n<32&&commands.TryDequeue(out var command);n++){var cmd=JsonSerializer.Deserialize<Dictionary<string,object>>(command)!;cmd["seq"]=++seq;cmd["timestamp"]=Environment.TickCount64;await writer.WriteLineAsync(JsonSerializer.Serialize(cmd));}
    var state=Volatile.Read(ref latest);var packet=state==null?new Dictionary<string,object> { ["type"]="heartbeat" }:JsonSerializer.Deserialize<Dictionary<string,object>>(state)!;
    packet["seq"]=++seq;packet["timestamp"]=Environment.TickCount64;
    await writer.WriteLineAsync(JsonSerializer.Serialize(packet));await Task.Delay(50,stop.Token);
   }
  }catch(Exception ex){if(!stop.IsCancellationRequested)Console.WriteLine("[Sourcebox] reconnect: "+ex.Message);}
   incoming.Enqueue("{\"type\":\"disconnect\"}");
   try{await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){}
  }
 }
 public override void Unload(bool hotReload){stop.Cancel();Clear();}
}








