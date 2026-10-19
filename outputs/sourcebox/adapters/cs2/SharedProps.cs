using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private class SharedProp {public CDynamicProp? Entity; public string Model="",Owner="",Clip="",AppliedClip=""; public long Received;public float[] Position=new float[3],Angles=new float[3];}
 private readonly Dictionary<string,SharedProp> props=new();
 private readonly HashSet<string> missingPropModels=new();
 private void RemoveProp(string id){if(props.Remove(id,out var p)&&p.Entity?.IsValid==true)p.Entity.Remove();}
 private void ClearProps(){foreach(var id in props.Keys.ToArray())RemoveProp(id);}
 private void PutProp(string id,string owner,string model,JsonElement s){
  if(s.TryGetProperty("alive",out var alive)&&!alive.GetBoolean()){RemoveProp(id);return;}
  if(!PropModels.TryGetValue(model,out var converted)){if(missingPropModels.Add(model))Console.WriteLine("[Sourcebox] Shared prop awaits Source 2 conversion: "+model);return;}
  if(!HasLocalModel(converted)){RemoveProp(id);return;}
  if(!props.TryGetValue(id,out var p))props[id]=p=new();if(p.Model!=converted&&p.Entity?.IsValid==true){p.Entity.Remove();p.Entity=null;}p.Model=converted;p.Clip=s.TryGetProperty("sequence",out var clip)?clip.GetString()??"":"";p.Owner=owner;p.Received=Environment.TickCount64;p.Position=s.GetProperty("position").EnumerateArray().Select(v=>v.GetSingle()).ToArray();p.Angles=s.GetProperty("rotation").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
 }
 private void ReadProps(JsonElement m,string type){
  var game=m.TryGetProperty("game",out var g)?g.GetString()!:"cube";
  if(type=="remove"){if(!m.TryGetProperty("scope",out var scope)||scope.GetString()!="player")foreach(var id in props.Where(x=>x.Value.Owner==game).Select(x=>x.Key).ToArray())RemoveProp(id);return;}
  if(type is "cube_state" or "cube_owner"){PutProp("sourcebox_shared_cube","cube","models/props_junk/wood_crate001a.mdl",m.GetProperty("state"));return;}
  if(type=="scene"){sceneSession=m.GetProperty("session").GetString()!;
   var live=m.GetProperty("live").EnumerateArray().Select(v=>"scene:"+v.GetInt32()).ToHashSet();foreach(var id in props.Keys.Where(k=>k.StartsWith("scene:")&&!live.Contains(k)).ToArray())RemoveProp(id);
   foreach(var o in m.GetProperty("objects").EnumerateArray())PutProp("scene:"+o.GetProperty("id").GetInt32(),game,o.GetProperty("model").GetString()!,o);return;
  }
  if(m.TryGetProperty("state",out var s)&&s.TryGetProperty("props",out var list))foreach(var p in list.EnumerateArray()){var id=p.GetProperty("id").GetString()!;PutProp(id,game,id.EndsWith("barrel")?"models/props_c17/oildrum001.mdl":"models/props_junk/wood_crate001a.mdl",p);}
 }
 private void TickProps(long now){if(anchor==null)return;foreach(var id in props.Keys.ToArray()){
  var p=props[id];if(now-p.Received>5000){RemoveProp(id);continue;}bool created=p.Entity?.IsValid!=true;
  if(created){p.Entity=Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");if(p.Entity==null)continue;p.Entity.DispatchSpawn();p.Entity.SetModel(p.Model);p.Entity.UseAnimGraph=false;p.Entity.Collision.SolidType=SolidType_t.SOLID_VPHYSICS;p.Entity.Collision.SolidFlags=0;p.Entity.TakesDamage=true;p.Entity.Health=100000;p.AppliedClip="";p.Entity.Render=id=="sourcebox_shared_cube"?Color.DodgerBlue:Color.White;}
  if(p.Clip!=""&&p.Clip!=p.AppliedClip){p.Entity!.AcceptInput("SetAnimation",value:p.Clip);p.AppliedClip=p.Clip;}
  var target=new Vector(anchor.X+p.Position[0],anchor.Y+p.Position[1],anchor.Z+p.Position[2]);var old=p.Entity!.AbsOrigin;if(!created&&old!=null)target=new Vector(old.X+(target.X-old.X)*0.35f,old.Y+(target.Y-old.Y)*0.35f,old.Z+(target.Z-old.Z)*0.35f);
  p.Entity.Teleport(target,new QAngle(p.Angles[0],p.Angles[1],p.Angles[2]),new Vector(0,0,0));
 }}
}
