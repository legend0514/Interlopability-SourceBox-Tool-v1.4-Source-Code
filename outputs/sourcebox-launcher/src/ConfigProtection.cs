using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;
public static class ConfigProtection {
 public record Entry(string Target,string? Backup);
 public record Snapshot(string Directory,Entry[] Files,string[] Executables);
 public record Request(Snapshot Snapshot,OwnedProcess Process);
 public static Snapshot Capture(string root,string[] executables,string session){
  var dir=Path.Combine(session,"config-backup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var files=new List<Entry>();
  foreach(var name in new[]{"config.cfg","autoexec.cfg","video.txt","video_default.txt"}){
   var target=CrossplaySetup.Destination(root,"cfg/"+name);string? backup=null;
   if(File.Exists(target)){backup=Path.Combine(dir,name);File.Copy(target,backup);}
   files.Add(new(target,backup));
  }
  var result=new Snapshot(dir,files.ToArray(),executables);File.WriteAllText(Path.Combine(dir,"snapshot.json"),JsonSerializer.Serialize(result));return result;
 }
 public static void Restore(Snapshot snapshot){
  foreach(var entry in snapshot.Files){
   // Keep both the original and session-written versions, including newly created files.
   var root=Path.GetDirectoryName(Path.GetDirectoryName(entry.Target))!;
   CrossplaySetup.Destination(root,Path.GetRelativePath(root,entry.Target));
   if(File.Exists(entry.Target)){
    if(entry.Backup!=null&&CrossplaySetup.Hash(entry.Target)==CrossplaySetup.Hash(entry.Backup))continue;
    File.Copy(entry.Target,Path.Combine(snapshot.Directory,"session-"+Path.GetFileName(entry.Target)),true);
   }
   if(entry.Backup!=null){Directory.CreateDirectory(Path.GetDirectoryName(entry.Target)!);var temporary=entry.Target+".sourcebox-"+Guid.NewGuid().ToString("N");File.Copy(entry.Backup,temporary);File.Move(temporary,entry.Target,true);}
   else if(File.Exists(entry.Target))File.Move(entry.Target,Path.Combine(snapshot.Directory,"created-"+Path.GetFileName(entry.Target)));
  }
  File.WriteAllText(Path.Combine(snapshot.Directory,"restored.txt"),DateTime.UtcNow.ToString("O"));
 }
 public static void Watch(Snapshot snapshot,OwnedProcess process){
  var request=Path.Combine(snapshot.Directory,"watch.json");File.WriteAllText(request,JsonSerializer.Serialize(new Request(snapshot,process)));
  var pi=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
  if(Path.GetFileNameWithoutExtension(pi.FileName).Equals("dotnet",StringComparison.OrdinalIgnoreCase))pi.ArgumentList.Add(typeof(ConfigProtection).Assembly.Location);
  pi.ArgumentList.Add("--protect-config");pi.ArgumentList.Add(request);Process.Start(pi)?.Dispose();
 }
 public static async Task Run(string file){
  var r=JsonSerializer.Deserialize<Request>(File.ReadAllText(file))!;
  try{
   while(r.Process.Matches())await Task.Delay(500);
   if(r.Snapshot.Executables.Any(Runtime.ProcessAt)){File.WriteAllText(Path.Combine(r.Snapshot.Directory,"restore-pending.txt"),"Another game process is active. Originals and session configuration are preserved; automatic restoration was skipped.");return;}
   Restore(r.Snapshot);
  }catch(Exception e){File.WriteAllText(Path.Combine(r.Snapshot.Directory,"restore-error.txt"),e.ToString());}
 }
}
