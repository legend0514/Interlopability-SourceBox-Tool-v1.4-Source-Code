using System.Globalization;
using System.Text.Json;
namespace SourceBox;

// UI translation only. Never translate protocol values, paths or diagnostic logs.
public static class Localization {
 public sealed record Language(string Id,string Name);
 public static readonly Language[] Languages={new("auto","System / Auto"),new("en","English"),new("es","Español"),new("fr","Français"),new("de","Deutsch"),new("pt","Português")};
 static readonly Dictionary<string,Dictionary<string,string>> Catalog=Load();
 static string current="en";
 static Dictionary<string,Dictionary<string,string>> Load(){
  using var stream=typeof(Localization).Assembly.GetManifestResourceStream("SourceBox.Translations.json")!;
  return JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,string>>>(stream)!;
 }
 public static void Set(string? language){
  var candidate=language is null or "auto"?CultureInfo.CurrentUICulture.TwoLetterISOLanguageName:language;
  current=Languages.Any(l=>l.Id==candidate)&&candidate!="auto"?candidate:"en";
 }
 public static string T(string english)=>Catalog.TryGetValue(english,out var values)&&values.TryGetValue(current,out var translated)?translated:english;
 public static string Format(string english,params object[] values)=>string.Format(CultureInfo.CurrentCulture,T(english),values);
 public static void Validate(){
  foreach(var entry in Catalog)foreach(var language in Languages.Where(l=>l.Id is not ("auto" or "en"))){
   if(!entry.Value.TryGetValue(language.Id,out var value)||string.IsNullOrWhiteSpace(value))throw new InvalidDataException("Missing translation: "+entry.Key+" / "+language.Id);
   var source=System.Text.RegularExpressions.Regex.Matches(entry.Key,@"\{\d+\}").Select(m=>m.Value).Order().ToArray();
   var target=System.Text.RegularExpressions.Regex.Matches(value,@"\{\d+\}").Select(m=>m.Value).Order().ToArray();
   if(!source.SequenceEqual(target))throw new InvalidDataException("Translation placeholders differ: "+entry.Key);
  }
  var before=current;
  try{
   foreach(var language in Languages.Where(l=>l.Id is not ("auto" or "en"))){
    Set(language.Id);
    if(T("SETTINGS")=="SETTINGS"||!Format("{0} clients connected",6).Contains("6"))throw new InvalidDataException("Language switching failed");
    var settings=new Settings{Language=language.Id,SelectedGames=new[]{"hl2","portal2"}};
    var loaded=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
    if(loaded.Language!=language.Id||!loaded.SelectedGames.SequenceEqual(settings.SelectedGames))throw new InvalidDataException("Language setting did not round trip");
   }
   Set("unsupported");if(T("SETTINGS")!="SETTINGS"||T("models/player.mdl")!="models/player.mdl")throw new InvalidDataException("English fallback failed");
  }finally{current=before;}
 }
}
