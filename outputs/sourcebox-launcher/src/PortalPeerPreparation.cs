using System.Text.Json;
namespace SourceBox;

public static class PortalPeerPreparation {
 public static async Task Prepare(Settings settings,Dictionary<string,GameInstall> games,CancellationToken ct,Action<string> log){
  var script=Path.Combine(settings.ProjectRoot,"tools/prepare-portable-p2-peers.py");
  if(!games.ContainsKey("portal2")||!File.Exists(script))return;
  settings.LocalAssetCaches.TryGetValue("tf2",out var tf);
  settings.LocalAssetCaches.TryGetValue("shared-source1",out var shared);
  shared??=Path.Combine(Settings.Home,"assets","empty");Directory.CreateDirectory(shared);tf??=shared;
  var compilerRoot=games.TryGetValue("hl2",out var hl2)?hl2.Root:SdkConfiguration.FindRuntime(settings);
  var compiler=Path.Combine(compilerRoot,"bin/studiomdl.exe");
  if(!File.Exists(compiler)){settings.LocalAssetCaches.Remove("portal-peers");log("Portal 2 will use cyan placeholder peers. Install the free Source SDK Base 2013 Multiplayer tools for full model conversion.");return;}
  var only=new List<string>{"chell"};
  if(games.ContainsKey("tf2")){only.Add("heavy");only.Add("soldier");}
  if(File.Exists(Path.Combine(shared,"models/player/kleiner.mdl")))only.Add("gmod");
  var extractor=Path.Combine(settings.ProjectRoot,"tools/prepare-portable-chell-input.cjs");
  var portal=games["portal2"];
  var fingerprint=CrossplaySetup.Hash(script)+CrossplaySetup.Hash(extractor)+CrossplaySetup.Hash(Path.Combine(portal.Root,"portal2/pak01_dir.vpk"))+string.Join("|",only)+string.Join("",new[]{tf,shared}.Select(p=>Path.Combine(p,"local-assets.json")).Where(File.Exists).Select(CrossplaySetup.Hash));
  if(settings.LocalAssetCaches.TryGetValue("portal-peers",out var prior)&&SharedAssetPreparation.Valid(prior,fingerprint))return;
  string blender=await SetupDownloads.Blender(ct,log);
  var sourceio=await SetupDownloads.SourceIo(ct,log);
  var stage=Path.Combine(Settings.Home,"assets","portal-peers-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  var request=Path.Combine(stage,"request.json");
  var chell=Path.Combine(stage,"inputs");var extraction=Path.Combine(stage,"extraction.json");
  File.WriteAllText(extraction,JsonSerializer.Serialize(new{portalRoot=portal.Root,cacheRoot=chell}));
  await SharedAssetPreparation.Run(settings.NodePath,new[]{extractor,extraction},Path.Combine(stage,"extraction.log"),ct);
  File.WriteAllText(request,JsonSerializer.Serialize(new{only,sourceioRoot=sourceio,tfCache=tf,sharedCache=shared,chellCache=chell,outputRoot=Path.Combine(stage,"sources")}));
  log("Converting animated Portal 2 player models locally. This can take several minutes…");
  await SharedAssetPreparation.Run(blender,new[]{"-b","--factory-startup","--python-exit-code","1","--python",script,"--",request},Path.Combine(stage,"conversion.log"),ct);
  var content=Path.Combine(compilerRoot,"hl2").Replace('\\','/');if(content.Contains('"')||content.Any(char.IsControl))throw new InvalidDataException("Invalid game content path.");
  File.WriteAllText(Path.Combine(stage,"gameinfo.txt"),"\"GameInfo\" { game \"SourceBox local conversion\" FileSystem { SteamAppId 220 SearchPaths { Game \""+content+"\" } } }");
  foreach(var name in only){
   await SharedAssetPreparation.Run(compiler,new[]{"-game",stage,Path.Combine(stage,"sources",name,"model.qc")},Path.Combine(stage,name+"-compile.log"),ct);
   foreach(var suffix in new[]{".mdl",".vvd",".dx90.vtx"})if(!File.Exists(Path.Combine(stage,name=="chell"?"models/sourcebox/portal2":"models/sourcebox/portal2/peers",name+suffix)))throw new IOException("Character compiler did not produce "+name+suffix);
  }
  SharedAssetPreparation.Receipt(stage,fingerprint);settings.LocalAssetCaches["portal-peers"]=stage;settings.Save();log("Portal 2 animated peer models prepared.");
 }
}
