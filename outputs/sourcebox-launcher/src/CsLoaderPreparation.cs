namespace SourceBox;
public static class CsLoaderPreparation {
 public static async Task Prepare(Settings settings,IEnumerable<GameInstall> games,CancellationToken ct,Action<string> log){
  var game=games.FirstOrDefault(g=>g.Def.Id=="cs2"&&g.Installed);if(game==null)return;
  if(!File.Exists(Path.Combine(game.Root,"game/csgo/addons/metamod.vdf")))
   settings.DependencyRoots["metamod"]=await SetupDownloads.Dependency("metamod-1.12.0.1227","https://github.com/alliedmodders/metamod-source/releases/download/1.12.0.1227/mmsource-1.12.0-git1227-windows.zip","01a684249ad093ddee1c144fe7cdd25a7bf004466122cd32fdc722bf6f7d332a",ct,log);
  if(!File.Exists(Path.Combine(game.Root,"game/csgo/addons/counterstrikesharp/api/CounterStrikeSharp.API.dll")))
   settings.DependencyRoots["counterstrikesharp"]=await SetupDownloads.Dependency("counterstrikesharp-1.0.376","https://github.com/roflmuffin/CounterStrikeSharp/releases/download/v1.0.376/counterstrikesharp-with-runtime-windows-1.0.376.zip","4f2777545a0940351450440288e7487509d539f770e66cf1c30cc2419084c5f8",ct,log);
  settings.Save();
 }
}
