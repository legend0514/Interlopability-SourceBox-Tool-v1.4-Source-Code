using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;
public static class ConfigProtection {
 public record Entry(string Target,string? Backup);
 public record Snapshot(string Directory,Entry[] Files,string[] Executables);
 public record Request(Snapshot Snapshot,OwnedProcess Process,bool Guard=false);
 public sealed class ReadLease:IDisposable {
  readonly List<FileStream> handles=new();
  internal ReadLease(Snapshot snapshot){
   try{foreach(var entry in snapshot.Files){
    var root=Path.GetDirectoryName(Path.GetDirectoryName(entry.Target))!;
    CrossplaySetup.Destination(root,Path.GetRelativePath(root,entry.Target));
    if(File.Exists(entry.Target))handles.Add(new FileStream(entry.Target,FileMode.Open,FileAccess.Read,FileShare.Read));
   }}catch{Dispose();throw;}
  }
  public void Dispose(){foreach(var handle in handles)handle.Dispose();handles.Clear();}
 }
 public static ReadLease Protect(Snapshot snapshot)=>new(snapshot);
 public static IEnumerable<string> CloudRoots(string steam,int app){
  using var active=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
  if(active?.GetValue("ActiveUser") is not int user||user==0)return Array.Empty<string>();
  var profile=Path.Combine(steam,"userdata",unchecked((uint)user).ToString(),app.ToString());
  return new[]{Path.Combine(profile,"local"),Path.Combine(profile,"remote")}.Where(p=>Directory.Exists(Path.Combine(p,"cfg"))).ToArray();
 }
 public static Snapshot Capture(string root,string[] executables,string session,IEnumerable<string>? additionalRoots=null){
  var dir=Path.Combine(session,"config-backup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var files=new List<Entry>();
  foreach(var folder in new[]{root}.Concat(additionalRoots??Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase)){
   var names=new[]{"config.cfg","autoexec.cfg","userconfig.cfg","video.txt","video_default.txt","videoconfig.txt","client.vdf","server.vdf","cs2_video.txt","cs2_machine_convars.vcfg"}.AsEnumerable();
   var cfg=CrossplaySetup.Destination(folder,"cfg/placeholder");cfg=Path.GetDirectoryName(cfg)!;
   if(Directory.Exists(cfg))names=names.Concat(Directory.GetFiles(cfg,"cs2_user_*.vcfg").Select(p=>Path.GetFileName(p)));
   foreach(var name in names){
   var target=CrossplaySetup.Destination(folder,"cfg/"+name);string? backup=null;
   if(File.Exists(target)){backup=Path.Combine(dir,files.Count+"-"+name);File.Copy(target,backup);}
   files.Add(new(target,backup));
   }
  }
  var result=new Snapshot(dir,files.ToArray(),executables);File.WriteAllText(Path.Combine(dir,"snapshot.json"),JsonSerializer.Serialize(result));return result;
 }
 // Legacy helper name retained for existing watch receipts. Unknown game/Cloud/user
 // writes cannot be attributed safely, so cleanup only preserves evidence.
 public static void Restore(Snapshot snapshot){
  var review=Path.Combine(snapshot.Directory,"review-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(review);
  var changes=new List<object>();
  foreach(var entry in snapshot.Files){
   var root=Path.GetDirectoryName(Path.GetDirectoryName(entry.Target))!;
   CrossplaySetup.Destination(root,Path.GetRelativePath(root,entry.Target));
   var exists=File.Exists(entry.Target);
   var original=entry.Backup!=null&&File.Exists(entry.Backup)?CrossplaySetup.Hash(entry.Backup):null;
   string? observed=null,copy=null;
   if(exists){
    copy=Path.Combine(review,Array.IndexOf(snapshot.Files,entry)+"-"+Path.GetFileName(entry.Target));
    File.Copy(entry.Target,copy,false);observed=CrossplaySetup.Hash(copy);
   }
   if(original==observed)continue;
   changes.Add(new{entry.Target,entry.Backup,originalSha256=original,observedSha256=observed,observedCopy=copy,
    action="preserved-current-file",reason="Game, user and Steam Cloud writes cannot be distinguished safely."});
  }
  File.WriteAllText(Path.Combine(review,"changes.json"),JsonSerializer.Serialize(changes,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(snapshot.Directory,"review-required.txt"),
   "No game settings or saves were restored, deleted or moved. Launch-time backups and later observed versions are preserved here. Review before manually restoring any setting. Last review: "+review);
 }
 public static async Task Watch(Snapshot snapshot,OwnedProcess process){
  var request=Path.Combine(snapshot.Directory,"watch.json");File.WriteAllText(request,JsonSerializer.Serialize(new Request(snapshot,process,true)));
  var pi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
  if(Path.GetFileNameWithoutExtension(pi.FileName).Equals("dotnet",StringComparison.OrdinalIgnoreCase))pi.ArgumentList.Add(typeof(ConfigProtection).Assembly.Location);
  pi.ArgumentList.Add("--protect-config");pi.ArgumentList.Add(request);using var helper=Process.Start(pi)??throw new IOException("Could not start settings protection.");
  for(int i=0;i<100;i++){
   if(File.Exists(Path.Combine(snapshot.Directory,"guard-ready.txt")))return;
   if(helper.HasExited)break;await Task.Delay(100).ConfigureAwait(false);
  }
  throw new IOException("Settings protection did not become ready. See the configuration backup in session diagnostics.");
 }
 public static async Task Run(string file){
  var r=JsonSerializer.Deserialize<Request>(File.ReadAllText(file))!;
  try{
   if(r.Guard){
    using(Protect(r.Snapshot)){
     File.WriteAllText(Path.Combine(r.Snapshot.Directory,"guard-ready.txt"),"Existing configuration files are readable but protected from writes for this session.");
     // Release on this exact process's exit, even if the user opens a new
     // normal Steam session with the same executable in the meantime.
     while(r.Process.Matches())await Task.Delay(500);
    }
   }
   while(r.Process.Matches())await Task.Delay(500);
   if(r.Snapshot.Executables.Any(Runtime.ProcessAt)){File.WriteAllText(Path.Combine(r.Snapshot.Directory,"restore-pending.txt"),"Another game process is active; configuration review was skipped. Launch-time backups remain available.");return;}
   Restore(r.Snapshot);
  }catch(Exception e){File.WriteAllText(Path.Combine(r.Snapshot.Directory,"restore-error.txt"),e.ToString());}
 }
}
