using System.Text;
using System.Text.RegularExpressions;
namespace SourceBox;

public static class SdkConfiguration {
 public static string FindRuntime(Settings settings){
  foreach(var library in Discovery.Libraries(settings.SteamRoot)){
   var manifest=Path.Combine(library,"steamapps/appmanifest_243750.acf");if(!File.Exists(manifest))continue;
   var match=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s*\"([^\"]+)\"");if(!match.Success)continue;
   try{var root=DesktopInstall.Resolve(Path.Combine(library,"steamapps/common"),match.Groups[1].Value);if(File.Exists(Path.Combine(root,"bin/x64/steam_api64.dll")))return root;}catch(InvalidDataException){}
  }
  return "";
 }
 static string Q(string value){if(value.Contains('"')||value.Any(char.IsControl))throw new InvalidDataException("Invalid content mount path.");return "\""+value.Replace('\\','/')+"\"";}
 public static string GameInfo(string id,string installedRoot,string sdkRuntime,IEnumerable<string> caches){
  if(id is not ("hl2" or "tf2"))throw new ArgumentException("Not an SDK game.");
  var mounts=new List<(string Key,string Value)>{("mod+mod_write","|gameinfo_path|."),("game+game_write","|gameinfo_path|."),("default_write_path","|gameinfo_path|."),("gamebin","|gameinfo_path|bin")};
  foreach(var cache in caches)mounts.Add(("game",cache));
  if(id=="tf2"){
   foreach(var name in new[]{"tf2_textures","tf2_sound_vo_english","tf2_sound_misc","tf2_misc"})mounts.Add(("game+mod",Path.Combine(installedRoot,"tf",name+".vpk")));
   mounts.Add(("game",Path.Combine(installedRoot,"tf")));
  }
  foreach(var name in new[]{"hl2_textures","hl2_sound_vo_english","hl2_sound_misc","hl2_misc"})mounts.Add(("game",Path.Combine(sdkRuntime,"hl2",name+".vpk")));
  mounts.Add(("game",Path.Combine(sdkRuntime,"hl2")));
  if(id=="hl2")mounts.Add(("game",Path.Combine(installedRoot,"hl2")));
  mounts.Add(("platform",Path.Combine(sdkRuntime,"platform/platform_misc.vpk")));mounts.Add(("platform",Path.Combine(sdkRuntime,"platform")));
  return "\"GameInfo\"\n{\n game \"SourceBox\"\n type "+(id=="hl2"?"singleplayer_only":"multiplayer_only")+"\n DependsOnAppID "+(id=="hl2"?220:440)+"\n"+(id=="tf2"?" AdditionalLocalization tf\n ReplayRequired 1\n":"")+" FileSystem\n {\n  SteamAppId 243750\n  SearchPaths\n  {\n"+string.Join("\n",mounts.Select(m=>"   "+m.Key+" "+Q(m.Value)))+"\n  }\n }\n}\n";
 }
 public static string WithAssetMounts(string text,IEnumerable<string> caches){
  text=Regex.Replace(text,@"\s*// sourcebox-managed-assets-begin[\s\S]*?// sourcebox-managed-assets-end","");
  var mounts=string.Join("\n",caches.Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>"   Game "+Q(p)));
  if(mounts.Length==0)return text;
  // Parse KeyValues tokens so quoted keys and comments cannot hide an existing
  // custom write path. Only SearchPaths keys control this decision.
  var tokens=Regex.Matches(text,@"//[^\r\n]*|""(?:\\.|[^""\\])*""|[{}]|[^\s{}""]+")
   .Cast<Match>().Where(m=>!m.Value.StartsWith("//")).ToArray();
  var start=Array.FindIndex(tokens,m=>m.Value.Trim('"').Equals("SearchPaths",StringComparison.OrdinalIgnoreCase));
  if(start<0||start+1>=tokens.Length||tokens[start+1].Value!="{")throw new InvalidDataException("Native content mount format changed; gameinfo was not edited.");
  var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int end=start+2;
  while(end<tokens.Length&&tokens[end].Value!="}"){
   if(end+1>=tokens.Length||tokens[end].Value=="{"||tokens[end+1].Value is "{" or "}")throw new InvalidDataException("Malformed native search paths; gameinfo was not edited.");
   keys.Add(tokens[end].Value.Trim('"'));end+=2;
   // Valve may append a platform condition to a search-path pair. Preserve it
   // verbatim rather than interpreting it as the next path key.
   if(end<tokens.Length&&Regex.IsMatch(tokens[end].Value,@"^\[!?\$[A-Za-z0-9_]+\]$"))end++;
  }
  if(end>=tokens.Length)throw new InvalidDataException("Unclosed native search paths; gameinfo was not edited.");
  var writes="";
  if(!keys.Any(k=>k.Split('+').Contains("game_write",StringComparer.OrdinalIgnoreCase)))writes+="   Game_Write \"|gameinfo_path|.\"\n";
  if(!keys.Any(k=>k.Split('+').Contains("default_write_path",StringComparer.OrdinalIgnoreCase)))writes+="   Default_Write_Path \"|gameinfo_path|.\"\n";
  var anchor=tokens[start+1];
  return text.Insert(anchor.Index+anchor.Length,"\n// sourcebox-managed-assets-begin\n"+writes+mounts+"\n// sourcebox-managed-assets-end");
 }
 public static string Generated(string content){
  var bytes=Encoding.UTF8.GetBytes(content);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
  var directory=Path.Combine(Settings.Home,"setup","generated");Directory.CreateDirectory(directory);var file=Path.Combine(directory,hash+".txt");
  if(!File.Exists(file)||CrossplaySetup.Hash(file)!=hash)File.WriteAllBytes(file,bytes);return file;
 }
}
