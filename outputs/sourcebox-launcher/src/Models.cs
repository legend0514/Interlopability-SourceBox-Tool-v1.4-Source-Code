using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace SourceBox;
public record GameDef(string Id,string Title,string Engine,int App,string Exe,string Content,int Port);
public record GameInstall(GameDef Def,string Root,string Exe,bool Installed,bool Ready,string Detail);
public sealed class Settings {
 public string ProjectRoot {get;set;}="";
 public string SdkRoot {get;set;}="";
 public string SteamRoot {get;set;}="";
 public string NodePath {get;set;}="";
 public string WeaponArtFolder {get;set;}="";
 public string Language {get;set;}="auto";
 public string PreferredGame {get;set;}="hl2";
 public int FpsLimit {get;set;}=60;
 public bool ScanOnOpen {get;set;}=true;
 public bool VerboseLogging {get;set;}=false;
 public bool ReducedMotion {get;set;}=false;
 public Dictionary<string,string> Executables {get;set;}=new();
 public Dictionary<string,string> DependencyRoots {get;set;}=new();
 public Dictionary<string,string> LocalAssetCaches {get;set;}=new();
 public string[] SelectedGames {get;set;}=new[]{"hl2"};
 public static readonly string Home=ResolveHome();
 static string ResolveHome(){var configured=Environment.GetEnvironmentVariable("SOURCEBOX_HOME");if(string.IsNullOrWhiteSpace(configured))return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SourceBox");if(!Path.IsPathFullyQualified(configured)||configured.Any(char.IsControl))throw new InvalidDataException("SOURCEBOX_HOME must be an absolute directory.");return Path.GetFullPath(configured);}
 public static string FilePath=>Path.Combine(Home,"settings.json");
 public static Settings Defaults(){
  var s=new Settings();
  for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent){
   var root=Path.Combine(d.FullName,"sourcebox");
   if(File.Exists(Path.Combine(root,"core.mjs"))){s.ProjectRoot=root;s.SdkRoot=Path.GetFullPath(Path.Combine(root,"../../work/sourcebox-sdk/game"));s.WeaponArtFolder=Path.GetFullPath(Path.Combine(root,"../../work/launcher-art/renders"));break;}
  }
  s.SteamRoot=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath","")?.ToString()??"";
  foreach(var p in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))try{var exe=Path.Combine(p,"node.exe");if(File.Exists(exe)){s.NodePath=exe;break;}}catch{}
  return s;
 }
 public static Settings Load(){Settings settings;if(!File.Exists(FilePath))settings=Defaults();else try{settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))??Defaults();}catch(System.Text.Json.JsonException){File.Copy(FilePath,FilePath+".invalid-"+DateTime.UtcNow.Ticks);settings=Defaults();}AutoDiscovery.Resolve(settings);return settings;}
 public void Save(){Validate();Directory.CreateDirectory(Home);var tmp=FilePath+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(tmp,FilePath,true);}
 public void Validate(){if(FpsLimit<30||FpsLimit>240)throw new Exception("FPS limit must be between 30 and 240.");if(!Discovery.Defs.Any(g=>g.Id==PreferredGame))throw new Exception("Choose a game from the list.");foreach(var p in new[]{ProjectRoot,SdkRoot,SteamRoot,NodePath,WeaponArtFolder}.Concat(Executables.Values).Concat(LocalAssetCaches.Values).Concat(DependencyRoots.Values)){if(p.Length>0&&(!Path.IsPathFullyQualified(p)||p.Any(char.IsControl)))throw new Exception("Paths must be absolute and contain no control characters.");}}
}
public static class Discovery {
 public static readonly GameDef[] Defs={
  new("hl2","Half-Life 2","Source 1",220,"mod_hl2_win64.exe","mod_hl2",27016),
  new("tf2","Team Fortress 2","Source 1",440,"mod_tf_win64.exe","mod_tf",27015),
  new("hl1mp","Half-Life Deathmatch: Source","Source 1",360,"hl1mp_win64.exe","hl1mp",27018),
  new("gmod","Garry's Mod","Source 1",4000,"gmod.exe","garrysmod",27017),
  new("portal2","Portal 2","Source 1",620,"portal2.exe","portal2",27020),
  new("cs2","Counter-Strike 2","Source 2",730,"game/bin/win64/cs2.exe","game/csgo",27019)};
 public static IEnumerable<string> Libraries(string steam){
  var dirs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);if(Directory.Exists(steam))dirs.Add(Path.GetFullPath(steam));
  var file=Path.Combine(steam,"steamapps/libraryfolders.vdf");
  if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s*\"([^\"]+)\"")){var p=m.Groups[1].Value.Replace("\\\\","\\");if(Directory.Exists(p))dirs.Add(Path.GetFullPath(p));}
  return dirs;
 }
 internal static bool NativeRoot(string id,string root)=>id switch {
  "hl2"=>File.Exists(Path.Combine(root,"hl2/gameinfo.txt"))&&File.Exists(Path.Combine(root,"hl2/hl2_misc_dir.vpk")),
  "tf2"=>File.Exists(Path.Combine(root,"tf/gameinfo.txt"))&&File.Exists(Path.Combine(root,"tf/tf2_misc_dir.vpk")),_=>false};
 internal static string? FindNativeRoot(string id,string exe){
  var name=Path.GetFileName(exe).ToLowerInvariant();
  if(id is not ("hl2" or "tf2")||name is not ("hl2.exe" or "hl2_win64.exe" or "tf_win64.exe"))return null;
  var dir=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(exe))!);
  for(int i=0;i<4&&dir!=null;i++,dir=dir.Parent)if(NativeRoot(id,dir.FullName))return dir.FullName;
  return null;
 }
 public static List<GameInstall> Scan(Settings s){
  var libs=Libraries(s.SteamRoot).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var result=new List<GameInstall>();
  foreach(var g in Defs){
   string root="",exe="";
   foreach(var lib in libs){var f=Path.Combine(lib,$"steamapps/appmanifest_{g.App}.acf");if(!File.Exists(f))continue;var m=Regex.Match(File.ReadAllText(f),"\"installdir\"\\s*\"([^\"]+)\"");if(!m.Success)continue;var common=Path.GetFullPath(Path.Combine(lib,"steamapps/common"));var candidate=Path.GetFullPath(Path.Combine(common,m.Groups[1].Value));if(candidate.StartsWith(common+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(candidate)){root=candidate;break;}}
   if(!Directory.Exists(root)){
    var folder=g.Id switch {"tf2"=>"Team Fortress 2","hl2"=>"Half-Life 2",_=>""};
    if(folder.Length>0)root=libs.Select(l=>Path.Combine(l,"steamapps/common",folder)).FirstOrDefault(p=>NativeRoot(g.Id,p))??"";
   }
   bool owned=Directory.Exists(root);
   if(g.Id is "hl2" or "tf2"){exe=Path.Combine(s.SdkRoot,g.Exe);}
   else if(owned)exe=Path.Combine(root,g.Exe);
   if(s.Executables.TryGetValue(g.Id,out var manual)&&File.Exists(manual)){
    var native=FindNativeRoot(g.Id,manual);
    if(g.Id is "hl2" or "tf2" && native!=null){root=native;owned=true;}
    else{
     if(!Path.GetFileName(manual).Equals(Path.GetFileName(g.Exe),StringComparison.OrdinalIgnoreCase)){result.Add(new(g,root,manual,false,false,"The selected executable does not match this game or its installed content."));continue;}
     exe=Path.GetFullPath(manual);if(g.Id is not ("hl2" or "tf2")){root=g.Id=="cs2"?Path.GetFullPath(Path.Combine(Path.GetDirectoryName(exe)!,"../../..")):g.Id=="gmod"&&Path.GetDirectoryName(exe)!.EndsWith(Path.Combine("bin","win64"),StringComparison.OrdinalIgnoreCase)?Path.GetFullPath(Path.Combine(Path.GetDirectoryName(exe)!,"../..")):Path.GetDirectoryName(exe)!;owned=true;}
    }
   }
   bool installed=owned&&(g.Id is "hl2" or "tf2"||File.Exists(exe));
   var baseDir=g.Id is "hl2" or "tf2"?Path.GetDirectoryName(exe)??"":root;
   var content=Path.Combine(baseDir,g.Content);
   var checks=new List<string>();
   checks.Add(Path.Combine(content,"maps/sourcebox_room_001."+(g.Id=="cs2"?"vpk":"bsp")));
   if(g.Id=="gmod"){
    checks.Add(Path.Combine(content,"addons/sourcebox_local/lua/autorun/server/sourcebox.lua"));
    checks.Add(Path.Combine(content,"addons/sourcebox_portal_dependency/lua/entities/prop_portal/init.lua"));
    checks.Add(Path.Combine(content,"data/sourcebox/assets.json"));
   }else if(g.Id=="cs2"){
    checks.Add(Path.Combine(content,"addons/counterstrikesharp/plugins/SourceboxCS2/SourceboxCS2.dll"));
    checks.Add(Path.Combine(content,"addons/metamod.vdf"));checks.Add(Path.Combine(content,"gameinfo.gi"));
   }else checks.Add(Path.Combine(content,$"addons/sourcebox_{g.Id}.dll"));
   if(g.Id is "hl2" or "tf2"){checks.Add(Path.Combine(content,"gameinfo.txt"));foreach(var name in new[]{"client.dll","server.dll"})checks.Add(Path.Combine(content,"bin/x64",name));}
   var missing=checks.Where(p=>!File.Exists(p)).Select(p=>Path.GetRelativePath(content,p)).Concat(ReleaseRequirements.Missing(s,g.Id,content)).ToArray();
   bool ready=installed&&File.Exists(exe)&&missing.Length==0;
   result.Add(new(g,root,exe,installed,ready,!installed?"Game not found. Choose its executable or install it through Steam.":!File.Exists(exe)?"Game installed. Choose its SourceBox runtime in Settings.":missing.Length>0?"Setup required: "+string.Join(", ",missing):"Ready to launch."));
  }return result;
 }
}
public record LiveGame(string State,string World,DateTime Seen,string Weapon,DateTime? PlayerSeen=null);
public static class Telemetry {
 public static Dictionary<string,LiveGame> Read(string file,DateTime now){
  var result=new Dictionary<string,LiveGame>();if(!File.Exists(file))return result;
  try{using var fs=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);bool partial=fs.Length>2*1024*1024;fs.Seek(Math.Max(0,fs.Length-2*1024*1024),SeekOrigin.Begin);using var r=new StreamReader(fs);if(partial)r.ReadLine();string? line;
   while((line=r.ReadLine())!=null)try{using var d=JsonDocument.Parse(line);var e=d.RootElement;if(!e.TryGetProperty("game",out var id)||!e.TryGetProperty("time",out var t)||!DateTime.TryParse(t.GetString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal,out var at))continue;var g=id.GetString()??"";if(!Discovery.Defs.Any(x=>x.Id==g))continue;var type=e.GetProperty("type").GetString();if(type=="disconnected")result[g]=new("Disconnected","",at,"");else if(type=="state")result[g]=new("Connected",e.TryGetProperty("world",out var w)?w.GetString()??"":"",at,e.TryGetProperty("weapon",out var we)?we.GetString()??"":"",at);
    else if(type=="heartbeat"){
     var prior=result.GetValueOrDefault(g);
     result[g]=new("Connected / awaiting player",prior?.World??"",at,prior?.Weapon??"",prior?.PlayerSeen);
    }}catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){}
  }catch(IOException){}
  foreach(var g in result.Keys.ToArray()){
   var live=result[g];
   if(now-live.Seen>TimeSpan.FromSeconds(5)||live.Seen>now.AddSeconds(3))result[g]=live with {State="Stale / disconnected"};
   else if(live.State!="Disconnected")result[g]=live with {State=live.PlayerSeen is DateTime player&&now-player<=TimeSpan.FromSeconds(5)&&player<=now.AddSeconds(3)?"Connected":"Connected / awaiting player"};
  }
  return result;
 }
 public static string Redact(string text,Settings settings){
  foreach(var p in new[]{settings.ProjectRoot,settings.SdkRoot,settings.SteamRoot,settings.NodePath,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Settings.Home}.Concat(settings.Executables.Values).Where(p=>p.Length>0).OrderByDescending(p=>p.Length))text=text.Replace(p,"[local path]",StringComparison.OrdinalIgnoreCase);
  text=Regex.Replace(text,@"(?i)[a-z]:[\\/](?:[^\r\n\""<>|]*?)[\\/]Users[\\/][^\\/\s]+","[user]");
  text=Regex.Replace(text,@"\b765611\d{11}\b","[steam-id]");
  return Regex.Replace(text,@"(?i)(password|token|secret|authorization|rcon_password)\s*[:=]\s*[^\s,;]+","$1=[redacted]");
 }
}




