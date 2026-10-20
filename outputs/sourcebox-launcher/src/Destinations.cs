using System.Text.Json;
using System.Text.RegularExpressions;
namespace SourceBox;

public record Destination(string Id,string Title,string Origin,string Source,bool SourcePresent);
public record DestinationCatalog(List<Destination> Items,string Error,bool Enabled);
public static class Destinations {
 public static DestinationCatalog Read(string project){
  try{
   var folder=Path.Combine(project,"sheets");
   using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"connectivity.json")));
   var name=manifest.RootElement.GetProperty("sheets").GetProperty("destinationWorlds").GetString()??"";
   if(!Regex.IsMatch(name,@"\A[a-z0-9-]+\.json\z"))throw new InvalidDataException("Invalid destination sheet path.");
   using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,name)));
   var root=doc.RootElement;var enabled=root.GetProperty("enabled").GetBoolean();
   var hub=root.GetProperty("nativeMap").GetString()??"";
   var seen=new HashSet<string>();var items=new List<Destination>();
   void Add(string id,string title,string source,string origin){
    if(!Regex.IsMatch(id,@"\A[a-z0-9_]+\z")||!seen.Add(id)||string.IsNullOrWhiteSpace(title))throw new InvalidDataException("Invalid or duplicate destination.");
    items.Add(new(id,title,origin,source,source.Length>0&&File.Exists(source)));
   }
   Add(hub,"Shared Room","","SourceBox");
   foreach(var world in root.GetProperty("worlds").EnumerateArray()){
    var source=world.GetProperty("source").GetString()??"";
    var normalized=source.Replace('\\','/');var parts=normalized.Split('/',StringSplitOptions.RemoveEmptyEntries);var common=Array.FindIndex(parts,p=>p.Equals("common",StringComparison.OrdinalIgnoreCase));
    var origin=common>=0&&common+1<parts.Length?parts[common+1]:"Custom";
    Add(world.GetProperty("id").GetString()??"",world.GetProperty("label").GetString()??"",source,origin);
   }
   return new(items,"",enabled);
  }catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException){return new(new(),"Destination list unavailable: "+e.Message,false);}
 }
}

