using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private readonly Queue<CBaseEntity> deathBodies=new();
 private readonly HashSet<string> deathEvents=new();
 private readonly Queue<string> deathEventOrder=new();
 private void ClearRagdolls(){while(deathBodies.TryDequeue(out var body))if(body.IsValid)body.Remove();deathEvents.Clear();deathEventOrder.Clear();}
 private void DeathMessage(JsonElement m){
  var key=m.GetProperty("event").GetString()!;if(!deathEvents.Add(key))return;
  deathEventOrder.Enqueue(key);while(deathEventOrder.Count>128)deathEvents.Remove(deathEventOrder.Dequeue());
  var id=m.GetProperty("game").GetString()!;var s=m.GetProperty("state");string model;
  if(id=="cs2")model=s.TryGetProperty("team",out var team)&&team.GetInt32()==2?"characters/models/tm_phoenix/tm_phoenix.vmdl":Model;
  else if(peers.TryGetValue(id,out var peer)&&PlayerModels.TryGetValue(peer.ModelKey,out var asset))model=asset.Model;
  else return;
  var prepared=id=="hl2"?"models/sourcebox/ragdolls/hl2.vmdl":id=="portal2"?"models/sourcebox/ragdolls/chell.vmdl":null;
  if(prepared!=null&&HasLocalModel(prepared))model=prepared;
  var body=Utilities.CreateEntityByName<CRagdollProp>("prop_ragdoll");if(body==null)return;
  var p=s.GetProperty("position");var v=s.GetProperty("velocity");
  using(var keys=new CEntityKeyValues()){
   keys.SetString("model",model);keys.SetInt("spawnflags",4);
   keys.SetVector("origin",p[0].GetSingle()+(anchor?.X??0),p[1].GetSingle()+(anchor?.Y??0),p[2].GetSingle()+(anchor?.Z??0));
   keys.SetAngle("angles",0,s.GetProperty("rotation")[1].GetSingle(),0);
   body.DispatchSpawn(keys);
  }
  if(!body.IsValid)return;
  var bones=body.Ragdoll.List.Count;
  if(bones==0){Console.WriteLine($"[Sourcebox] Ragdoll physics unavailable: {id} {model}");body.Remove();return;}
  body.Teleport(null,null,new Vector(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle()));
  deathBodies.Enqueue(body);while(deathBodies.Count>m.GetProperty("maxBodies").GetInt32()){var old=deathBodies.Dequeue();if(old.IsValid)old.Remove();}
  AddTimer(m.GetProperty("lifetimeMs").GetSingle()/1000,()=>{if(body.IsValid)body.Remove();});
  Console.WriteLine($"[Sourcebox] death body {id}: {model}; physics bones={bones}");
 }
}
