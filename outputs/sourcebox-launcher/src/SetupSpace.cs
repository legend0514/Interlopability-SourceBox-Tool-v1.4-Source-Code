namespace SourceBox;
public static class SetupSpace {
 internal static string Volume(string path)=>Path.GetPathRoot(Path.GetFullPath(path))??throw new IOException("Cannot locate setup drive.");
 internal static Dictionary<string,long> Required(IEnumerable<SetupCopy> copies,string backupRoot){
  var totals=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
  void Add(string path,long bytes){var drive=Volume(path);totals[drive]=checked(totals.GetValueOrDefault(drive)+bytes);}
  foreach(var copy in copies){
   var target=CrossplaySetup.Destination(copy.Root,copy.Relative);
   Add(target,new FileInfo(copy.Source).Length);
   if(File.Exists(target))Add(backupRoot,new FileInfo(target).Length);
  }
  return totals;
 }
 public static void Require(string path,long bytes,string operation)=>Check(new Dictionary<string,long>{{Volume(path),bytes}},operation);
 internal static Dictionary<string,long> Combined(params (string Path,long Bytes)[] allocations){
  var totals=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
  foreach(var allocation in allocations){var volume=Volume(allocation.Path);totals[volume]=checked(totals.GetValueOrDefault(volume)+allocation.Bytes);}
  return totals;
 }
 internal static void Check(IReadOnlyDictionary<string,long> requirements,string operation,Func<string,long>? free=null){
  free??=root=>new DriveInfo(root).AvailableFreeSpace;
  foreach(var entry in requirements){
   var available=free(entry.Key);var needed=checked(entry.Value+64L*1024*1024);
   if(available<needed)throw new IOException($"Not enough free disk space on {entry.Key} for {operation}. Need at least {needed/1048576.0:N0} MB free; available {available/1048576.0:N0} MB. Free space on this drive, then retry setup. Existing game files have not been changed by this step.");
  }
 }
}
