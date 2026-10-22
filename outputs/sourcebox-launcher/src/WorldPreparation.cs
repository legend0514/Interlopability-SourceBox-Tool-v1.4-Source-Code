using System.Text.Json;
using System.Text.Json.Nodes;
namespace SourceBox;

public static class WorldPreparation {
 public static readonly Dictionary<string,(string Game,string Map)> Providers=new(){
  ["gm_construct"]=("gmod","garrysmod/maps/gm_construct.bsp"),["gm_flatgrass"]=("gmod","garrysmod/maps/gm_flatgrass.bsp"),
  ["ctf_2fort"]=("tf2","tf/maps/ctf_2fort.bsp"),["portal_triple_laser"]=("portal2","portal2/maps/sp_a2_triple_laser.bsp"),["hl1_frenzy"]=("hl1mp","hl1mp/maps/frenzy.bsp")};
 public static JsonObject ResolveRegistry(string project,IReadOnlyDictionary<string,GameInstall> games){
  var registry=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"sheets/worlds-expanded.json")))!.AsObject();
  var missing=new List<string>();
  foreach(var node in registry["worlds"]!.AsArray()){
   var world=node!.AsObject();var id=world["id"]!.GetValue<string>();
   if(!Providers.TryGetValue(id,out var source))throw new InvalidDataException("Unknown map provider: "+id);
   var path=games.TryGetValue(source.Game,out var game)?Path.Combine(game.Root,source.Map):"";
   if(!File.Exists(path))missing.Add(world["label"]!.GetValue<string>());else world["source"]=Path.GetFullPath(path);
  }
  if(missing.Count>0)throw new IOException("Destination preparation needs these installed maps: "+string.Join(", ",missing)+". The Shared Room remains available. Missing maps cannot be reconstructed without installed content or a prepared local topology cache.");
  registry["enabled"]=true;registry["nativeGeometry"]=true;return registry;
 }
 public static async Task Prepare(Settings settings,GameInstall[] installed,string[] selected,CancellationToken ct,Action<string> log){
  var games=installed.Where(g=>g.Installed).ToDictionary(g=>g.Def.Id);
  var registry=ResolveRegistry(settings.ProjectRoot,games);
  var targets=selected.Distinct().OrderBy(s=>s).ToArray();
  var compilers=new Dictionary<string,string>();var mounts=new List<string>();
  foreach(var game in games.Values){
   if(game.Def.Id=="cs2")continue;
   foreach(var content in new[]{game.Def.Id is "hl2" or "tf2"?game.Def.Id:game.Def.Content,"hl2","hl1"}.Distinct()){
    var folder=Path.Combine(game.Root,content);if(!Directory.Exists(folder))continue;
    mounts.Add(folder);mounts.AddRange(Directory.GetFiles(folder,"*_dir.vpk").Select(p=>p.Replace("_dir.vpk",".vpk",StringComparison.Ordinal)));
   }
  }
  foreach(var id in targets){
   if(!games.ContainsKey(id))throw new IOException("Game is not installed: "+id);
   if(id=="cs2"){
    foreach(var tool in new[]{"resourcecompiler.exe","source1import.exe","dmxconvert.exe"})if(!File.Exists(Path.Combine(games[id].Root,"game/bin/win64",tool)))throw new IOException("Install Counter-Strike 2 Workshop Tools before preparing CS2 destinations.");
    continue;
   }
   var root=id is "portal2" or "hl1mp"?games[id].Root:games.GetValueOrDefault("tf2")?.Root??SdkConfiguration.FindRuntime(settings);
   var compiler=Path.Combine(root,"bin");
   foreach(var tool in new[]{"vbsp","vvis","vrad"})if(!File.Exists(Path.Combine(compiler,tool+".exe")))throw new IOException("Map compiler "+tool+" is missing for "+id+". Install the game's SDK tools through Steam.");
   compilers[id]=compiler;
  }
  string Tool(string name)=>Path.Combine(settings.ProjectRoot,"tools",name);
  var scripts=new[]{"prepare-portable-worlds.py","combine-source-worlds.py","export-worlds.py","verify-world-lighting.py","prepare-portable-cs2-room.ps1","cs2-worlds-portable.cjs","prepare-portable-room-materials.cjs","verify-portable-worlds.mjs"};
  var compilerFiles=compilers.Values.Distinct().SelectMany(folder=>new[]{"vbsp","vvis","vrad"}.Select(n=>Path.Combine(folder,n+".exe"))).ToList();
  if(targets.Contains("cs2"))compilerFiles.AddRange(new[]{"resourcecompiler","source1import","dmxconvert"}.Select(n=>Path.Combine(games["cs2"].Root,"game/bin/win64",n+".exe")));
  var bases=new[]{"maps/hl2/sourcebox_room_001.vmf","maps/tf2/sourcebox_room_001.vmf","maps/portal2/sourcebox_room_001.vmf","sheets/hl1mp.json"}.ToList();
  if(targets.Contains("cs2"))bases.Add("maps/cs2/sourcebox_room_001.vmf");
  var fingerprint=string.Join("",scripts.Select(n=>CrossplaySetup.Hash(Tool(n))))+string.Join("",bases.Select(n=>CrossplaySetup.Hash(Path.Combine(settings.ProjectRoot,n))))+string.Join("",compilerFiles.OrderBy(f=>f).Select(CrossplaySetup.Hash))+string.Join("|",mounts.Distinct().OrderBy(p=>p))+string.Join(",",targets)+registry.ToJsonString()+string.Join("",registry["worlds"]!.AsArray().Select(n=>CrossplaySetup.Hash(n!["source"]!.GetValue<string>())));
  if(settings.LocalAssetCaches.TryGetValue("worlds",out var old)&&SharedAssetPreparation.Valid(old,fingerprint)){log("Prepared destination cache is current.");return;}
  SetupSpace.Require(Settings.Home,8L*1024*1024*1024,"destination map conversion");
  var stage=Path.Combine(Settings.Home,"assets","worlds-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  var output=Path.Combine(stage,"build");var decompiler=await SetupDownloads.BspSource(ct,log);var blender=await SetupDownloads.Blender(ct,log);
  var request=Path.Combine(stage,"request.json");
  File.WriteAllText(request,JsonSerializer.Serialize(new{outputRoot=output,projectRoot=settings.ProjectRoot,registry,targets,java=Path.Combine(decompiler,"bin/java.exe"),compilers,mounts=mounts.Distinct().ToArray(),csRoot=games.GetValueOrDefault("cs2")?.Root,hl2Root=games.GetValueOrDefault("hl2")?.Root??SdkConfiguration.FindRuntime(settings),nodePath=settings.NodePath,powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe")}));
  log("Preparing destination maps locally. This can take a while; compiler progress is shown below.");
  await SharedAssetPreparation.Run(blender,new[]{"-b","--factory-startup","--python-exit-code","1","--python",Tool(scripts[0]),"--",request},Path.Combine(stage,"preparation.log"),ct,log,120);
  if(!File.Exists(Path.Combine(output,"prepared-worlds.json")))throw new IOException("Destination preparation did not produce a validated result.");
  await SharedAssetPreparation.Run(settings.NodePath,new[]{Tool("verify-portable-worlds.mjs"),Path.Combine(output,"prepared-worlds.json")},Path.Combine(stage,"validation.log"),ct,log);
  SharedAssetPreparation.Receipt(stage,fingerprint);settings.LocalAssetCaches["worlds"]=stage;settings.Save();
 }
 public static JsonObject? Prepared(Settings settings){
  if(!settings.LocalAssetCaches.TryGetValue("worlds",out var root))return null;
  var file=Path.Combine(root,"build/prepared-worlds.json");return File.Exists(file)?JsonNode.Parse(File.ReadAllText(file))!.AsObject():null;
 }
 public static void Activate(Settings settings){
  var prepared=Prepared(settings)??throw new IOException("Prepared destinations are missing.");
  var registry=prepared["registry"]!.DeepClone();registry["enabled"]=true;registry["nativeGeometry"]=true;
  var data=Path.Combine(settings.ProjectRoot,"worlds");Directory.CreateDirectory(data);
  foreach(var world in registry["worlds"]!.AsArray()){
   var id=world!["id"]!.GetValue<string>();if(!Providers.ContainsKey(id))throw new InvalidDataException("Unknown destination ID.");
   var target=Path.Combine(data,id+".json");File.Copy(Path.Combine(prepared["metadata"]!.GetValue<string>(),id+".json"),target+".new",true);File.Move(target+".new",target,true);
  }
  foreach(var name in new[]{"worlds.json","worlds-expanded.json"}){
   var file=Path.Combine(settings.ProjectRoot,"sheets",name);var temp=file+".new";
   File.WriteAllText(temp,registry.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,file,true);
  }
 }
}
