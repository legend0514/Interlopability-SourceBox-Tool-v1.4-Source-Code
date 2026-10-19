using CounterStrikeSharp.API;
public partial class SourceboxCS2 {
 private readonly Dictionary<string,bool> localContent=new(StringComparer.OrdinalIgnoreCase);
 private bool HasLocalModel(string model){
  if(!model.StartsWith("models/sourcebox/",StringComparison.Ordinal))return true;
  if(localContent.TryGetValue(model,out var present))return present;
  var content=System.IO.Path.GetFullPath(System.IO.Path.Combine(ModuleDirectory,"../../../.."));
  present=System.IO.File.Exists(System.IO.Path.Combine(content,model+"_c"));localContent[model]=present;
  if(!present)Console.WriteLine("[Sourcebox] Local model conversion not installed: "+model);
  return present;
 }
}
