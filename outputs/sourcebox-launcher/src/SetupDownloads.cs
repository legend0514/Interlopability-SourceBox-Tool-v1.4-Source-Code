using System.IO.Compression;
using System.Net.Http;
namespace SourceBox;

public static class SetupDownloads {
 public static async Task<string> Blender(CancellationToken ct,Action<string> log){
  var installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation/Blender 5.2/blender.exe");
  if(File.Exists(installed))return installed;
  log("Preparing a private copy of Blender 5.2.1 for local content conversion. This is a large first-run download.");
  var root=await Dependency("blender-5.2.1","https://download.blender.org/release/Blender5.2/blender-5.2.1-windows-x64.zip","0e631dad7d0cad6d5d18abdd2e2550f6c0213215334eda00ddbd3d22b96ecb2c",ct,log,"blender-5.2.1-windows-x64");
  var executable=Path.Combine(root,"src/blender.exe");if(!File.Exists(executable))throw new IOException("Blender archive does not contain its expected executable.");return executable;
 }
 public static void Extract(string archive,string destination){
  using var zip=ZipFile.OpenRead(archive);long total=0;
  foreach(var entry in zip.Entries){
   var relative=entry.FullName.Replace('\\','/');
   if(relative.EndsWith('/'))continue;
   var output=DesktopInstall.Resolve(destination,relative);
   if(((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("Linked dependency entry.");
   total=checked(total+entry.Length);if(total>2L*1024*1024*1024)throw new InvalidDataException("Dependency exceeds extraction limit.");
   Directory.CreateDirectory(Path.GetDirectoryName(output)!);entry.ExtractToFile(output,false);
  }
 }
 public static async Task<string> SourceIo(CancellationToken ct,Action<string> log){
  const string hash="6B59D292966BB80D9D87C867CD1C56943A35F6E6A338ACB37341CFCC49ABD44A";
  const string revision="6deac3176bf0e8dd2a50038cfa1066abfa58c1f9";
  var directory=await Dependency("sio-"+revision[..8],"https://codeload.github.com/REDxEYE/SourceIO/zip/"+revision,hash,ct,log,"SourceIO-"+revision);
  return Path.Combine(directory,"src");
 }
 public static async Task<string> Dependency(string name,string url,string hash,CancellationToken ct,Action<string> log,string? shortenRoot=null){
  if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9.-]+$")||new Uri(url).Scheme!="https")throw new InvalidDataException("Invalid dependency definition.");
  hash=hash.ToUpperInvariant();var directory=Path.Combine(Settings.Home,"dependencies",name);
  if(SharedAssetPreparation.Valid(directory,hash))return directory;
  Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
  var stage=directory+"-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
  var archive=stage+".zip";
  log("Downloading "+name+"…");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
  deadline.CancelAfter(TimeSpan.FromMinutes(20));ct=deadline.Token;
  using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(20)};
  using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct)){
   if(!response.IsSuccessStatusCode)throw new HttpRequestException("Download of "+name+" from "+new Uri(url).Host+" failed: HTTP "+(int)response.StatusCode+" ("+response.ReasonPhrase+"). This is a dependency download, not Steam authentication.",null,response.StatusCode);
   await using var stream=await response.Content.ReadAsStreamAsync(ct);await using var output=File.Create(archive);
   var buffer=new byte[81920];long bytes=0;int count;
   while((count=await stream.ReadAsync(buffer,ct))>0){bytes+=count;if(bytes>512L*1024*1024)throw new InvalidDataException("Dependency download is too large.");await output.WriteAsync(buffer.AsMemory(0,count),ct);}
  }
  if(CrossplaySetup.Hash(archive)!=hash)throw new InvalidDataException("Converter download failed checksum verification.");
  Extract(archive,stage);
  if(shortenRoot!=null)Directory.Move(DesktopInstall.Resolve(stage,shortenRoot),Path.Combine(stage,"src"));
  SharedAssetPreparation.Receipt(stage,hash);
  // A failed previous extraction is preserved for diagnostics, never overlaid.
  if(Directory.Exists(directory))Directory.Move(directory,directory+"-previous-"+Guid.NewGuid().ToString("N"));
  Directory.Move(stage,directory);File.Delete(archive);return directory;
 }
}
