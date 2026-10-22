using System.Security.Cryptography;
using System.Text.Json;
namespace SourceBox;

public sealed record SetupCopy(string Source,string Root,string Relative,string Sha256);
public sealed record SetupPlan(string Game,List<SetupCopy> Files,List<string> Missing);
public static class CrossplaySetup {
 public static string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream));}
 // Resolve every destination against a discovered game root, including existing parents.
 public static string Destination(string root,string relative){
  var target=DesktopInstall.Resolve(root,relative);
  for(var p=new DirectoryInfo(Path.GetDirectoryName(target)!);p!=null;p=p.Parent){
   if(p.Exists&&(p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Setup will not write through a linked folder: "+p.FullName);
   if(p.FullName.Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))break;
  }
  if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Setup will not replace a linked file.");
  return target;
 }
 internal static bool KnownOldRoom(Settings settings,string game,string installed){
  var manifest=Path.Combine(settings.ProjectRoot,"sheets/room-upgrades.json");
  if(!File.Exists(installed)||!File.Exists(manifest))return false;
  try{
   using var doc=JsonDocument.Parse(File.ReadAllText(manifest));
   if(doc.RootElement.GetProperty("version").GetInt32()!=1||!doc.RootElement.GetProperty("games").TryGetProperty(game,out var versions))return false;
   var size=new FileInfo(installed).Length;
   var candidates=versions.EnumerateArray().Where(v=>v.GetProperty("bytes").GetInt64()==size).ToArray();
   if(candidates.Length==0)return false;
   var hash=Hash(installed);return candidates.Any(v=>string.Equals(v.GetProperty("sha256").GetString(),hash,StringComparison.OrdinalIgnoreCase));
  }catch(Exception e)when(e is IOException or JsonException or InvalidOperationException or KeyNotFoundException){return false;}
 }
 public static SetupPlan Plan(Settings settings,GameInstall game){
  var files=new List<SetupCopy>();var missing=new List<string>();var root=game.IdRoot();var content=game.Def.Content;
  void Need(string path,string label){if(!File.Exists(path))missing.Add(label);}
  void Copy(string source,string relative,bool preserve=false){
   var target=Destination(root,relative);
   if(preserve&&File.Exists(target))return;
   if(!File.Exists(source)){missing.Add("SourceBox file: "+Path.GetRelativePath(settings.ProjectRoot,source));return;}
   var hash=Hash(source);if(File.Exists(target)&&Hash(target)==hash)return;
   files.Add(new(source,root,relative,hash));
  }
  string Project(string relative)=>Path.Combine(settings.ProjectRoot,relative);
  if(!game.Installed)missing.Add("Install "+game.Def.Title+" through Steam.");
  Need(game.Exe,"Game runtime: "+game.Def.Title);
  if(game.Def.Id is "hl2" or "tf2"){
   if(!File.Exists(Path.Combine(root,content,"gameinfo.txt"))){
    var engine=SdkConfiguration.FindRuntime(settings);
    if(engine.Length==0)missing.Add("Install Source SDK Base 2013 Multiplayer (Steam app 243750).");
    else{
     Copy(SdkConfiguration.Generated(SdkConfiguration.GameInfo(game.Def.Id,game.Root,engine,SdkConfiguration.AssetMounts(settings,game.Def.Id))),content+"/gameinfo.txt");
     Copy(Path.Combine(engine,"bin/x64/steam_api64.dll"),"bin/x64/steam_api64.dll",true);
     Copy(SdkConfiguration.Generated("243750\n"),"steam_appid.txt",true);
     Copy(SdkConfiguration.Generated("exec config.cfg\nexec sourcebox_controls.cfg\nstuffcmds\n"),content+"/cfg/valve.rc",true);
    }
   }
   var controls="alias +sourcebox_use \"+use; cmd sourcebox_take\"\nalias -sourcebox_use \"-use\"\nbind e +sourcebox_use\nbind F6 \"cmd sourcebox_resetprops\"\n";
   if(game.Def.Id=="tf2")controls+="cl_hud_playerclass_playermodel_showed_confirm_dialog 1\ncl_hud_playerclass_use_playermodel 0\n";
   Copy(SdkConfiguration.Generated(controls),content+"/cfg/sourcebox_controls.cfg");
   if(game.Def.Id=="hl2"&&!File.Exists(Path.Combine(root,content,"cfg/sourcebox_loadout.cfg"))){
    using var gameplay=JsonDocument.Parse(File.ReadAllText(Project("sheets/hl2-gameplay.json")));
    var commands=gameplay.RootElement.GetProperty("commands").EnumerateArray().Select(v=>v.GetString()).Concat(gameplay.RootElement.GetProperty("bindings").EnumerateObject().Select(p=>"bind \""+p.Name+"\" \""+p.Value.GetString()+"\""));
    Copy(SdkConfiguration.Generated(string.Join("\n",commands)+"\n"),content+"/cfg/sourcebox_loadout.cfg",true);
   }
   foreach(var name in new[]{"client.dll","server.dll"})Need(Path.Combine(root,content,"bin/x64",name),"SDK "+name);
  }
  if(game.Def.Id=="cs2"){
   var worldHost=WorldPreparation.Prepared(settings)?["hosts"]?["cs2"];
   foreach(var dependency in new[]{("metamod","addons/metamod.vdf"),("counterstrikesharp","addons/counterstrikesharp/api/CounterStrikeSharp.API.dll")}){
    if(File.Exists(Path.Combine(root,"game/csgo",dependency.Item2)))continue;
    if(settings.DependencyRoots.TryGetValue(dependency.Item1,out var prepared)&&File.Exists(Path.Combine(prepared,dependency.Item2))){
     foreach(var source in Directory.GetFiles(Path.Combine(prepared,"addons"),"*",SearchOption.AllDirectories))Copy(source,"game/csgo/"+Path.GetRelativePath(prepared,source));
    }else missing.Add("Prepared "+dependency.Item1+" dependency");
   }
   if(settings.LocalAssetCaches.TryGetValue("cs2",out var csCache)&&File.Exists(Path.Combine(csCache,"maps/sourcebox_room_001.vpk"))){
    foreach(var folder in new[]{"maps","materials","models","lighting"}){var input=Path.Combine(csCache,folder);if(Directory.Exists(input))foreach(var source in Directory.GetFiles(input,"*",SearchOption.AllDirectories))Copy(source,"game/csgo/"+folder+"/"+Path.GetRelativePath(input,source),folder=="maps");}
   }else Need(Path.Combine(root,"game/csgo/maps/sourcebox_room_001.vpk"),"Locally compiled Source 2 room and converted assets");
   Copy(Project("adapters/cs2/bin/Release/net10.0/SourceboxCS2.dll"),"game/csgo/addons/counterstrikesharp/plugins/SourceboxCS2/SourceboxCS2.dll");
   Copy(Project("adapters/cs2/bin/Release/net10.0/SourceboxCS2.deps.json"),"game/csgo/addons/counterstrikesharp/plugins/SourceboxCS2/SourceboxCS2.deps.json");
   if(worldHost!=null){
    var map=worldHost["map"]!.GetValue<string>();
    if(!File.Exists(map)||!Hash(map).Equals(worldHost["sha256"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase))missing.Add("CS2 destination map checksum mismatch");
    else{
     // Replace a pending baseline map copy with the validated world map.
     files.RemoveAll(f=>f.Relative=="game/csgo/maps/sourcebox_room_001.vpk");
     foreach(var folder in new[]{"maps","models","materials","lighting"}){var source=Path.Combine(worldHost["content"]!.GetValue<string>(),folder);if(!Directory.Exists(source))continue;foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){var relative="game/csgo/"+folder+"/"+Path.GetRelativePath(source,file);files.RemoveAll(f=>f.Relative==relative);Copy(file,relative);}}
    }
   }
  }else{
   // Update only positively identified old first-party rooms; preserve custom/multiverse maps.
   var preparedWorlds=WorldPreparation.Prepared(settings);
   var preparedHost=preparedWorlds?["hosts"]?[game.Def.Id];
   if(preparedHost!=null){
    var map=preparedHost["map"]!.GetValue<string>();
    if(!File.Exists(map)||!Hash(map).Equals(preparedHost["sha256"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase))missing.Add("Destination map checksum mismatch: "+game.Def.Id);
    else Copy(map,content+"/maps/sourcebox_room_001.bsp");
   }else if(!File.Exists(Path.Combine(root,content,"maps/sourcebox_room_001.bsp"))||KnownOldRoom(settings,game.Def.Id,Path.Combine(root,content,"maps/sourcebox_room_001.bsp"))){
    var hosts=Project("sheets/native-world-hosts.json");string map=Project("maps/"+game.Def.Id+"/sourcebox_room_001.bsp");
    if(File.Exists(hosts)){using var doc=JsonDocument.Parse(File.ReadAllText(hosts));if(doc.RootElement.TryGetProperty(game.Def.Id,out var host)&&host.TryGetProperty("enabled",out var enabled)&&enabled.GetBoolean())map=host.GetProperty("map").GetString()!;}
    var worlds=Project("sheets/worlds.json");
    if(game.Def.Id=="gmod"&&File.Exists(worlds)){using var doc=JsonDocument.Parse(File.ReadAllText(worlds));if(doc.RootElement.GetProperty("enabled").GetBoolean())missing.Add("Prepared GMod world host map and its local asset mounts");}
    Copy(map,content+"/maps/sourcebox_room_001.bsp");
   }
   if(game.Def.Id=="gmod"){
    if(preparedHost!=null){
     Copy(SdkConfiguration.Generated(preparedWorlds!["registry"]!.ToJsonString()),content+"/data/sourcebox/worlds.json");
     foreach(var metadata in Directory.GetFiles(preparedWorlds["metadata"]!.GetValue<string>(),"*.metadata.json"))Copy(metadata,content+"/data/sourcebox/worlds/"+Path.GetFileName(metadata));
    }
    var dependencyTarget=Path.Combine(root,content,"addons/sourcebox_portal_dependency/lua/entities/prop_portal/init.lua");
    if(!File.Exists(dependencyTarget)){
     if(settings.DependencyRoots.TryGetValue("portal-addon",out var addon)&&File.Exists(Path.Combine(addon,"lua/entities/prop_portal/init.lua"))){
      foreach(var folder in new[]{"lua","models","materials","sound","particles"}){var source=Path.Combine(addon,folder);if(Directory.Exists(source))foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))Copy(file,content+"/addons/sourcebox_portal_dependency/"+folder+"/"+Path.GetRelativePath(source,file));}
     }else missing.Add("Installed Portal Gun successor addon (Workshop 1800764828)");
    }
    foreach(var cache in settings.LocalAssetCaches.Where(p=>p.Key!="cs2"&&p.Key!="fallback")){foreach(var folder in new[]{"models","materials"}){var source=Path.Combine(cache.Value,folder);if(Directory.Exists(source))foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))Copy(file,content+"/addons/sourcebox_assets_"+cache.Key+"/"+folder+"/"+Path.GetRelativePath(source,file));}}

    var lua=Project("adapters/gmod/lua");
    if(Directory.Exists(lua))foreach(var file in Directory.GetFiles(lua,"*.lua",SearchOption.AllDirectories))Copy(file,content+"/addons/sourcebox_local/lua/"+Path.GetRelativePath(lua,file));else missing.Add("GMod Lua adapter payload");
    foreach(var pair in new[]{("asset-references","assets"),("gmod-scene","scene"),("physics-objects","physics"),("portals","portals"),("cs2","cs2"),("hl1mp","hl1mp")})Copy(Project("sheets/"+pair.Item1+".json"),content+"/data/sourcebox/"+pair.Item2+".json");
   }else{
    var prefix=game.Def.Id is "hl2" or "tf2"?"bin/sdk/":"bin/";
    Copy(Project(prefix+"sourcebox_"+game.Def.Id+".dll"),content+"/addons/sourcebox_"+game.Def.Id+".dll");
    if(game.Def.Id=="tf2")Copy(Project("adapters/tf2/sourcebox_portals.nut"),content+"/scripts/vscripts/sourcebox_portals.nut");
    if(game.Def.Id=="hl1mp"){
     Copy(Project("adapters/hl1mp/sourcebox_prop_portals.nut"),content+"/scripts/vscripts/sourcebox_prop_portals.nut");
     Copy(Project("bin/sourcebox_preview.dll"),"bin/x64/tools/sourcebox_preview.dll");
     var registry=Path.Combine(root,"bin/x64/enginetools.txt");
     if(!File.Exists(registry)||!File.ReadAllText(registry).Contains("tools/sourcebox_preview.dll")){
      var text=File.Exists(registry)?File.ReadAllText(registry):"\"enginetools\" { }";var end=text.LastIndexOf('}');
      if(end<0)missing.Add("Malformed HL1 engine tool registry");else Copy(SdkConfiguration.Generated(text.Insert(end,"\n\"library\" \"tools/sourcebox_preview.dll\"\n")),"bin/x64/enginetools.txt");
     }
    }
   }
  }
  if(game.Def.Id is "hl1mp" or "portal2" or "hl2" or "tf2" or "gmod" && settings.LocalAssetCaches.Count>0){
   var info=Path.Combine(root,content,"gameinfo.txt");
   if(!File.Exists(info)){if(game.Def.Id is "hl1mp" or "portal2" or "gmod")missing.Add("Native gameinfo.txt");}
   else{
    var mounts=SdkConfiguration.AssetMounts(settings,game.Def.Id).ToList();
    var worlds=WorldPreparation.Prepared(settings);
    if(worlds?["hosts"]?[game.Def.Id]!=null){mounts.Insert(0,worlds["assets"]!.GetValue<string>());mounts.AddRange(worlds["mounts"]!.AsArray().Select(p=>p!.GetValue<string>()));}
    var text=File.ReadAllText(info);var updated=SdkConfiguration.WithAssetMounts(text,mounts);
    if((game.Def.Id is "hl2" or "tf2")&&text.Contains("game \"SourceBox\"",StringComparison.Ordinal)){
     var engine=SdkConfiguration.FindRuntime(settings);
     if(engine.Length>0)updated=SdkConfiguration.GameInfo(game.Def.Id,game.Root,engine,mounts);
    }
    if(updated!=text)Copy(SdkConfiguration.Generated(updated),content+"/gameinfo.txt");
   }
  }
  missing.AddRange(ReleaseRequirements.Missing(settings,game.Def.Id,Path.Combine(root,content)));
  return new(game.Def.Id,files,missing.Distinct().ToList());
 }
 public static void Apply(IEnumerable<SetupPlan> plans,string backupRoot,CancellationToken ct,Func<string,bool> running,Action<string> log){
  var all=plans.ToArray();var missing=all.SelectMany(p=>p.Missing.Select(m=>p.Game+": "+m)).ToArray();
  if(missing.Length>0)throw new InvalidOperationException("Crossplay setup needs these components before any files can be changed:\n"+string.Join("\n",missing));
  foreach(var p in all)if(running(p.Game))throw new IOException("Close "+p.Game+" before installing its adapter. It was left running.");
  var targets=new Dictionary<string,SetupCopy>(StringComparer.OrdinalIgnoreCase);
  foreach(var f in all.SelectMany(p=>p.Files)){
   ct.ThrowIfCancellationRequested();var target=Destination(f.Root,f.Relative);
   if(Hash(f.Source)!=f.Sha256)throw new IOException("Setup source changed after validation.");
   if(targets.TryGetValue(target,out var prior)&&prior.Sha256!=f.Sha256)throw new IOException("Conflicting setup destination: "+f.Relative);
   targets.TryAdd(target,f);
  }
  var copies=targets.Values.ToArray();
  SetupSpace.Check(SetupSpace.Required(copies,backupRoot),"crossplay installation and backups");
  Directory.CreateDirectory(backupRoot);var changed=new List<(string Target,string? Backup)>();var writtenHashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  File.WriteAllText(Path.Combine(backupRoot,"plan.json"),JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));
  try{
   foreach(var f in copies){
    ct.ThrowIfCancellationRequested();foreach(var p in all)if(running(p.Game))throw new IOException("A selected game started during setup.");
    var target=Destination(f.Root,f.Relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    string? backup=null;if(File.Exists(target)){backup=Path.Combine(backupRoot,changed.Count+".original");File.Copy(target,backup,false);}
    changed.Add((target,backup));File.WriteAllText(Path.Combine(backupRoot,"changes.json"),JsonSerializer.Serialize(changed.Select(x=>new{x.Target,x.Backup})));
    var stage=target+".sourcebox-"+Guid.NewGuid().ToString("N");
    try{File.Copy(f.Source,stage);if(Hash(stage)!=f.Sha256)throw new IOException("Copied adapter failed verification.");File.Move(stage,target,true);writtenHashes[target]=f.Sha256;}finally{if(File.Exists(stage))File.Delete(stage);}
    log("Installed "+f.Relative);
   }
   File.WriteAllText(Path.Combine(backupRoot,"complete.txt"),DateTime.UtcNow.ToString("O"));
  }catch(Exception original){
   var errors=new List<Exception>{original};foreach(var item in changed.AsEnumerable().Reverse())try{
    if(!writtenHashes.TryGetValue(item.Target,out var expected))continue;
    var definition=targets[item.Target];Destination(definition.Root,definition.Relative);
    if(!File.Exists(item.Target)||Hash(item.Target)!=expected)throw new IOException("Rollback preserved a newer or removed file: "+item.Target);
    if(item.Backup!=null)File.Copy(item.Backup,item.Target,true);else File.Delete(item.Target);
   }catch(Exception restore){errors.Add(restore);}
   if(errors.Count>1)throw new AggregateException("Setup failed and some files could not be restored. Backups: "+backupRoot,errors);throw;
  }
 }
}
