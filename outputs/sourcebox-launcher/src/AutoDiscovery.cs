using Microsoft.Win32;
namespace SourceBox;

// Only discover known install locations. Never execute a discovered setup script.
public static class AutoDiscovery {
 public static string[] SteamRoots(string configured){
  var candidates=new List<string>{configured};
  foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"})
   foreach(var value in new[]{"SteamPath","InstallPath"})try{if(Registry.GetValue(key,value,null) is string path)candidates.Add(path);}catch(System.Security.SecurityException){}
  candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
  candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Steam"));
  return candidates.Where(p=>!string.IsNullOrWhiteSpace(p)&&Directory.Exists(p)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 public static bool Resolve(Settings settings,string appDirectory,IEnumerable<string> steamRoots,IEnumerable<string> executablePaths){
  var before=System.Text.Json.JsonSerializer.Serialize(settings);
  if(!Directory.Exists(settings.SteamRoot))settings.SteamRoot=steamRoots.FirstOrDefault(Directory.Exists)??settings.SteamRoot;
  var roots=new List<string>{Path.Combine(appDirectory,"runtime","sourcebox"),Path.Combine(Settings.Home,"runtime","sourcebox")};
  for(var dir=new DirectoryInfo(appDirectory);dir!=null;dir=dir.Parent)roots.Add(Path.Combine(dir.FullName,"sourcebox"));
  if(!File.Exists(Path.Combine(settings.ProjectRoot,"core.mjs")))settings.ProjectRoot=roots.FirstOrDefault(p=>File.Exists(Path.Combine(p,"core.mjs")))??settings.ProjectRoot;
  var sdk=new List<string>{Path.Combine(appDirectory,"runtime","sdk"),Path.Combine(Settings.Home,"runtime","sdk")};
  if(Directory.Exists(settings.ProjectRoot))sdk.Add(Path.GetFullPath(Path.Combine(settings.ProjectRoot,"../../work/sourcebox-sdk/game")));
  bool SdkExists(string p)=>File.Exists(Path.Combine(p,"mod_hl2_win64.exe"))||File.Exists(Path.Combine(p,"mod_tf_win64.exe"));
  if(!SdkExists(settings.SdkRoot))settings.SdkRoot=sdk.FirstOrDefault(SdkExists)??settings.SdkRoot;
  if(!File.Exists(settings.NodePath))settings.NodePath=new[]{Path.Combine(appDirectory,"runtime","node","node.exe"),Path.Combine(Settings.Home,"runtime","node","node.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"nodejs","node.exe"),Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432")??"","nodejs","node.exe"),Path.Combine(Environment.GetEnvironmentVariable("NVM_SYMLINK")??"","node.exe")}.Concat(executablePaths.Select(p=>Path.Combine(p,"node.exe"))).Where(Path.IsPathFullyQualified).FirstOrDefault(File.Exists)??settings.NodePath;
  if(!Directory.Exists(settings.WeaponArtFolder)&&Directory.Exists(settings.ProjectRoot)){
   var art=Path.GetFullPath(Path.Combine(settings.ProjectRoot,"../../work/launcher-art/renders"));if(Directory.Exists(art))settings.WeaponArtFolder=art;
  }
  return before!=System.Text.Json.JsonSerializer.Serialize(settings);
 }
 public static bool Resolve(Settings settings)=>Resolve(settings,AppContext.BaseDirectory,SteamRoots(settings.SteamRoot),string.Join(Path.PathSeparator,new[]{Environment.GetEnvironmentVariable("PATH"),Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.User),Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.Machine)}).Split(Path.PathSeparator).Select(p=>p.Trim().Trim('"')).Where(p=>Path.IsPathFullyQualified(p)));
}
