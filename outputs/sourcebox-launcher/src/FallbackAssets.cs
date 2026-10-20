using System.Text.Json;
namespace SourceBox;
public static class FallbackAssets {
 internal static bool Needed(string model,string provider,ISet<string> installed,IEnumerable<string> realCaches)=>
  !realCaches.Any(root=>File.Exists(DesktopInstall.Resolve(root,model)))&&(provider=="always"||!installed.Contains(provider));
 public static void Prepare(Settings settings,IEnumerable<string> installed,Action<string> log){
  var source=Path.Combine(settings.ProjectRoot,"fallback");var manifest=Path.Combine(source,"aliases.json");
  if(!File.Exists(manifest))throw new IOException("SourceBox placeholder assets are missing. Extract the complete updated package.");
  settings.LocalAssetCaches.Remove("fallback");
  var real=settings.LocalAssetCaches.Values.ToArray();var ids=installed.ToHashSet(StringComparer.OrdinalIgnoreCase);
  var aliases=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifest))!;
  var needed=aliases.Where(p=>Needed(p.Key,p.Value,ids,real)).Select(p=>p.Key).ToArray();
  var fingerprint=CrossplaySetup.Hash(manifest)+string.Join("|",needed);
  var stage=Path.Combine(Settings.Home,"assets","fallback-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint)))[..16]);
  Directory.CreateDirectory(stage);
  void Copy(string relative){var input=DesktopInstall.Resolve(source,relative);if(!File.Exists(input))return;var output=DesktopInstall.Resolve(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(input,output,true);}
  foreach(var model in needed)foreach(var suffix in new[]{".mdl",".vvd",".dx90.vtx",".dx80.vtx",".sw.vtx"})Copy(model[..^4]+suffix);
  Copy("materials/sourcebox/fallback/fallback.vmt");
  Copy("materials/sourcebox/fallback/cyan.vtf");
  File.WriteAllText(Path.Combine(stage,"fallback-status.json"),JsonSerializer.Serialize(new{kind="Original static debug figures",models=needed}));
  settings.LocalAssetCaches["fallback"]=stage;settings.Save();
  if(needed.Length>0)log("Using "+needed.Length+" cyan placeholder models where converted game content is unavailable.");
 }
}
