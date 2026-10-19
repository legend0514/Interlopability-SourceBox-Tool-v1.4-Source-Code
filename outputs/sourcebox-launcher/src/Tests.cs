using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;
static class Tests {
 public static async Task<int> LaunchCheck(string[] ids){
  var runtime=new Runtime(Settings.Load());
  var observedLog=Environment.GetEnvironmentVariable("SOURCEBOX_TEST_CORE_LOG");
  if(!string.IsNullOrEmpty(observedLog)&&Path.IsPathFullyQualified(observedLog)&&File.Exists(observedLog))runtime.CoreLog=observedLog;
  using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(20));
  try{
   await runtime.Start(ids,timeout.Token);
   await Task.Delay(TimeSpan.FromSeconds(30),timeout.Token);runtime.Refresh();
   var connected=ids.All(id=>runtime.Live.TryGetValue(id,out var state)&&state.State.StartsWith("Connected"));
   File.WriteAllText(Path.Combine(Settings.Home,"launch-check.json"),JsonSerializer.Serialize(new{
    status=connected?"passed":"failed",kind="Native launch and adapter connectivity only; not gameplay acceptance",
    requested=ids,live=runtime.Live,processes=runtime.Owned.ToArray(),session=runtime.SessionDir
   },new JsonSerializerOptions{WriteIndented=true}));
   return connected?0:1;
  }catch(Exception e){File.WriteAllText(Path.Combine(Settings.Home,"launch-check-error.txt"),e.ToString());return 1;}
  finally{await runtime.Stop();}
 }
 public static async Task<int> LiveCheck(){
  var r=new Runtime(Settings.Load());r.Refresh();var ids=r.Games.Where(g=>g.Ready&&(Runtime.ProcessAt(g.Exe)||(g.Def.Id=="gmod"&&Runtime.ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe"))))).Select(g=>g.Def.Id).ToArray();
  if(ids.Length==0||!Runtime.PortListening(27841))return 2;
  try{
   await r.Start(ids,CancellationToken.None);
   if(r.Owned.Count!=0)throw new Exception("Read-only attachment unexpectedly started a process.");
   var connected=r.Live.Where(x=>x.Value.State.StartsWith("Connected")).ToDictionary(x=>x.Key,x=>x.Value);
   await r.Stop();r.Refresh();
   File.WriteAllText(Path.Combine(Settings.Home,"live-check.json"),JsonSerializer.Serialize(new{status="passed",kind="Attach existing clients; Stop must preserve external processes",observed=ids,connected,owned=r.Owned.Count,games=r.Games,diagnostics=r.Diagnostics()},new JsonSerializerOptions{WriteIndented=true}));
   foreach(var id in ids){var g=r.Games.Single(g=>g.Def.Id==id);if(!Runtime.ProcessAt(g.Exe)&&!(id=="gmod"&&Runtime.ProcessAt(Path.Combine(g.Root,"bin/win64/gmod.exe"))))throw new Exception("External process disappeared: "+id);}
   return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(Settings.Home,"live-check-error.txt"),e.ToString());return 1;}
 }
 public static int Run(){var lines=new List<string>();var temp=Path.Combine(Path.GetTempPath(),"sourcebox-tests-"+Guid.NewGuid());Directory.CreateDirectory(temp);int failures=0;
  void Test(string name,Action action){try{action();lines.Add("PASS "+name);}catch(Exception e){failures++;lines.Add("FAIL "+name+": "+e.Message);}}
  void Check(bool value){if(!value)throw new Exception("Assertion failed");}
   Test("room lighting upgrades require an exact old-room hash and preserve custom maps",()=>{
    var root=Path.Combine(temp,"room-upgrades");Directory.CreateDirectory(Path.Combine(root,"sheets"));var file=Path.Combine(root,"room.bsp");File.WriteAllText(file,"old room");
    var settings=new Settings{ProjectRoot=root};
    File.WriteAllText(Path.Combine(root,"sheets/room-upgrades.json"),JsonSerializer.Serialize(new{version=1,games=new Dictionary<string,object>{{"hl2",new[]{new{bytes=new FileInfo(file).Length,sha256=CrossplaySetup.Hash(file)}}}}}));
    Check(CrossplaySetup.KnownOldRoom(settings,"hl2",file));Check(!CrossplaySetup.KnownOldRoom(settings,"tf2",file));
    File.WriteAllText(file,"new room");Check(!CrossplaySetup.KnownOldRoom(settings,"hl2",file));
    File.WriteAllText(Path.Combine(root,"sheets/room-upgrades.json"),"invalid");Check(!CrossplaySetup.KnownOldRoom(settings,"hl2",file));
   });
   Test("native configuration restoration preserves original settings and every session version",()=>{
    var root=Path.Combine(temp,"native-config");Directory.CreateDirectory(Path.Combine(root,"cfg"));Directory.CreateDirectory(Path.Combine(root,"save"));
    File.WriteAllText(Path.Combine(root,"cfg/config.cfg"),"original");File.WriteAllText(Path.Combine(root,"save/game.sav"),"save remains intact");
    var snapshot=ConfigProtection.Capture(root,Array.Empty<string>(),temp);
    File.WriteAllText(Path.Combine(root,"cfg/config.cfg"),"session");File.WriteAllText(Path.Combine(root,"cfg/video.txt"),"created by game");
    ConfigProtection.Restore(snapshot);
    Check(File.ReadAllText(Path.Combine(root,"cfg/config.cfg"))=="original");
    Check(File.ReadAllText(Path.Combine(snapshot.Directory,"session-config.cfg"))=="session");
    Check(File.ReadAllText(Path.Combine(snapshot.Directory,"created-video.txt"))=="created by game");
    Check(File.ReadAllText(Path.Combine(root,"save/game.sav"))=="save remains intact");
   });
   Test("unselected installed CS2 cannot require Workshop Tools during other-game preparation",()=>{
    var saved=File.ReadAllText(Settings.FilePath);
    try{
     var root=Path.Combine(temp,"selected-only");Directory.CreateDirectory(Path.Combine(root,"fallback"));File.WriteAllText(Path.Combine(root,"fallback/aliases.json"),"{}");
     var settings=new Settings{ProjectRoot=root,SelectedGames=new[]{"hl2"}};
     var cs=new GameInstall(Discovery.Defs.Single(d=>d.Id=="cs2"),Path.Combine(temp,"no-workshop-tools"),"missing.exe",true,false,"");
     SharedAssetPreparation.Prepare(settings,new[]{cs},CancellationToken.None,_=>{},new[]{"hl2"}).GetAwaiter().GetResult();
     Check(!settings.DependencyRoots.ContainsKey("metamod")&&!settings.LocalAssetCaches.ContainsKey("cs2"));
    }finally{File.WriteAllText(Settings.FilePath,saved);}
   });
   Test("native executable selection validates game content and keeps SDK executable separate",()=>{
    var root=Path.Combine(temp,"Other Drive SteamLibrary","Half-Life 2");Directory.CreateDirectory(Path.Combine(root,"hl2"));File.WriteAllText(Path.Combine(root,"hl2/gameinfo.txt"),"game");File.WriteAllText(Path.Combine(root,"hl2/hl2_misc_dir.vpk"),"marker");File.WriteAllText(Path.Combine(root,"hl2.exe"),"fixture");
    Check(Discovery.FindNativeRoot("hl2",Path.Combine(root,"hl2.exe"))==root);
    Check(Discovery.FindNativeRoot("tf2",Path.Combine(root,"hl2.exe"))==null);
    var settings=new Settings{SdkRoot=Path.Combine(temp,"sdk"),Executables=new(){{"hl2",Path.Combine(root,"hl2.exe")}}};
    var game=Discovery.Scan(settings).Single(g=>g.Def.Id=="hl2");Check(game.Installed&&game.Root==root&&game.Exe==Path.Combine(settings.SdkRoot,"mod_hl2_win64.exe"));Check(!game.Ready&&game.Detail.Contains("runtime"));
    Directory.CreateDirectory(settings.SdkRoot);File.WriteAllText(game.Exe,"fixture");var incomplete=Discovery.Scan(settings).Single(g=>g.Def.Id=="hl2");Check(!incomplete.Ready&&incomplete.Detail.Contains("client.dll")&&incomplete.Detail.Contains("server.dll"));
   });
   Test("support export includes bounded server failure evidence and redacts local paths",()=>{
    var runtime=new Runtime(Settings.Load());
    File.WriteAllText(Path.Combine(runtime.SessionDir,"cs2-server.log"),"Load failed at "+runtime.Settings.ProjectRoot);
    File.WriteAllText(Path.Combine(runtime.SessionDir,"cs2-gameinfo.backup"),"private backup");
    using var zip=System.IO.Compression.ZipFile.OpenRead(runtime.Export());
    Check(zip.GetEntry("cs2-server.log")!=null&&zip.GetEntry("cs2-gameinfo.backup")==null);
    using var reader=new StreamReader(zip.GetEntry("cs2-server.log")!.Open());Check(reader.ReadToEnd().Contains("[local path]"));
   });
   Test("one to six game tiles cover a full monitor without gaps or overlap",()=>{
    foreach(var bounds in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-1919,83,1919,1079)})for(int n=1;n<=6;n++){
     var tiles=GameWindowLayout.Tiles(bounds,n);Check(tiles.Length==n);
     Check(tiles.Sum(r=>(long)r.Width*r.Height)==(long)bounds.Width*bounds.Height);
     for(int i=0;i<n;i++){Check(bounds.Contains(tiles[i]));for(int j=i+1;j<n;j++)Check(!tiles[i].IntersectsWith(tiles[j]));}
    }
    var four=GameWindowLayout.Tiles(new Rectangle(0,0,1920,1080),4);Check(four.All(r=>r.Size==new Size(960,540)));
    var two=GameWindowLayout.Tiles(new Rectangle(0,0,1920,1080),2);Check(two.All(r=>r.Size==new Size(960,1080)));
   });
   Test("game slots stay reserved while peers load or disconnect",()=>{
    var ids=new[]{"hl2","tf2","hl1mp","gmod","cs2","portal2"};
    var slots=GameWindowLayout.Slots(new Rectangle(-2560,0,2560,1440),ids);
    foreach(var visible in new[]{new[]{"hl2"},new[]{"cs2","tf2"},ids.Reverse().ToArray()}){
     foreach(var id in visible)Check(slots[id]==GameWindowLayout.Tiles(new Rectangle(-2560,0,2560,1440),6)[Array.IndexOf(ids,id)]);
    }
    Check(GameWindowLayout.Slots(new Rectangle(0,0,1920,1080),new[]{"tf2","tf2"}).Count==1);
   });
   Test("native game proportions survive portrait, ultrawide and scaled monitor tiles",()=>{
    foreach(var bounds in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-3840,47,3840,2160),new Rectangle(0,0,3440,1440)})
    for(int n=1;n<=6;n++)foreach(var tile in GameWindowLayout.Tiles(bounds,n))
    foreach(var native in new[]{new Size(1920,1080),new Size(1024,768),new Size(960,1080),tile.Size}){
     var fit=GameWindowLayout.Fit(tile,native);Check(tile.Contains(fit));
     Check(Math.Abs((double)fit.Width/fit.Height-(double)native.Width/native.Height)<0.01);
     Check(Math.Abs((fit.Left-tile.Left)-(tile.Right-fit.Right))<=1);
     Check(Math.Abs((fit.Top-tile.Top)-(tile.Bottom-fit.Bottom))<=1);
     if(native==tile.Size)Check(fit==tile);
    }
   });
   Test("fallback selection preserves installed and prepared models",()=>{
    var ids=new HashSet<string>{"tf2"};var cache=Path.Combine(temp,"real");Directory.CreateDirectory(Path.Combine(cache,"models"));File.WriteAllText(Path.Combine(cache,"models/real.mdl"),"real");
    Check(!FallbackAssets.Needed("models/real.mdl","always",ids,new[]{cache}));
    Check(!FallbackAssets.Needed("models/player.mdl","tf2",ids,new[]{cache}));
    Check(FallbackAssets.Needed("models/peer.mdl","always",ids,new[]{cache}));
    Check(FallbackAssets.Needed("models/player.mdl","gmod",ids,new[]{cache}));
   });
   Test("packaged fallback prepares peers with no source games and yields to real models",()=>{
    var saved=File.ReadAllText(Settings.FilePath);
    try{
     var settings=Settings.Load();settings.LocalAssetCaches.Clear();
     FallbackAssets.Prepare(settings,Array.Empty<string>(),_=>{});
     var model="models/sourcebox/portal2/peers/heavy.mdl";
     var fallback=settings.LocalAssetCaches["fallback"];
     Check(File.Exists(Path.Combine(fallback,model))&&File.Exists(Path.Combine(fallback,"materials/sourcebox/fallback/cyan.vtf")));
     Check(!ReleaseRequirements.Missing(settings,"portal2",temp).Any());
     var real=Path.Combine(temp,"converted");Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(real,model))!);File.WriteAllText(Path.Combine(real,model),"real model fixture");settings.LocalAssetCaches["portal-peers"]=real;
     FallbackAssets.Prepare(settings,new[]{"tf2"},_=>{});
     Check(!File.Exists(Path.Combine(settings.LocalAssetCaches["fallback"],model)));
     Check(settings.LocalAssetCaches["portal-peers"]==real);
    }finally{File.WriteAllText(Settings.FilePath,saved);}
   });
   Test("character selection hit tests current position at multiple window sizes",()=>{
   using var canvas=new SceneCanvas();
   foreach(var size in new[]{new Size(1200,720),new Size(960,720)}){
    canvas.Size=size;canvas.Characters=true;
    using var bitmap=new Bitmap(size.Width,size.Height);canvas.DrawToBitmap(bitmap,new Rectangle(Point.Empty,size));
    float scale=Math.Min(size.Width/1200f,size.Height/720f);float ox=(size.Width-1200*scale)/2,oy=(size.Height-720*scale)/2;
    var ids=new[]{"hl2","tf2","hl1mp","gmod","portal2","cs2"};
    for(int i=0;i<6;i++)Check(canvas.HitTest(new Point((int)(ox+(215+i%3*382)*scale),(int)(oy+(i<3?275:480)*scale)))==ids[i]);
    canvas.Characters=false;Check(canvas.HitTest(new Point(215,275))=="");
   }
  });
  Test("automatic discovery repairs stale paths and preserves valid overrides",()=>{
   var app=Path.Combine(temp,"portable");var core=Path.Combine(app,"runtime","sourcebox");var sdk=Path.Combine(app,"runtime","sdk");var node=Path.Combine(app,"runtime","node");var steam=Path.Combine(temp,"steam-auto");
   foreach(var folder in new[]{app,core,sdk,node,steam})Directory.CreateDirectory(folder);
   File.WriteAllText(Path.Combine(core,"core.mjs"),"");File.WriteAllText(Path.Combine(sdk,"mod_tf_win64.exe"),"");File.WriteAllText(Path.Combine(node,"node.exe"),"");
   var s=new Settings{ProjectRoot=Path.Combine(temp,"old-pc"),SteamRoot=Path.Combine(temp,"missing-steam")};
   Check(AutoDiscovery.Resolve(s,app,new[]{steam},Array.Empty<string>()));Check(s.ProjectRoot==core&&s.SdkRoot==sdk&&s.NodePath==Path.Combine(node,"node.exe")&&s.SteamRoot==steam);
   Check(!AutoDiscovery.Resolve(s,app,new[]{steam},Array.Empty<string>()));
   var custom=Path.Combine(temp,"custom");Directory.CreateDirectory(custom);File.WriteAllText(Path.Combine(custom,"core.mjs"),"");s.ProjectRoot=custom;AutoDiscovery.Resolve(s,app,new[]{steam},Array.Empty<string>());Check(s.ProjectRoot==custom);
  });
  Test("crossplay setup validates all prerequisites before writing",()=>{
   var root=Path.Combine(temp,"setup-target");Directory.CreateDirectory(root);var source=Path.Combine(temp,"adapter.dll");File.WriteAllText(source,"new");
   var copy=new SetupCopy(source,root,"addons/adapter.dll",CrossplaySetup.Hash(source));var target=Path.Combine(root,"addons/adapter.dll");
   bool refused=false;try{CrossplaySetup.Apply(new[]{new SetupPlan("hl2",new(){copy},new(){"Missing runtime"})},Path.Combine(temp,"no-backup"),CancellationToken.None,_=>false,_=>{});}catch(InvalidOperationException){refused=true;}Check(refused&&!File.Exists(target));
   refused=false;try{CrossplaySetup.Apply(new[]{new SetupPlan("hl2",new(){copy},new())},Path.Combine(temp,"no-backup2"),CancellationToken.None,_=>true,_=>{});}catch(IOException){refused=true;}Check(refused&&!File.Exists(target));
   refused=false;try{CrossplaySetup.Destination(root,"../escape.dll");}catch(InvalidDataException){refused=true;}Check(refused);
  });
  Test("crossplay setup verifies payloads and rolls back partial updates",()=>{
   var root=Path.Combine(temp,"setup-rollback");Directory.CreateDirectory(root);var source=Path.Combine(temp,"update.dll");File.WriteAllText(source,"new");var hash=CrossplaySetup.Hash(source);
   File.WriteAllText(Path.Combine(root,"existing.dll"),"old");
   var plan=new SetupPlan("tf2",new(){new(source,root,"existing.dll",hash),new(source,root,"added.dll",hash)},new());
   bool failed=false;try{CrossplaySetup.Apply(new[]{plan},Path.Combine(temp,"rollback-receipt"),CancellationToken.None,_=>false,_=>throw new IOException("fixture interruption"));}catch(IOException){failed=true;}
   Check(failed&&File.ReadAllText(Path.Combine(root,"existing.dll"))=="old"&&!File.Exists(Path.Combine(root,"added.dll")));
   CrossplaySetup.Apply(new[]{plan},Path.Combine(temp,"success-receipt"),CancellationToken.None,_=>false,_=>{});
   Check(File.ReadAllText(Path.Combine(root,"existing.dll"))=="new"&&File.ReadAllText(Path.Combine(root,"added.dll"))=="new");
   File.WriteAllText(source,"tampered");failed=false;try{CrossplaySetup.Apply(new[]{plan},Path.Combine(temp,"tampered-receipt"),CancellationToken.None,_=>false,_=>{});}catch(IOException){failed=true;}Check(failed);
  });
  Test("two SDK adapters share identical files but reject conflicting writes",()=>{
   var root=Path.Combine(temp,"shared-sdk");var source=Path.Combine(temp,"shared-api.dll");File.WriteAllText(source,"same");
   var copy=new SetupCopy(source,root,"bin/api.dll",CrossplaySetup.Hash(source));
   CrossplaySetup.Apply(new[]{new SetupPlan("hl2",new(){copy},new()),new SetupPlan("tf2",new(){copy},new())},Path.Combine(temp,"shared-sdk-backup"),CancellationToken.None,_=>false,_=>{});
   Check(File.ReadAllText(Path.Combine(root,"bin/api.dll"))=="same");
   var other=Path.Combine(temp,"other-api.dll");File.WriteAllText(other,"different");
   bool rejected=false;try{CrossplaySetup.Apply(new[]{new SetupPlan("hl2",new(){copy},new()),new SetupPlan("tf2",new(){new(other,root,"bin/api.dll",CrossplaySetup.Hash(other))},new())},Path.Combine(temp,"conflict-backup"),CancellationToken.None,_=>false,_=>{});}catch(IOException){rejected=true;}
   Check(rejected&&File.ReadAllText(Path.Combine(root,"bin/api.dll"))=="same");
  });
  Test("SDK mounts use discovered locations and reject injected paths",()=>{
   var text=SdkConfiguration.GameInfo("tf2",Path.Combine(temp,"Games TF"),Path.Combine(temp,"SDK"),new[]{Path.Combine(temp,"Local assets")});
   Check(text.Contains("SteamAppId 243750")&&text.Contains("DependsOnAppID 440")&&text.Contains("Local assets")&&!text.Contains("Program Files (x86)/Steam/steamapps/common"));
   bool rejected=false;try{SdkConfiguration.GameInfo("tf2","bad\"path","sdk",Array.Empty<string>());}catch(InvalidDataException){rejected=true;}Check(rejected);
  });
  Test("native asset mounts are idempotent and preserve game paths",()=>{
   var original="GameInfo { FileSystem { SearchPaths { Game |gameinfo_path|. } } }";
   var mounts=new[]{Path.Combine(temp,"cache")};var updated=SdkConfiguration.WithAssetMounts(original,mounts);
   Check(updated.Contains("Game |gameinfo_path|.")&&SdkConfiguration.WithAssetMounts(updated,mounts)==updated);
   Check(updated.Contains("Game_Write \"|gameinfo_path|.\"")&&updated.Contains("Default_Write_Path \"|gameinfo_path|.\""));
   bool rejected=false;try{SdkConfiguration.WithAssetMounts("broken",mounts);}catch(InvalidDataException){rejected=true;}Check(rejected);
  });
  Test("existing bridge requires fresh connected data and session identities",()=>{
   var file=Path.Combine(temp,"bridge.json");
   File.WriteAllText(file,JsonSerializer.Serialize(new{connected=true,connection=Guid.NewGuid(),coreSession=Guid.NewGuid()}));
   Check(Runtime.ExistingBridgeReady(file));File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddSeconds(-10));Check(!Runtime.ExistingBridgeReady(file));
   File.WriteAllText(file,"{\"connected\":false}");Check(!Runtime.ExistingBridgeReady(file));
   File.WriteAllText(file,"{\"connected\":true,\"connection\":\"invalid\",\"coreSession\":null}");Check(!Runtime.ExistingBridgeReady(file));
  });
  Test("download extraction rejects traversal and preserves valid files",()=>{
   var zipPath=Path.Combine(temp,"dependency.zip");
   using(var zip=System.IO.Compression.ZipFile.Open(zipPath,System.IO.Compression.ZipArchiveMode.Create)){
    using var writer=new StreamWriter(zip.CreateEntry("../escape.txt").Open());writer.Write("invalid");
   }
   bool rejected=false;try{SetupDownloads.Extract(zipPath,Path.Combine(temp,"dependency-out"));}catch(InvalidDataException){rejected=true;}
   Check(rejected&&!File.Exists(Path.Combine(temp,"escape.txt")));
   var validZip=Path.Combine(temp,"valid-dependency.zip");
   using(var zip=System.IO.Compression.ZipFile.Open(validZip,System.IO.Compression.ZipArchiveMode.Create)){
    using var writer=new StreamWriter(zip.CreateEntry("tool/data.txt").Open());writer.Write("verified fixture");
   }
   SetupDownloads.Extract(validZip,Path.Combine(temp,"dependency-out"));
   Check(File.ReadAllText(Path.Combine(temp,"dependency-out/tool/data.txt"))=="verified fixture");
  });
  Test("release setup refuses missing converted content and accepts a local cache",()=>{
   var project=Path.Combine(temp,"release-requirements");Directory.CreateDirectory(Path.Combine(project,"setup"));
   var cache=Path.Combine(temp,"release-cache");Directory.CreateDirectory(cache);
   var manifest=Path.Combine(project,"setup/local-content-requirements.json");
   File.WriteAllText(manifest,JsonSerializer.Serialize(new[]{new ReleaseRequirements.Requirement("portal2","models/peer.mdl","Converted peer")}));
   var settings=new Settings{ProjectRoot=project,LocalAssetCaches=new(){{"peers",cache}}};
   Check(ReleaseRequirements.Missing(settings,"portal2",temp).Single()=="Converted peer");
   Directory.CreateDirectory(Path.Combine(cache,"models"));File.WriteAllText(Path.Combine(cache,"models/peer.mdl"),"fixture");
   Check(!ReleaseRequirements.Missing(settings,"portal2",temp).Any());
   File.WriteAllText(manifest,JsonSerializer.Serialize(new[]{new ReleaseRequirements.Requirement("portal2","../escape","Invalid")}));
   bool rejected=false;try{ReleaseRequirements.Missing(settings,"portal2",temp).ToArray();}catch(InvalidDataException){rejected=true;}Check(rejected);
  });
  Test("installation relocates bundled runtime paths and preserves custom projects",()=>{
   var saved=File.Exists(Settings.FilePath)?File.ReadAllText(Settings.FilePath):null;
   try{
    var source=Path.Combine(temp,"install-source");var target=Path.Combine(temp,"installed/app");
    foreach(var root in new[]{source,target}){Directory.CreateDirectory(Path.Combine(root,"runtime/sourcebox"));Directory.CreateDirectory(Path.Combine(root,"runtime/sdk"));File.WriteAllText(Path.Combine(root,"runtime/sourcebox/core.mjs"),"");File.WriteAllText(Path.Combine(root,"runtime/sdk/mod_tf_win64.exe"),"");}
    new Settings{ProjectRoot=Path.Combine(source,"runtime/sourcebox"),SdkRoot=Path.Combine(source,"runtime/sdk")}.Save();
    DesktopInstall.RelocateSettings(source,target);var relocated=Settings.Load();Check(Path.GetFullPath(relocated.ProjectRoot)==Path.GetFullPath(Path.Combine(target,"runtime/sourcebox")));
    var custom=Path.Combine(temp,"custom-install-project");Directory.CreateDirectory(custom);File.WriteAllText(Path.Combine(custom,"core.mjs"),"");relocated.ProjectRoot=custom;relocated.Save();DesktopInstall.RelocateSettings(source,target);Check(Settings.Load().ProjectRoot==custom);
   }finally{if(saved==null)File.Delete(Settings.FilePath);else File.WriteAllText(Settings.FilePath,saved);}
  });
  Test("desktop package rejects traversal and altered files",()=>{
   var folder=Path.Combine(temp,"package");Directory.CreateDirectory(folder);var exe=Path.Combine(folder,"SourceBox.exe");File.WriteAllText(exe,"fixture");
   var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(exe)));
   File.WriteAllText(Path.Combine(folder,"package-manifest.json"),JsonSerializer.Serialize(new[]{new DesktopInstall.Entry("SourceBox.exe",hash)}));
   Check(DesktopInstall.Verify(folder).Length==1);
   File.WriteAllText(Path.Combine(folder,"private-settings.json"),"must not be exported");var export=DesktopInstall.ExportZip(folder,Path.Combine(temp,"export.zip"));
   using(var zip=System.IO.Compression.ZipFile.OpenRead(export)){Check(zip.Entries.Count==2&&!zip.Entries.Any(e=>e.Name=="private-settings.json"));}
   File.AppendAllText(exe,"changed");bool rejected=false;try{DesktopInstall.Verify(folder);}catch(InvalidDataException){rejected=true;}Check(rejected);
   rejected=false;try{DesktopInstall.Resolve(folder,"../outside");}catch(InvalidDataException){rejected=true;}Check(rejected);
  });
  Test("content providers are independent of launch readiness and missing games select topology",()=>{
   var root=Path.Combine(temp,"content-provider");Directory.CreateDirectory(Path.Combine(root,"tf"));
   var def=Discovery.Defs.Single(g=>g.Id=="tf2");var games=new[]{new GameInstall(def,root,"",false,false,"No SDK adapter")};
   var catalog=new DestinationCatalog(new(){new("ctf_2fort","2Fort","TF2","",false)},"",true);
   var profile=SharedContent.Scan(temp,games,catalog);Check(profile.Sources[0].Available&&profile.Worlds[0].Mode=="installed-assets");
   var missing=SharedContent.Scan(temp,Array.Empty<GameInstall>(),catalog);Check(missing.Worlds[0].Mode=="topology-placeholder"&&!missing.Worlds[0].TopologyCached);
   Directory.CreateDirectory(Path.Combine(temp,"worlds"));File.WriteAllText(Path.Combine(temp,"worlds/ctf_2fort.json"),"{}");
   Check(SharedContent.Scan(temp,Array.Empty<GameInstall>(),catalog).Worlds[0].TopologyCached);
  });
  Test("destination registry follows manifest and rejects traversal and duplicate IDs",()=>{
   var root=Path.Combine(temp,"world-project");var sheets=Path.Combine(root,"sheets");Directory.CreateDirectory(sheets);
   var manifest=Path.Combine(sheets,"connectivity.json");var sheet=Path.Combine(sheets,"destinations.json");
   File.WriteAllText(manifest,"{\"sheets\":{\"destinationWorlds\":\"destinations.json\"}}");
   File.WriteAllText(sheet,"{\"enabled\":true,\"nativeMap\":\"room\",\"worlds\":[{\"id\":\"test\",\"label\":\"Test\",\"source\":\"missing.bsp\"}]}");
   var catalog=Destinations.Read(root);Check(catalog.Enabled&&catalog.Items.Count==2&&!catalog.Items[1].SourcePresent);
   File.WriteAllText(sheet,File.ReadAllText(sheet).Replace("\"test\"","\"room\""));Check(Destinations.Read(root).Error.Length>0);
   File.WriteAllText(manifest,"{\"sheets\":{\"destinationWorlds\":\"../escape.json\"}}");Check(Destinations.Read(root).Items.Count==0);
   File.WriteAllText(manifest,"broken");Check(Destinations.Read(root).Error.Length>0);
  });
  Test("no installed games reports missing",()=>{var s=new Settings{SteamRoot=temp,SdkRoot=Path.Combine(temp,"sdk")};Check(Discovery.Scan(s).All(g=>!g.Installed&&!g.Ready));});
  Test("alternate Steam library detected; adapter distinct from install",()=>{
   var alternate=Path.Combine(temp,"alternate");Directory.CreateDirectory(Path.Combine(temp,"steamapps"));Directory.CreateDirectory(Path.Combine(alternate,"steamapps/common/GarrysMod"));File.WriteAllText(Path.Combine(temp,"steamapps/libraryfolders.vdf"),"\"libraryfolders\" { \"1\" { \"path\" \""+alternate.Replace("\\","\\\\")+"\" } }");File.WriteAllText(Path.Combine(alternate,"steamapps/appmanifest_4000.acf"),"\"AppState\" { \"installdir\" \"GarrysMod\" }");File.WriteAllText(Path.Combine(alternate,"steamapps/common/GarrysMod/gmod.exe"),"");var g=Discovery.Scan(new(){SteamRoot=temp,SdkRoot=Path.Combine(temp,"sdk")}).Single(g=>g.Def.Id=="gmod");Check(g.Installed&&!g.Ready);
  });
  Test("manual executable and malformed manifest traversal",()=>{
   var manual=Path.Combine(temp,"portal2.exe");File.WriteAllText(manual,"");var s=new Settings{SteamRoot=temp,Executables=new(){{"portal2",manual}}};Check(Discovery.Scan(s).Single(g=>g.Def.Id=="portal2").Installed);s.Executables["portal2"]=Path.Combine(temp,"wrong.exe");File.WriteAllText(s.Executables["portal2"],"");Check(!Discovery.Scan(s).Single(g=>g.Def.Id=="portal2").Ready);
  });
  Test("Steam manifest traversal cannot escape common directory",()=>{
   var lib=Path.Combine(temp,"malicious");Directory.CreateDirectory(Path.Combine(lib,"steamapps"));File.WriteAllText(Path.Combine(lib,"steamapps/appmanifest_620.acf"),"\"installdir\" \"../../../escape\"");Check(!Discovery.Scan(new(){SteamRoot=lib}).Single(g=>g.Def.Id=="portal2").Installed);
  });
  Test("ownership receipt serialization retains identity",()=>{var p=new OwnedProcess{Component="test",Exe="C:\\test.exe",Pid=42,StartTicks=123};var copy=JsonSerializer.Deserialize<OwnedProcess>(JsonSerializer.Serialize(p))!;Check(copy.Pid==42&&copy.StartTicks==123&&copy.Exe==p.Exe);});
  Test("fresh telemetry, stale telemetry, disconnect and malformed lines",()=>{
   var log=Path.Combine(temp,"core.jsonl");var now=DateTime.UtcNow;File.WriteAllText(log,"bad json\n"+JsonSerializer.Serialize(new{type="state",game="hl2",time=now,world="gm_construct",weapon="weapon_physcannon"})+"\n");Check(Telemetry.Read(log,now)["hl2"].State=="Connected");Check(Telemetry.Read(log,now.AddSeconds(6))["hl2"].State.StartsWith("Stale"));File.AppendAllText(log,JsonSerializer.Serialize(new{type="disconnected",game="hl2",time=now})+"\n");Check(Telemetry.Read(log,now)["hl2"].State=="Disconnected");File.AppendAllText(log,"{\"game\":42,\"time\":false}\n");Check(Telemetry.Read(log,now)["hl2"].State=="Disconnected");
  });
  Test("configuration validation and redaction",()=>{bool rejected=false;try{new Settings{ProjectRoot="relative",FpsLimit=60}.Validate();}catch{rejected=true;}Check(rejected);var s=new Settings{ProjectRoot=temp};var text=Telemetry.Redact(temp+" token=abc secret=xyz",s);Check(!text.Contains(temp)&&!text.Contains("abc")&&!text.Contains("xyz"));});
  Test("ownership rejects PID reuse / mismatch; stops exact owned fixture",()=>{
   var exe=Environment.ProcessPath!;var pi=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};if(Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))pi.ArgumentList.Add(typeof(Tests).Assembly.Location);pi.ArgumentList.Add("--fixture");using var p=Process.Start(pi)!;
   Check(Runtime.ProcessAt(exe.Replace('\\','/')));Check(ProcessTree.Parent(p.Id)==Environment.ProcessId);
   try{var r=new OwnedProcess{Component="fixture",Pid=p.Id,Exe=exe,StartTicks=p.StartTime.ToUniversalTime().Ticks+1};Check(!r.Matches());r.Stop();Check(!p.HasExited);r.StartTicks--;Check(r.Matches());r.Stop();Check(p.HasExited);}finally{if(!p.HasExited)p.Kill();}
  });
  Test("real coordinator starts via detached host on an isolated ephemeral port",()=>{
   var config=Settings.Load();if(!File.Exists(config.NodePath)||!File.Exists(Path.Combine(config.ProjectRoot,"core.mjs")))throw new Exception("Existing coordinator prerequisite unavailable");
   var script=Path.Combine(temp,"core-fixture.mjs");
   File.WriteAllText(script,"import {createCore} from "+JsonSerializer.Serialize(new Uri(Path.Combine(config.ProjectRoot,"core.mjs")).AbsoluteUri)+";const c=createCore();c.server.listen(0,'127.0.0.1',()=>console.log(JSON.stringify({boundPort:c.server.address().port})));process.on('SIGTERM',()=>c.close());");
   var log=Path.Combine(temp,"core-output.log");var owned=ServiceHost.Start(new(config.NodePath,new[]{script},config.ProjectRoot,"core-fixture",log,Path.Combine(temp,"receipt.json")),Path.Combine(temp,"service.json"));
   try{for(int i=0;i<60&&(!File.Exists(log)||!File.ReadAllText(log).Contains("boundPort"));i++)Thread.Sleep(100);using var doc=JsonDocument.Parse(File.ReadAllLines(log).First(x=>x.Contains("boundPort")));int port=doc.RootElement.GetProperty("boundPort").GetInt32();Check(Runtime.PortListening(port));Check(owned.Matches());owned.Stop();Check(!Runtime.PortListening(port));}finally{owned.Stop();}
  });
  Directory.CreateDirectory(Settings.Home);File.WriteAllLines(Path.Combine(Settings.Home,"self-tests.txt"),lines.Append($"{lines.Count-failures}/{lines.Count} passed"));return failures==0?0:1;
 }
}




