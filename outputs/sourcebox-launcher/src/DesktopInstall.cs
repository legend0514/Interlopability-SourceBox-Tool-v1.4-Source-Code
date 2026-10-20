using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
namespace SourceBox;

public static class DesktopInstall {
 public record Entry(string Path,string Sha256);
 public static string Resolve(string root,string relative){
  if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':'))throw new InvalidDataException("Invalid package path.");
  var full=Path.GetFullPath(Path.Combine(root,relative));
  if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package path escapes its folder.");
  return full;
 }
 public static Entry[] Verify(string root){
  var entries=JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(Path.Combine(root,"package-manifest.json")))??throw new InvalidDataException("Missing package manifest.");
  if(entries.Length==0||!entries.Any(e=>e.Path=="SourceBox.exe"))throw new InvalidDataException("Incomplete SourceBox package.");
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var e in entries){
   if(!seen.Add(e.Path))throw new InvalidDataException("Duplicate package entry.");
   var full=Resolve(root,e.Path);
   for(var dir=new DirectoryInfo(Path.GetDirectoryName(full)!);dir!=null&&dir.FullName.StartsWith(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase);dir=dir.Parent)
    if((dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked package folders are unsupported.");
   if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked package files are unsupported.");
   using var stream=File.OpenRead(full);
   if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(e.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package verification failed: "+e.Path);
  }
  return entries;
 }
 public static string Install(string source,string? installationRoot=null,bool shortcuts=true){
  var entries=Verify(source);
  var root=installationRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","SourceBox");
  root=Path.GetFullPath(root);
  var target=Path.Combine(root,"app");var exe=Path.Combine(target,"SourceBox.exe");
  if(Path.GetFullPath(source).TrimEnd('\\').Equals(target,StringComparison.OrdinalIgnoreCase)){if(shortcuts)Shortcuts(exe);return exe;}
  if(Runtime.ProcessAt(exe))throw new IOException("Close the installed SourceBox window before updating it. Games can stay open.");
  Directory.CreateDirectory(root);var stage=Path.Combine(root,"staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  foreach(var entry in entries){var output=Resolve(stage,entry.Path);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(Resolve(source,entry.Path),output);}
  File.Copy(Path.Combine(source,"package-manifest.json"),Path.Combine(stage,"package-manifest.json"));Verify(stage);
  var backup=Path.Combine(root,"previous-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));bool moved=false;
  try{if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}Directory.Move(stage,target);}
  catch{if(moved&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
  RelocateSettings(source,target);
  if(shortcuts)Shortcuts(exe);return exe;
 }
 internal static void RelocateSettings(string source,string target){
  var existed=File.Exists(Settings.FilePath);
  var settings=existed?JsonSerializer.Deserialize<Settings>(File.ReadAllText(Settings.FilePath))??new Settings():new Settings();
  bool Bundled(string path){if(!Path.IsPathFullyQualified(path))return true;var full=Path.GetFullPath(path);return new[]{source,target}.Any(root=>full.StartsWith(Path.GetFullPath(Path.Combine(root,"runtime"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));}
  if(File.Exists(Path.Combine(target,"runtime/sourcebox/core.mjs"))&&(!existed||Bundled(settings.ProjectRoot)))settings.ProjectRoot=Path.Combine(target,"runtime/sourcebox");
  if(File.Exists(Path.Combine(target,"runtime/sdk/mod_tf_win64.exe"))&&(!existed||Bundled(settings.SdkRoot)))settings.SdkRoot=Path.Combine(target,"runtime/sdk");
  AutoDiscovery.Resolve(settings,target,AutoDiscovery.SteamRoots(settings.SteamRoot),(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator));
  settings.Save();
 }
 public static string ExportZip(string source,string output){
  var entries=Verify(source);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
  using(var stream=new FileStream(output,FileMode.CreateNew))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create)){
   foreach(var entry in entries)zip.CreateEntryFromFile(Resolve(source,entry.Path),entry.Path.Replace('\\','/'),CompressionLevel.Optimal);
   zip.CreateEntryFromFile(Path.Combine(source,"package-manifest.json"),"package-manifest.json");
  }
  using(var stream=File.OpenRead(output))File.WriteAllText(output+".sha256",Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()+"  "+Path.GetFileName(output)+Environment.NewLine);
  return output;
 }
 static void Shortcuts(string exe){
  var type=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("Windows shortcut service is unavailable.");
  dynamic shell=Activator.CreateInstance(type)!;
  try{
   foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"SourceBox")}){
    Directory.CreateDirectory(folder);dynamic shortcut=shell.CreateShortcut(Path.Combine(folder,"SourceBox.lnk"));
    try{shortcut.TargetPath=exe;shortcut.WorkingDirectory=Path.GetDirectoryName(exe);shortcut.IconLocation=Path.Combine(Path.GetDirectoryName(exe)!,"Assets","sourcebox.ico")+",0";shortcut.Description="SourceBox";shortcut.Save();}
    finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);}
   }
  }finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
 }
 public static void Run(bool quiet){
  try{var exe=Install(AppContext.BaseDirectory);Directory.CreateDirectory(Settings.Home);File.WriteAllText(Path.Combine(Settings.Home,"installation.txt"),exe);if(!quiet)MessageBox.Show("SourceBox is installed. Open it from your desktop or Start menu.","SourceBox");}
  catch(Exception e){Environment.ExitCode=1;if(!quiet)MessageBox.Show(e.Message,"SourceBox installation",MessageBoxButtons.OK,MessageBoxIcon.Error);else{Directory.CreateDirectory(Settings.Home);File.WriteAllText(Path.Combine(Settings.Home,"installation-error.txt"),e.ToString());}}
 }
}
