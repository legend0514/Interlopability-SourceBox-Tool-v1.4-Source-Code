using System.Text.Json;
namespace SourceBox;
public static class CsContentPreparation {
 public sealed record Job(string source,string target,string? weaponBone=null);
 public static readonly Job[] Players={
  new("models/sourcebox/hl2/player.mdl","models/sourcebox/hl2/player.vmdl","bip01_r_hand"),
  new("models/sourcebox/portal2/peers/heavy.mdl","models/sourcebox/tf2/heavy/heavy.vmdl","weapon_bone"),
  new("models/sourcebox/portal2/peers/gmod.mdl","models/sourcebox/gmod/kleiner/kleiner.vmdl","bip01_r_hand"),
  new("models/player/mp/gordon/gordon.mdl","models/sourcebox/hl1mp/gordon.vmdl","bip01_r_hand"),
  new("models/sourcebox/portal2/chell.mdl","models/sourcebox/portal2/chell/chell.vmdl","wrist_R"),
  new("models/sourcebox/portal2/w_portalgun.mdl","models/sourcebox/portals/world/world.vmdl"),
  new("models/sourcebox/portal2/v_portalgun.mdl","models/sourcebox/portals/view/view.vmdl")};
 public static async Task Prepare(Settings settings,Dictionary<string,GameInstall> games,CancellationToken ct,Action<string> log,bool rebuild=false){
  if(!games.TryGetValue("cs2",out var cs))return;
  var script=Path.Combine(settings.ProjectRoot,"tools/prepare-portable-cs2-models.cjs");if(!File.Exists(script))return;
  foreach(var sheet in new[]{"asset-references.json","cs2-scene-assets.json"})if(!File.Exists(Path.Combine(settings.ProjectRoot,"sheets",sheet)))throw new IOException("The runtime package is missing "+sheet+". Reinstall the complete SourceBox package.");
  var gameContent=Path.Combine(cs.Root,"game/csgo");
  if(!rebuild&&File.Exists(Path.Combine(gameContent,"maps/sourcebox_room_001.vpk"))&&Players.All(j=>File.Exists(Path.Combine(gameContent,j.target+"_c")))&&new[]{"ring","monitor"}.All(n=>File.Exists(Path.Combine(gameContent,$"models/sourcebox/portals/{n}/{n}.vmdl_c"))))return;
  var hl2Root=games.TryGetValue("hl2",out var hl2)?hl2.Root:SdkConfiguration.FindRuntime(settings);
  if(!Directory.Exists(Path.Combine(hl2Root,"hl2")))throw new IOException("CS2 room conversion needs Source SDK Base 2013 Multiplayer (free Steam tools, app 243750) when Half-Life 2 is not installed.");
  foreach(var tool in new[]{"resourcecompiler.exe","source1import.exe","dmxconvert.exe","cs_mdl_import.exe"})if(!File.Exists(Path.Combine(cs.Root,"game/bin/win64",tool)))throw new IOException("Install Counter-Strike 2 Workshop Tools in Steam, then retry crossplay setup.");
  var scripts=new[]{script,Path.Combine(settings.ProjectRoot,"tools/prepare-portable-cs2-textures.py"),Path.Combine(settings.ProjectRoot,"tools/prepare-portable-cs2-portal-surfaces.py"),Path.Combine(settings.ProjectRoot,"tools/prepare-portable-cs2-room.ps1"),Path.Combine(settings.ProjectRoot,"tools/prepare-portable-room-materials.cjs")};
  var fingerprint=string.Join("",scripts.Select(CrossplaySetup.Hash))+string.Join("",settings.LocalAssetCaches.OrderBy(p=>p.Key).Where(p=>p.Key!="cs2").Select(p=>p.Value));
  if(settings.LocalAssetCaches.TryGetValue("cs2",out var previous)&&SharedAssetPreparation.Valid(previous,fingerprint))return;
  var stage=Path.Combine(Settings.Home,"assets","cs2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  var room=Path.Combine(stage,"room");var models=Path.Combine(stage,"converted");
  var roomRequest=Path.Combine(stage,"room-request.json");
  File.WriteAllText(roomRequest,JsonSerializer.Serialize(new{csRoot=cs.Root,hl2Root,outputRoot=room,nodePath=settings.NodePath}));
  log("Building the CS2 Shared Room with the installed Workshop Tools…");
  await SharedAssetPreparation.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe"),new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",scripts[3],"-Request",roomRequest},Path.Combine(stage,"room.log"),ct);
  var jobs=Players.ToList();
  using var references=JsonDocument.Parse(File.ReadAllText(Path.Combine(settings.ProjectRoot,"sheets/asset-references.json")));
  using var scene=JsonDocument.Parse(File.ReadAllText(Path.Combine(settings.ProjectRoot,"sheets/cs2-scene-assets.json")));
  var common=new[]{"models/props_junk/wood_crate001a.mdl","models/props_c17/oildrum001.mdl","models/zombie/classic.mdl"}.Concat(references.RootElement.GetProperty("hl2Weapons").EnumerateObject().Select(p=>p.Value.GetString()!)).Distinct();
  foreach(var model in common)if(!jobs.Any(j=>j.source==model)&&scene.RootElement.TryGetProperty(model,out var target))jobs.Add(new(model,target.GetString()!));
  var request=Path.Combine(stage,"models-request.json");
  File.WriteAllText(request,JsonSerializer.Serialize(new{csRoot=cs.Root,outputRoot=models,providers=settings.LocalAssetCaches.Where(p=>p.Key!="cs2").Select(p=>p.Value).ToArray(),archives=new[]{Path.Combine(hl2Root,"hl2/hl2_misc_dir.vpk"),Path.Combine(hl2Root,"hl2/hl2_textures_dir.vpk")},jobs,blender=await SetupDownloads.Blender(ct,log),sourceioRoot=await SetupDownloads.SourceIo(ct,log),portalSurfaces=true}));
  log("Converting CS2 players, weapons and shared-room physics objects…");
  await SharedAssetPreparation.Run(settings.NodePath,new[]{script,request},Path.Combine(stage,"models.log"),ct);
  foreach(var source in new[]{room,models})foreach(var folder in new[]{"maps","models","materials","lighting"}){
   var input=Path.Combine(source,folder);if(!Directory.Exists(input))continue;
   foreach(var file in Directory.GetFiles(input,"*",SearchOption.AllDirectories)){var output=DesktopInstall.Resolve(stage,Path.Combine(folder,Path.GetRelativePath(input,file)));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output,true);}
  }
  SharedAssetPreparation.Receipt(stage,fingerprint);settings.LocalAssetCaches["cs2"]=stage;settings.Save();log("CS2 room and core crossplay content prepared.");
 }
}
