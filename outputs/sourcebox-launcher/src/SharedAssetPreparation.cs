using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;

public static class SharedAssetPreparation {
 internal static async Task Run(string exe,IEnumerable<string> args,string logFile,CancellationToken ct,Action<string>? progress=null,int timeoutMinutes=10){
  var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
  Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
  using var writer=new StreamWriter(logFile,false){AutoFlush=true};var sync=new object();
  using var process=Process.Start(info)??throw new IOException("Could not start content preparation.");
  try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
  async Task Drain(StreamReader reader){string? line;while((line=await reader.ReadLineAsync())!=null){lock(sync)writer.WriteLine(line);if(line.StartsWith("[SourceBox] "))progress?.Invoke(line[12..]);}}
  var output=Drain(process.StandardOutput);var error=Drain(process.StandardError);
  try{await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(timeoutMinutes),ct);}
  catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();await Task.WhenAll(output,error);throw;}
  await Task.WhenAll(output,error);writer.Flush();
  if(process.ExitCode!=0)throw new IOException("Content preparation failed (exit "+process.ExitCode+").\n"+FailureDetail(logFile)+"\nDetails: "+logFile);
 }
 internal static string FailureDetail(string file){
  try{using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);stream.Seek(Math.Max(0,stream.Length-8192),SeekOrigin.Begin);using var reader=new StreamReader(stream);var lines=reader.ReadToEnd().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);var useful=lines.Where(l=>l.Contains("error",StringComparison.OrdinalIgnoreCase)||l.Contains("failed",StringComparison.OrdinalIgnoreCase)||l.StartsWith("Error:"));return string.Join("\n",(useful.Any()?useful:lines.TakeLast(3)).TakeLast(4).Select(l=>l.Length>240?l[..240]:l));}catch(IOException){return "Open the preparation log for the compiler's error.";}
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
 internal static bool PortalAddonReady(string root)=>File.Exists(Path.Combine(root,"lua/entities/prop_portal/init.lua"))&&File.Exists(Path.Combine(root,"models/weapons/c_portalgun.mdl"));
 internal static string[] PortalArchives(string gmodRoot,IEnumerable<string> libraries){
  var files=libraries.Select(l=>Path.Combine(l,"steamapps/workshop/content/4000/1800764828")).Where(Directory.Exists)
   .SelectMany(d=>Directory.GetFiles(d,"*",SearchOption.AllDirectories)).ToList();
  var addons=Path.Combine(gmodRoot,"garrysmod/addons");
  if(Directory.Exists(addons))files.AddRange(Directory.GetFiles(addons,"*1800764828*",SearchOption.TopDirectoryOnly));
  return files.Where(file=>{
   try{using var stream=File.OpenRead(file);Span<byte> magic=stackalloc byte[4];return stream.Read(magic)==4&&magic.SequenceEqual("GMAD"u8);}
   catch(IOException){return false;}
  }).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
 }
 public static bool NeedsPortalPeers(IEnumerable<string> selected)=>selected.Any(id=>id is "portal2" or "cs2");
 public static bool NeedsCs2(IEnumerable<string> selected)=>selected.Contains("cs2");
 public static async Task Prepare(Settings settings,IEnumerable<GameInstall> discovered,CancellationToken ct,Action<string> log,IEnumerable<string>? selected=null){
  var games=discovered.Where(g=>g.Installed).ToDictionary(g=>g.Def.Id);
  var targets=(selected??settings.SelectedGames).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var availableProviders=games.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
  try{await LocalAssetPreparation.PrepareTf2(settings,games.Values,ct,log);}
  catch(Exception e)when(!targets.Contains("tf2")&&(e is IOException or TimeoutException)){
   ct.ThrowIfCancellationRequested();settings.LocalAssetCaches.Remove("tf2");availableProviders.Remove("tf2");
   log("Optional TF2 shared assets could not be prepared: "+e.Message+" Selected games will use placeholders for unavailable TF2 models.");
  }
  string Tool(string name)=>Path.Combine(settings.ProjectRoot,"tools",name);
  if(File.Exists(Tool("prepare-portable-hl1-assets.cjs"))){
   var names=new Dictionary<string,string>{{"hl1mp","Half-Life 1 Source Deathmatch"},{"tf2","Team Fortress 2"},{"gmod","GarrysMod"},{"hl2","Half-Life 2"}};
   var roots=games.Where(p=>names.ContainsKey(p.Key)).ToDictionary(p=>names[p.Key],p=>p.Value.Root);
   var sdk=SdkConfiguration.FindRuntime(settings);if(sdk.Length>0)roots["SDK Base"]=sdk;
   var sources=roots.Values.SelectMany(root=>new[]{"hl1/hl1_pak_dir.vpk","hl1mp/hl1mp_pak_dir.vpk","hl2/hl2_misc_dir.vpk","hl2/hl2_textures_dir.vpk","garrysmod/garrysmod_dir.vpk"}.Select(p=>Path.Combine(root,p))).Where(File.Exists).Concat(new[]{"hl1mp.json","gmod-scene.json","cs2.json"}.Select(p=>Path.Combine(settings.ProjectRoot,"sheets",p)));
   await Recipe(settings,"shared-source1",Tool("prepare-portable-hl1-assets.cjs"),new(){{"games",roots}},sources,ct,log);
  }
  if(games.TryGetValue("gmod",out var gmod)&&File.Exists(Tool("prepare-portable-portal-assets.mjs"))&&
    (targets.Contains("gmod")||PortalAddonReady(Path.Combine(gmod.Root,"garrysmod/addons/sourcebox_portal_dependency"))||
     (settings.DependencyRoots.TryGetValue("portal-addon",out var reusable)&&PortalAddonReady(reusable))||
     PortalArchives(gmod.Root,Discovery.Libraries(settings.SteamRoot)).Length>0)){
   string addon=Path.Combine(gmod.Root,"garrysmod/addons/sourcebox_portal_dependency");
   if(!PortalAddonReady(addon)&&settings.DependencyRoots.TryGetValue("portal-addon",out var configured)&&PortalAddonReady(configured))addon=configured;
   if(!PortalAddonReady(addon)){
    var archives=PortalArchives(gmod.Root,Discovery.Libraries(settings.SteamRoot));
    if(archives.Length==0)throw new IOException("Install the Portal Gun successor addon in Garry's Mod (Workshop 1800764828), let Steam finish downloading it, then retry setup.");
    var archive=archives.OrderByDescending(File.GetLastWriteTimeUtc).First();var fingerprint=CrossplaySetup.Hash(archive);
    if(settings.DependencyRoots.TryGetValue("portal-addon",out var prior)&&Valid(prior,fingerprint))addon=prior;
    else{
     addon=Stage("portal-addon");var gmad=Path.Combine(gmod.Root,"bin/gmad.exe");if(!File.Exists(gmad))throw new IOException("Garry's Mod archive extractor is missing; verify the game in Steam.");
     log("Extracting the installed Portal Gun addon locally…");await Run(gmad,new[]{"extract","-file",archive,"-out",addon},Path.Combine(addon,"extraction.log"),ct);
     if(!PortalAddonReady(addon))throw new IOException("Portal Gun extraction completed, but lua/entities/prop_portal/init.lua or models/weapons/c_portalgun.mdl is missing. The addon is incomplete or incompatible.");
     Receipt(addon,fingerprint);
    }
   }
   settings.DependencyRoots["portal-addon"]=addon;settings.Save();
   var inputs=Directory.GetFiles(Path.Combine(addon,"models/weapons"),"*portalgun*",SearchOption.TopDirectoryOnly).Concat(Directory.GetFiles(Path.Combine(addon,"materials/models/weapons/v_models/v_portalgun/p1"))).Concat(Directory.GetFiles(Path.Combine(addon,"materials/models/weapons/w_models/portalgun/p1")));
   await Recipe(settings,"portal-gun",Tool("prepare-portable-portal-assets.mjs"),new(){{"addonRoot",addon}},inputs.OrderBy(f=>f,StringComparer.OrdinalIgnoreCase),ct,log);
  }
  try{if(NeedsPortalPeers(targets))await PortalPeerPreparation.Prepare(settings,games,ct,log);}
  catch(Exception e)when(e is IOException or System.Net.Http.HttpRequestException or TimeoutException){
   ct.ThrowIfCancellationRequested();
   settings.LocalAssetCaches.Remove("portal-peers");
   log("Portal 2 full character conversion is unavailable: "+e.Message+" Using original cyan placeholder peers instead.");
  }
  FallbackAssets.Prepare(settings,availableProviders,log);
  if(NeedsCs2(targets))await CsLoaderPreparation.Prepare(settings,games.Values,ct,log);
  var own=Path.Combine(settings.ProjectRoot,"shared");if(Directory.Exists(own)){settings.LocalAssetCaches["sourcebox"]=own;settings.Save();}
  if(NeedsCs2(targets))await CsContentPreparation.Prepare(settings,games,ct,log);
 }
}
