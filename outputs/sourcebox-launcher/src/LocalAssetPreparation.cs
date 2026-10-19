using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;

public static class LocalAssetPreparation {
 public static async Task PrepareTf2(Settings settings,IEnumerable<GameInstall> games,CancellationToken ct,Action<string> log){
  var game=games.FirstOrDefault(g=>g.Def.Id=="tf2"&&g.Installed);if(game==null)return;
  var script=Path.Combine(settings.ProjectRoot,"tools/prepare-portable-tf-assets.cjs");
  if(!File.Exists(script))throw new IOException("The TF2 asset preparation component is missing from this runtime.");
  var archives=new[]{"tf2_misc_dir.vpk","tf2_textures_dir.vpk"}.ToDictionary(n=>n,n=>CrossplaySetup.Hash(Path.Combine(game.Root,"tf",n)));
  string recipe=CrossplaySetup.Hash(script);
  if(settings.LocalAssetCaches.TryGetValue("tf2",out var existing)){
   var receipt=Path.Combine(existing,"local-assets.json");
   if(File.Exists(receipt))try{
    using var doc=JsonDocument.Parse(File.ReadAllText(receipt));var root=doc.RootElement;
    if(root.GetProperty("recipe").GetString()==recipe&&root.GetProperty("archives").GetArrayLength()==archives.Count&&root.GetProperty("archives").EnumerateArray().All(a=>archives.TryGetValue(a.GetProperty("name").GetString()!,out var hash)&&hash.Equals(a.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))&&root.GetProperty("files").EnumerateArray().All(f=>File.Exists(DesktopInstall.Resolve(existing,f.GetProperty("name").GetString()!))&&CrossplaySetup.Hash(DesktopInstall.Resolve(existing,f.GetProperty("name").GetString()!)).Equals(f.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)))return;
   }catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidDataException){log("Rebuilding the local TF2 asset cache.");}
  }
  var stage=Path.Combine(Settings.Home,"assets","tf2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  var request=Path.Combine(stage,"request.json");
  File.WriteAllText(request,JsonSerializer.Serialize(new{tf2Root=game.Root,cacheRoot=stage,sheetPath=Path.Combine(settings.ProjectRoot,"sheets/asset-references.json")}));
  var info=new ProcessStartInfo(settings.NodePath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add(script);info.ArgumentList.Add(request);
  log("Preparing TF2 player and weapon assets from your installed game…");
  using var process=Process.Start(info)??throw new IOException("Could not start local asset preparation.");
  var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
  try{await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(5),ct);}catch{if(!process.HasExited)process.Kill(true);throw;}
  File.WriteAllText(Path.Combine(stage,"preparation.log"),await output+await errors);
  if(process.ExitCode!=0)throw new IOException("TF2 asset preparation failed. Details: "+Path.Combine(stage,"preparation.log"));
  var manifest=Path.Combine(stage,"local-assets.json");
  var data=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!;data["recipe"]=recipe;File.WriteAllText(manifest,data.ToJsonString());
  settings.LocalAssetCaches["tf2"]=stage;settings.Save();log("TF2 assets prepared locally; no game content is included in the download.");
 }
}
