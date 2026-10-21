using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO.Compression;
namespace SourceBox;
public sealed class OwnedProcess {
 public required string Component {get;set;} public required string Exe {get;set;} public required int Pid {get;set;} public required long StartTicks {get;set;}
 public bool Matches(){try{using var p=Process.GetProcessById(Pid);return p.StartTime.ToUniversalTime().Ticks==StartTicks&&Path.GetFullPath(p.MainModule!.FileName).Equals(Path.GetFullPath(Exe),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
 public void Stop(){if(!Matches())return;using var p=Process.GetProcessById(Pid);if(!p.CloseMainWindow()||!p.WaitForExit(2000)){p.Kill(false);p.WaitForExit(5000);}}
}
public sealed class Runtime {
 public readonly GameWindowLayout Layout=new();
 public Settings Settings;public List<GameInstall> Games=new();public Dictionary<string,LiveGame> Live=new();
 public DestinationCatalog Catalog=new(new(),"Not scanned",false);
 public ContentProfile Content=new(1,Array.Empty<ContentSource>(),Array.Empty<ContentWorld>());
 public string State="Idle",CoreStatus="Not checked",CoreLog="";public System.Collections.Concurrent.ConcurrentBag<OwnedProcess> Owned=new();
 public readonly List<string> Timeline=new();public event Action? Changed;
 public string SessionDir;private readonly SemaphoreSlim gate=new(1,1);private readonly object logLock=new();
 public Runtime(Settings settings){Settings=settings;SessionDir=Path.Combine(Settings.Home,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Environment.ProcessId);Directory.CreateDirectory(SessionDir);Log("Launcher opened. Existing game processes are external and will not be stopped.");}
 public void Log(string msg,bool verbose=false){if(verbose&&!Settings.VerboseLogging)return;lock(logLock){var line=DateTime.Now.ToString("HH:mm:ss")+"  "+msg;Timeline.Add(line);if(Timeline.Count>500)Timeline.RemoveAt(0);File.AppendAllText(Path.Combine(SessionDir,"timeline.log"),line+Environment.NewLine);}Changed?.Invoke();}
 public string[] Lines(){lock(logLock)return Timeline.ToArray();}
 public static bool PortListening(int port)=>IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p=>p.Port==port&&IPAddress.IsLoopback(p.Address));
 public static bool UdpBusy(int port)=>IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(p=>p.Port==port);
 public static bool ProcessAt(string exe){if(!File.Exists(exe))return false;exe=Path.GetFullPath(exe);foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))using(p)try{if(p.MainModule?.FileName.Equals(exe,StringComparison.OrdinalIgnoreCase)==true)return true;}catch{}return false;}
 public void Refresh(){
  if(AutoDiscovery.Resolve(Settings))Settings.Save();
  Games=Discovery.Scan(Settings);
  Catalog=Destinations.Read(Settings.ProjectRoot);
  Content=SharedContent.Scan(Settings.ProjectRoot,Games,Catalog);
  SharedContent.Save(Content,Path.Combine(SessionDir,"content-profile.json"));
  if(string.IsNullOrEmpty(CoreLog)||!File.Exists(CoreLog)){
   var logs=Path.Combine(Settings.ProjectRoot,"logs");var candidates=Directory.Exists(logs)?Directory.GetFiles(logs,"core*.jsonl").ToList():new List<string>();var prior=Path.Combine(Settings.Home,"logs");if(Directory.Exists(prior))candidates.AddRange(Directory.GetFiles(prior,"core.log",SearchOption.AllDirectories));CoreLog=candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()??"";
  }
  Layout.Apply(Games);
  Live=Telemetry.Read(CoreLog,DateTime.UtcNow);
  Log(HealthSummary(Games,Live),true);
  CoreStatus=PortListening(27841)?Live.Values.Any(x=>x.State.StartsWith("Connected"))?"Online":"Waiting for players":"Offline";
  foreach(var p in Owned.Where(p=>!p.Matches()).ToArray()){
   // Keep ownership receipts for restart eligibility, but never stop a reused PID.
   if(!reportedExits.Contains(p.Pid)&&!Owned.Any(other=>other.Component==p.Component&&other.Matches())){reportedExits.Add(p.Pid);Log(p.Component+" exited. Restart is available; see component log.");}
  }
  Changed?.Invoke();
 }
 internal static string HealthSummary(IEnumerable<GameInstall> games,IReadOnlyDictionary<string,LiveGame> live)=>
  "Health poll: "+games.Count(g=>g.Ready)+" prepared; "+live.Count+" observed; "+live.Count(p=>p.Value.State.StartsWith("Connected"))+" connected; "+live.Count(p=>p.Value.State=="Connected")+" sending player updates. "+string.Join("; ",live.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value.State+(p.Value.World.Length>0?" @ "+p.Value.World:"")))+". Player updates do not confirm remote rendering.";
 private readonly HashSet<int> reportedExits=new();
 internal bool TestClassMenu;
 private OwnedProcess Spawn(string component,string exe,IEnumerable<string> args,string cwd,bool hidden=false,Dictionary<string,string>? env=null){
  if(hidden){
   var token=component+"-"+Guid.NewGuid().ToString("N");
   var request=new ServiceRequest(exe,args.ToArray(),cwd,component,Path.Combine(SessionDir,component+".log"),Path.Combine(SessionDir,token+".receipt.json"));
   var owned=ServiceHost.Start(request,Path.Combine(SessionDir,token+".request.json"));Owned.Add(owned);SaveReceipts();Log("Started "+component+" (owned PID "+owned.Pid+").");return owned;
  }
  var pi=new ProcessStartInfo(exe){WorkingDirectory=cwd,UseShellExecute=false,CreateNoWindow=hidden,RedirectStandardOutput=hidden,RedirectStandardError=hidden};foreach(var a in args)pi.ArgumentList.Add(a);if(env!=null)foreach(var e in env)pi.Environment[e.Key]=e.Value;
  var p=Process.Start(pi)??throw new Exception("Could not start "+component);
  var receipt=new OwnedProcess{Component=component,Exe=Path.GetFullPath(exe),Pid=p.Id,StartTicks=p.StartTime.ToUniversalTime().Ticks};Owned.Add(receipt);SaveReceipts();
  Log("Started "+component+" (owned PID "+p.Id+").");return receipt;
 }
 private void SaveReceipts()=>File.WriteAllText(Path.Combine(SessionDir,"ownership.json"),JsonSerializer.Serialize(Owned));
 private static async Task Wait(Func<bool> ready,string detail,CancellationToken ct,int seconds=120){for(int i=0;i<seconds*4;i++){ct.ThrowIfCancellationRequested();if(ready())return;await Task.Delay(250,ct);}throw new TimeoutException(detail);}
 public async Task PrepareCrossplay(string[] ids,CancellationToken ct){
  var games=Discovery.Scan(Settings).Where(g=>ids.Contains(g.Def.Id)).ToArray();
  if(games.Length!=ids.Distinct().Count())throw new Exception("Unknown game selection.");
  var failures=new List<string>();
  if(games.Any(g=>!g.Ready)&&File.Exists(Settings.NodePath)){
   try{await SharedAssetPreparation.Prepare(Settings,Discovery.Scan(Settings),ct,line=>Log(line),ids);}
   catch(Exception e)when(e is IOException or TimeoutException or System.Net.Http.HttpRequestException){
    ct.ThrowIfCancellationRequested();failures.Add(e.Message);Log("Content preparation stopped: "+e.Message+" Checking which games can still be set up.");
   }
  }
  var backup=Path.Combine(Settings.Home,"setup",DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N"));
  bool Running(string id){var g=games.Single(g=>g.Def.Id==id);return ProcessAt(g.Exe)||(id=="gmod"&&ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe")));}
  failures.AddRange(ApplySetupPlans(ids,id=>CrossplaySetup.Plan(Settings,games.Single(g=>g.Def.Id==id)),backup,ct,Running,line=>Log(line)));
  Refresh();
  if(failures.Count>0)throw new IOException("Some setup steps failed. Successfully prepared games were kept and can be started separately.\n\n"+string.Join("\n\n",failures.Distinct()));
  Log("Adapter setup completed. Backups: "+backup);
 }
 internal static List<string> ApplySetupPlans(IEnumerable<string> ids,Func<string,SetupPlan> plan,string backup,CancellationToken ct,Func<string,bool> running,Action<string> log){
  var failures=new List<string>();
  foreach(var id in ids){
   ct.ThrowIfCancellationRequested();
   try{CrossplaySetup.Apply(new[]{plan(id)},Path.Combine(backup,id),ct,running,log);log(id+" adapter setup completed.");}
   catch(Exception e)when(e is IOException or InvalidOperationException){failures.Add(id+": "+e.Message);log(id+" setup failed: "+e.Message);}
  }
  return failures;
 }
 public async Task Start(string[] ids,CancellationToken ct){
  if(!await gate.WaitAsync(0,ct))throw new Exception("Another session action is in progress.");
  try{
   State="Scanning";Refresh();Settings.Validate();
   if(ids.Length==0)throw new Exception("Select at least one game in Library.");
   await EnsureSteam(ct);
   if(!File.Exists(Path.Combine(Settings.ProjectRoot,"core.mjs")))throw new Exception("Your installed games were scanned, but the SourceBox crossplay runtime is missing. This launcher package does not yet include automatic crossplay installation.");
   if(!File.Exists(Settings.NodePath))throw new Exception("Choose node.exe in Settings.");
   using(var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Settings.ProjectRoot,"sheets/runtime.json"))))if(d.RootElement.GetProperty("port").GetInt32()!=27841||d.RootElement.GetProperty("host").GetString()!="127.0.0.1")throw new Exception("This adapter build requires loopback 27841. Rebuild adapters before changing its port.");
   // Installed files may belong to an older release. Refresh stopped adapters
   // even when every expected filename already exists.
   var unprepared=Games.Where(g=>ids.Contains(g.Def.Id)&&(!g.Ready||
    (!ProcessAt(g.Exe)&&!(g.Def.Id=="gmod"&&ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe")))&&CrossplaySetup.Plan(Settings,g).Files.Count>0))).Select(g=>g.Def.Id).ToArray();
   if(unprepared.Length>0){State="Preparing crossplay";Changed?.Invoke();await PrepareCrossplay(unprepared,ct);}
   foreach(var id in ids){var g=Games.Single(x=>x.Def.Id==id);if(!g.Ready)throw new Exception(g.Def.Title+": "+g.Detail);}
   Layout.Selected.UnionWith(ids);
   State="Starting";Changed?.Invoke();
   if(!PortListening(27841)){
    var pi=new ProcessStartInfo(Settings.NodePath){WorkingDirectory=Settings.ProjectRoot,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};pi.ArgumentList.Add("preflight.mjs");
    using var pre=Process.Start(pi)!;var output=pre.StandardOutput.ReadToEndAsync();var errors=pre.StandardError.ReadToEndAsync();try{await pre.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(30),ct);}catch{if(!pre.HasExited)pre.Kill();throw;}if(pre.ExitCode!=0)throw new Exception("Configuration preflight failed: "+await errors+await output);
    CoreLog=Path.Combine(SessionDir,"core.log");var core=Spawn("core",Settings.NodePath,new[]{"core.mjs"},Settings.ProjectRoot,true);
    await Wait(()=>core.Matches()&&File.Exists(CoreLog)&&File.ReadAllText(CoreLog).Contains("\"listening\""),"Coordinator did not report listening.",ct,15);
   }else if(!Live.Values.Any(x=>x.State.StartsWith("Connected"))&&!Owned.Any(x=>x.Component=="core"&&x.Matches()))throw new Exception("Port 27841 is occupied, but no fresh Sourcebox log confirms its identity. Select the correct project; existing process was left untouched.");
   else Log("Using existing coordinator; it remains external unless this launcher started it.");
   // Initialize Portal 2 before the newer clients contend for startup resources.
   foreach(var id in ids.OrderBy(id=>id=="portal2"?0:id==Settings.PreferredGame?1:2)){
    ct.ThrowIfCancellationRequested();var g=Games.Single(x=>x.Def.Id==id);
    var running=ProcessAt(g.Exe)||(id=="gmod"&&ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe")));
    if(running){Log(g.Def.Title+" is already running; observing without taking ownership.");continue;}
    await LaunchGame(g,ct);
    State="Connecting";Changed?.Invoke();
    await Wait(()=>{Live=Telemetry.Read(CoreLog,DateTime.UtcNow);return Live.TryGetValue(id,out var state)&&state.State.StartsWith("Connected");},g.Def.Title+" started but did not connect within 120 seconds. Check its native console and adapter setup.",ct);
    Log(g.Def.Title+" connected (fresh coordinator event).");
   }
   Refresh();State=ids.All(id=>Live.TryGetValue(id,out var l)&&l.State.StartsWith("Connected"))?"Running":"Connecting / external clients";
  }catch(OperationCanceledException){State="Idle";Log("Launch cancelled. Already started components remain owned; Stop closes them.");throw;}catch(Exception e){State="Error";Log(e.Message);throw;}finally{gate.Release();Changed?.Invoke();}
 }
 private async Task EnsureSteam(CancellationToken ct){
  var exe=Path.Combine(Settings.SteamRoot,"steam.exe");
  if(!File.Exists(exe))throw new IOException("Steam could not be found. Choose its installation folder in Settings.");
  if(!ProcessAt(exe)){
   Log("Opening Steam before launching games…");
   Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Settings.SteamRoot,Arguments="-silent"});
  }
  await Wait(()=>{
   if(!ProcessAt(exe))return false;
   using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
   return key?.GetValue("ActiveUser") is int user&&user!=0;
  },"Steam is open but has not signed in. Sign in to Steam, then start the games again.",ct,90);
 }
 private async Task LaunchGame(GameInstall g,CancellationToken ct){
  var id=g.Def.Id;var root=g.IdRoot();
  var ordered=Games.Where(x=>Layout.Selected.Contains(x.Def.Id)).Select(x=>x.Def.Id).ToArray();
  var tile=GameWindowLayout.Slots(Layout.Monitor,ordered)[id];
  if(UdpBusy(g.Def.Port))throw new Exception(g.Def.Title+" port "+g.Def.Port+" is already occupied; no process was stopped.");
  var args=new List<string>{"-insecure","-multirun","-windowed","-noborder","-w",tile.Width.ToString(),"-h",tile.Height.ToString(),"-x",tile.X.ToString(),"-y",tile.Y.ToString(),"-novid","-console","-condebug","-nojoy","+fps_max",Settings.FpsLimit.ToString(),"-port",g.Def.Port.ToString(),"+sv_lan","1"};
  if(id is "hl2" or "tf2"){
   args.AddRange(new[]{"-game",Path.Combine(root,g.Def.Content),"+sv_cheats","1","+sv_allow_point_servercommand","always","+engine_no_focus_sleep","0","+plugin_load","addons/sourcebox_"+id,"+exec","sourcebox_controls.cfg"});
   if(id=="hl2")args.AddRange(new[]{"+maxplayers","2","+sv_pure","-1"});
   if(id=="tf2"&&TestClassMenu)args.AddRange(new[]{"+sourcebox_class_probe","2"});
  }else if(id=="portal2")args.AddRange(new[]{"-game","portal2","-steam","-allowmultiple","+sv_cheats","1","+engine_no_focus_sleep","0","+r_portal_fastpath","0","+r_portal_stencil_depth","2","+plugin_load","addons/sourcebox_portal2"});
  else if(id=="hl1mp")args.AddRange(new[]{"-game","hl1mp","-tools","+sv_cheats","1","+sv_allow_point_servercommand","always","+engine_no_focus_sleep","0","+plugin_load","addons/sourcebox_hl1mp"});
  else if(id=="gmod"){
   if(PortListening(27842)){
    if(!await BridgeHealth.Matches(Path.Combine(root,"garrysmod/data/sourcebox"),Settings.NodePath))throw new Exception("GMod bridge port 27842 is occupied by "+BridgeHealth.PortOwner(27842)+". It could not be verified for this installation. Stop the old session in its SourceBox launcher, or restart Windows before trying again. No unrelated process was stopped.");
    Log("Using the verified GMod bridge for this installation; it remains external while reconnecting to the coordinator.");
   }else{
    var bridge=Spawn("gmod-bridge",Settings.NodePath,new[]{"gmod-bridge.mjs",Path.Combine(root,"garrysmod/data/sourcebox")},Settings.ProjectRoot,true);
    await Wait(()=>bridge.Matches()&&PortListening(27842),"GMod bridge did not become ready.",ct,10);
   }
   args.AddRange(new[]{"-noworkshop","+maxplayers","1","+gamemode","sandbox","+fps_max_nofocus","30","+sv_pause_sp","0"});
  }else if(id=="cs2"){
   await StartCsServer(g,ct);
   args=new(){"-insecure","-multirun","-windowed","-noborder","-w",tile.Width.ToString(),"-h",tile.Height.ToString(),"-x",tile.X.ToString(),"-y",tile.Y.ToString(),"-novid","-condebug","+fps_max",Settings.FpsLimit.ToString(),"+connect","127.0.0.1:27019"};
  }
  if(id!="cs2")args.AddRange(new[]{"+map","sourcebox_room_001","+sv_pausable","0"});
  if(id!="cs2")args.InsertRange(0,new[]{"-clientport",(g.Def.Port+200).ToString(),"+tv_port",(g.Def.Port+100).ToString()});
  var configuration=id is "gmod" or "portal2" or "hl1mp" or "cs2"?ConfigProtection.Capture(Path.Combine(root,g.Def.Content),id=="gmod"?new[]{g.Exe,Path.Combine(root,"bin/win64/gmod.exe")}:new[]{g.Exe},SessionDir,ConfigProtection.CloudRoots(Settings.SteamRoot,g.Def.App)):null;
  using var protection=configuration==null?null:ConfigProtection.Protect(configuration);
  var receipt=Spawn(id,g.Exe,args,id=="cs2"?Path.Combine(g.Root,"game"):root,false,id=="portal2"?new(){{"SteamAppId","620"},{"VPROJECT",Path.Combine(root,"portal2")}}:null);
  OwnedProcess? observedChild=null;
  if(id=="gmod"&&!Path.GetFullPath(g.Exe).Equals(Path.GetFullPath(Path.Combine(root,"bin/win64/gmod.exe")),StringComparison.OrdinalIgnoreCase)){
   var childExe=Path.GetFullPath(Path.Combine(root,"bin/win64/gmod.exe"));
   try{await Wait(()=>{foreach(var p in Process.GetProcessesByName("gmod"))using(p)try{if(p.Id!=receipt.Pid&&p.MainModule?.FileName.Equals(childExe,StringComparison.OrdinalIgnoreCase)==true&&ProcessTree.Parent(p.Id)==receipt.Pid&&p.StartTime.ToUniversalTime().Ticks>=receipt.StartTicks){Owned.Add(new(){Component="gmod",Exe=childExe,Pid=p.Id,StartTicks=p.StartTime.ToUniversalTime().Ticks});SaveReceipts();return true;}}catch{}return false;},"Could not verify ownership of GMod's child process; it will remain untouched.",ct,30);}catch(TimeoutException)when(ProcessAt(childExe)){
    observedChild=ObserveStartedProcess(childExe,receipt.StartTicks);
    Log("GMod is running, but its parent relationship changed. Observing it without stop permission; configuration backups remain available in session logs.");
   }
  }
  if(configuration!=null){var owner=observedChild??Owned.Where(o=>o.Component==id&&o.Matches()).OrderByDescending(o=>o.StartTicks).FirstOrDefault()??receipt;
   if(!owner.Matches())throw new IOException(g.Def.Title+" exited or could not be verified before settings protection was attached. Check its console log; no unrelated process was stopped.");
   try{await ConfigProtection.Watch(configuration,owner);}catch{if(observedChild==null)owner.Stop();throw;}
   Log("Existing native config files are protected for this session. Changes to those settings will not be saved by "+g.Def.Title+".");}
 }
 // A settings lease may follow an observed process without granting Stop ownership.
 internal static OwnedProcess? ObserveStartedProcess(string exe,long earliest){
  exe=Path.GetFullPath(exe);
  foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))using(p)try{
   var ticks=p.StartTime.ToUniversalTime().Ticks;
   if(ticks>=earliest&&string.Equals(p.MainModule?.FileName,exe,StringComparison.OrdinalIgnoreCase))
    return new OwnedProcess{Component="observed",Exe=exe,Pid=p.Id,StartTicks=ticks};
  }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
  return null;
 }
 internal static bool ExistingBridgeReady(string file){
  try{
   if(!File.Exists(file)||DateTime.UtcNow-File.GetLastWriteTimeUtc(file)>TimeSpan.FromSeconds(2))return false;
   using var state=JsonDocument.Parse(File.ReadAllText(file));var root=state.RootElement;
   return root.GetProperty("connected").GetBoolean()&&Guid.TryParse(root.GetProperty("coreSession").GetString(),out _)&&Guid.TryParse(root.GetProperty("connection").GetString(),out _);
  }catch(Exception e)when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException){return false;}
 }
 private async Task StartCsServer(GameInstall g,CancellationToken ct){
  var request=Path.Combine(SessionDir,"cs2-bootstrap.json");
  File.WriteAllText(request,JsonSerializer.Serialize(new BootstrapRequest(g.Exe,g.Root,SessionDir)));
  // The helper survives a UI crash and restores gameinfo before allowing the
  // client to start. It never receives user-provided shell commands.
  var host=Environment.ProcessPath!;var args=new List<string>();
  if(Path.GetFileNameWithoutExtension(host).Equals("dotnet",StringComparison.OrdinalIgnoreCase))args.Add(typeof(Runtime).Assembly.Location);
  args.AddRange(new[]{"--cs-bootstrap",request});
  var helperInfo=new ProcessStartInfo(host){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in args)helperInfo.ArgumentList.Add(arg);using var helper=Process.Start(helperInfo)!;
  var result=Path.Combine(SessionDir,"cs2-bootstrap-result.json");
  // Cancellation waits for the helper's restore before returning.
  await Wait(()=>File.Exists(result),"CS2 helper has not finished. It remains responsible for restoring gameinfo; inspect bootstrap logs.",CancellationToken.None,75);
  var response=JsonSerializer.Deserialize<BootstrapResult>(File.ReadAllText(result))!;
  if(response.Process!=null){Owned.Add(response.Process);SaveReceipts();}
  if(!response.Success)throw new Exception(response.Error);
  ct.ThrowIfCancellationRequested();
 }
 public async Task Stop(){
  await gate.WaitAsync();try{State="Stopping";Changed?.Invoke();
   foreach(var p in Owned.AsEnumerable().Reverse().Where(x=>x.Component!="core"))try{p.Stop();}catch(Exception e){Log("Stop failed for "+p.Component+": "+e.Message);}
   // Preserve an owned coordinator if any external game currently depends on it.
   Refresh();bool external=Games.Any(g=>(ProcessAt(g.Exe)||(g.Def.Id=="gmod"&&ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe"))))&&!Owned.Any(o=>o.Component==g.Def.Id&&o.Matches()));
   foreach(var p in Owned.Where(x=>x.Component=="core"))if(external)Log("Coordinator retained because external games are still running.");else p.Stop();
   State="Idle";Log("Stopped owned components. External sessions were preserved.");
  }finally{gate.Release();Changed?.Invoke();}
 }
 public async Task RestartFailed(string id,CancellationToken ct){if(!Owned.Any(p=>p.Component==id)||Owned.Any(p=>p.Component==id&&p.Matches()))throw new Exception("Restart applies only to a failed component this launcher started.");if(id=="core")await Start(Settings.SelectedGames,ct);else await Start(new[]{id=="cs2-server"?"cs2":id=="gmod-bridge"?"gmod":id},ct);}
 public string Diagnostics()=>Telemetry.Redact("SourceBox 1.5.2-beta.1 | "+State+" | "+CoreStatus+"\n"+string.Join("\n",Games.Select(g=>g.Def.Title+": "+(g.Ready?"prepared":"setup needed")+"; "+(Live.GetValueOrDefault(g.Def.Id)?.State??"No telemetry")))+"\n"+string.Join("\n",Lines()),Settings);
 public string Export(){
  var path=Path.Combine(Settings.Home,"support-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".zip");
  using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
  void Add(string name,string text){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(Telemetry.Redact(text,Settings));}
  Add("diagnostics.txt",Diagnostics());
  foreach(var name in new[]{"timeline.log","cs2-server.log","cs2-bootstrap-result.json","core.log","gmod-bridge.log"}){
   var file=Path.Combine(SessionDir,name);if(!File.Exists(file))continue;
   try{using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);bool tail=stream.Length>1024*1024;stream.Seek(Math.Max(0,stream.Length-1024*1024),SeekOrigin.Begin);using var reader=new StreamReader(stream);if(tail)reader.ReadLine();Add(name,(tail?"[Last 1 MiB of log]\n":"")+reader.ReadToEnd());}
   catch(IOException e){Add(name+".error.txt",e.Message);}
  }
  return path;
 }

}
public static class GameExtensions {public static string IdRoot(this GameInstall g)=>g.Def.Id is "hl2" or "tf2"?Path.GetDirectoryName(g.Exe)!:g.Root;}
public static class ProcessTree {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Entry {public uint Size,Usage,Pid;public IntPtr Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;}
 [DllImport("kernel32.dll")]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32FirstW(IntPtr h,ref Entry e);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32NextW(IntPtr h,ref Entry e);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 public static int Parent(int pid){var h=CreateToolhelp32Snapshot(2,0);if(h==new IntPtr(-1))return 0;try{var e=new Entry{Size=(uint)Marshal.SizeOf<Entry>(),Exe=""};if(Process32FirstW(h,ref e))do{if(e.Pid==pid)return (int)e.Parent;}while(Process32NextW(h,ref e));return 0;}finally{CloseHandle(h);}}
}




