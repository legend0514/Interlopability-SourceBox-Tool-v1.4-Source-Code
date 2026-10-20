using System.Text.Json;
namespace SourceBox;

public static class ReleaseRequirements {
 public sealed record Requirement(string Game,string Path,string Description);
 public static IEnumerable<string> Missing(Settings settings,string game,string content){
  var file=System.IO.Path.Combine(settings.ProjectRoot,"setup/local-content-requirements.json");
  if(!File.Exists(file))yield break;
  var requirements=JsonSerializer.Deserialize<Requirement[]>(File.ReadAllText(file))??throw new InvalidDataException("Missing content requirements.");
  foreach(var requirement in requirements.Where(r=>r.Game==game)){
   var roots=settings.LocalAssetCaches.Values.Append(content);
   if(!roots.Any(root=>File.Exists(DesktopInstall.Resolve(root,requirement.Path))))yield return requirement.Description;
  }
 }
}
