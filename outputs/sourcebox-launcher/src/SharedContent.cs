using System.Text.Json;
namespace SourceBox;

public record ContentSource(string Game,string Title,string Root,bool Available,string Engine);
public record ContentWorld(string Id,string Owner,string Mode,bool TopologyCached);
public record ContentProfile(int Version,ContentSource[] Sources,ContentWorld[] Worlds);
public static class SharedContent {
 public static string Owner(string id)=>id switch {
  "gm_construct" or "gm_flatgrass"=>"gmod", "ctf_2fort"=>"tf2",
  "portal_triple_laser"=>"portal2", "hl1_frenzy"=>"hl1mp", _=>""
 };
 public static ContentProfile Scan(string project,IEnumerable<GameInstall> games,DestinationCatalog catalog){
  // A content provider does not need its SDK adapter or running game process.
  var sources=games.Select(g=>new ContentSource(g.Def.Id,g.Def.Title,g.Root,
   !string.IsNullOrEmpty(g.Root)&&Directory.Exists(g.Root)&&Directory.Exists(Path.Combine(g.Root,g.Def.Id switch {"hl2"=>"hl2","tf2"=>"tf",_=>g.Def.Content})),g.Def.Engine)).ToArray();
  var worlds=catalog.Items.Select(d=>{
   var owner=Owner(d.Id);bool installed=owner==""||sources.Any(s=>s.Game==owner&&s.Available);
   var cache=Path.Combine(project,"worlds",d.Id+".json");
   return new ContentWorld(d.Id,owner,installed?"installed-assets":"topology-placeholder",owner==""||File.Exists(cache));
  }).ToArray();
  return new(1,sources,worlds);
 }
 public static void Save(ContentProfile profile,string file){
  Directory.CreateDirectory(Path.GetDirectoryName(file)!);
  var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(profile,new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));File.Move(temp,file,true);
 }
}
