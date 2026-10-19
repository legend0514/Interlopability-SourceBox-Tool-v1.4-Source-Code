using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;

public static class SharedAssetPreparation {
 internal static async Task Run(string exe,IEnumerable<string> args,string logFile,CancellationToken ct){
  var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
  using var process=Process.Start(info)??throw new IOException("Could not start content preparation.");var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
  try{await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(10),ct);}catch{if(!process.HasExited)process.Kill(true);throw;}
  File.WriteAllText(logFile,await output+await error);if(process.ExitCode!=0)throw new IOException("Content preparation failed. Details: "+logFile);
 }
 internal static bool Valid(string root,string fingerprint){
  try{using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"sourcebox-receipt.json")));
   return doc.RootElement.GetProperty("fingerprint").GetString()==fingerprint&&doc.RootElement.GetProperty("files").EnumerateArray().All(f=>CrossplaySetup.Hash(DesktopInstall.Resolve(root,f.GetProperty("name").GetString()!))==f.GetProperty("hash").GetString());
  }catch(Exception e)when(e is IOException or JsonException or KeyNotFoundException or InvalidDataException){return false;}
 }
 internal static void Receipt(string root,string fingerprint){
  var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(f=>Path.GetFileName(f)!="sourcebox-receipt.json").Select(f=>new{name=Path.GetRelativePath(root,f),hash=CrossplaySetup.Hash(f)}).ToArray();
  File.WriteAllText(Path.Combine(root,"sourcebox-receipt.json"),JsonSerializer.Serialize(new{fingerprint,localOnly=true,redistribute=false,files}));
 }
 static string Stage(string id){var root=Path.Combine(Settings.Home,"assets",id+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
 static async Task Recipe(Settings settings,string id,string script,Dictionary<string,object> request,IEnumerable<string> sources,CancellationToken ct,Action<string> log){
  var helper=Path.Combine(Path.GetDirectoryName(script)!,"installed-assets.cjs");
  string fingerprint=CrossplaySetup.Hash(script)+(File.Exists(helper)?CrossplaySetup.Hash(helper):"")+string.Join("",sources.Select(CrossplaySetup.Hash));
  if(settings.LocalAssetCaches.TryGetValue(id,out var prior)&&Valid(prior,fingerprint))return;
  var stage=Stage(id);request["cacheRoot"]=stage;var input=Path.Combine(stage,"request.json");File.WriteAllText(input,JsonSerializer.Serialize(request));
  log("Preparing "+id+" from installed content…");await Run(settings.NodePath,new[]{script,input},Path.Combine(stage,"preparation.log"),ct);Receipt(stage,fingerprint);settings.LocalAssetCaches[id]=stage;settings.Save();
 }
 public static bool NeedsCs2(IEnumerable<string> selected)=>selected.Contains("cs2");
 public static async Task Prepare(Settings settings,IEnumerable<GameInstall> discovered,CancellationToken ct,Action<string> log,IEnumerable<string>? selected=null){
  var games=discovered.Where(g=>g.Installed).ToDictionary(g=>g.Def.Id);
  var targets=(selected??settings.SelectedGames).ToHashSet(StringComparer.OrdinalIgnoreCase);
  await LocalAssetPreparation.PrepareTf2(settings,games.Values,ct,log);
  string Tool(string name)=>Path.Combine(settings.ProjectRoot,"tools",name);
  if(File.Exists(Tool("prepare-portable-hl1-assets.cjs"))){
   var names=new Dictionary<string,string>{{"hl1mp","Half-Life 1 Source Deathmatch"},{"tf2","Team Fortress 2"},{"gmod","GarrysMod"},{"hl2","Half-Life 2"}};
   var roots=games.Where(p=>names.ContainsKey(p.Key)).ToDictionary(p=>names[p.Key],p=>p.Value.Root);
   var sdk=SdkConfiguration.FindRuntime(settings);if(sdk.Length>0)roots["SDK Base"]=sdk;
   var sources=roots.Values.SelectMany(root=>new[]{"hl1/hl1_pak_dir.vpk","hl1mp/hl1mp_pak_dir.vpk","hl2/hl2_misc_dir.vpk","hl2/hl2_textures_dir.vpk","garrysmod/garrysmod_dir.vpk"}.Select(p=>Path.Combine(root,p))).Where(File.Exists).Concat(new[]{"hl1mp.json","gmod-scene.json","cs2.json"}.Select(p=>Path.Combine(settings.ProjectRoot,"sheets",p)));
   await Recipe(settings,"shared-source1",Tool("prepare-portable-hl1-assets.cjs"),new(){{"games",roots}},sources,ct,log);
  }
  if(targets.Contains("gmod")&&games.TryGetValue("gmod",out var gmod)&&File.Exists(Tool("prepare-portable-portal-assets.mjs"))){
   string addon=Path.Combine(gmod.Root,"garrysmod/addons/sourcebox_portal_dependency");
   if(!File.Exists(Path.Combine(addon,"models/weapons/c_portalgun.mdl"))){
    var archives=Discovery.Libraries(settings.SteamRoot).Select(l=>Path.Combine(l,"steamapps/workshop/content/4000/1800764828")).Where(Directory.Exists).SelectMany(d=>Directory.GetFiles(d,"*.gma",SearchOption.AllDirectories)).ToArray();
    if(archives.Length==0)throw new IOException("Install the Portal Gun successor addon in Garry's Mod (Workshop 1800764828), let Steam finish downloading it, then retry setup.");
    var archive=archives.OrderByDescending(File.GetLastWriteTimeUtc).First();var fingerprint=CrossplaySetup.Hash(archive);
    if(settings.DependencyRoots.TryGetValue("portal-addon",out var prior)&&Valid(prior,fingerprint))addon=prior;
    else{
     addon=Stage("portal-addon");var gmad=Path.Combine(gmod.Root,"bin/gmad.exe");if(!File.Exists(gmad))throw new IOException("Garry's Mod archive extractor is missing; verify the game in Steam.");
     log("Extracting the installed Portal Gun addon locally…");await Run(gmad,new[]{"extract","-file",archive,"-out",addon},Path.Combine(addon,"extraction.log"),ct);
     if(!File.Exists(Path.Combine(addon,"lua/entities/prop_portal/init.lua")))throw new IOException("The downloaded addon is missing its portal implementation.");
     Receipt(addon,fingerprint);
    }
   }
   settings.DependencyRoots["portal-addon"]=addon;settings.Save();
   var inputs=Directory.GetFiles(Path.Combine(addon,"models/weapons"),"*portalgun*",SearchOption.TopDirectoryOnly).Concat(Directory.GetFiles(Path.Combine(addon,"materials/models/weapons/v_models/v_portalgun/p1"))).Concat(Directory.GetFiles(Path.Combine(addon,"materials/models/weapons/w_models/portalgun/p1")));
   await Recipe(settings,"portal-gun",Tool("prepare-portable-portal-assets.mjs"),new(){{"addonRoot",addon}},inputs.OrderBy(f=>f,StringComparer.OrdinalIgnoreCase),ct,log);
  }
  try{await PortalPeerPreparation.Prepare(settings,games,ct,log);}
  catch(Exception e)when(e is IOException or System.Net.Http.HttpRequestException or TimeoutException){
   ct.ThrowIfCancellationRequested();
   settings.LocalAssetCaches.Remove("portal-peers");
   log("Portal 2 full character conversion is unavailable: "+e.Message+" Using original cyan placeholder peers instead.");
  }
  FallbackAssets.Prepare(settings,games.Keys,log);
  if(NeedsCs2(targets))await CsLoaderPreparation.Prepare(settings,games.Values,ct,log);
  var own=Path.Combine(settings.ProjectRoot,"shared");if(Directory.Exists(own)){settings.LocalAssetCaches["sourcebox"]=own;settings.Save();}
  if(NeedsCs2(targets))await CsContentPreparation.Prepare(settings,games,ct,log);
 }
}
