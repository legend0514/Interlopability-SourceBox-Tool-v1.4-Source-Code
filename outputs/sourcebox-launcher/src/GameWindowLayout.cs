using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SourceBox;
public sealed class GameWindowLayout {
 public Rectangle Monitor=Screen.PrimaryScreen?.Bounds??new Rectangle(0,0,1920,1080);
 public HashSet<string> Selected=new();
 readonly Dictionary<IntPtr,Rectangle> applied=new();
 readonly Dictionary<IntPtr,Size> nativeSizes=new();
 public static Rectangle Fit(Rectangle tile,Size native){
  if(native.Width<=0||native.Height<=0)return tile;
  double scale=Math.Min((double)tile.Width/native.Width,(double)tile.Height/native.Height);
  int w=Math.Max(1,(int)Math.Floor(native.Width*scale)),h=Math.Max(1,(int)Math.Floor(native.Height*scale));
  return new Rectangle(tile.X+(tile.Width-w)/2,tile.Y+(tile.Height-h)/2,w,h);
 }
 public static Dictionary<string,Rectangle> Slots(Rectangle monitor,IEnumerable<string> ids){
  var ordered=ids.Distinct().ToArray();var tiles=Tiles(monitor,ordered.Length);
  return ordered.Select((id,i)=>(id,tile:tiles[i])).ToDictionary(x=>x.id,x=>x.tile);
 }
 public static Rectangle[] Tiles(Rectangle screen,int count){
  if(count<1)return Array.Empty<Rectangle>();
  int columns=count<=2?count:(int)Math.Ceiling(Math.Sqrt(count));
  int rows=(count+columns-1)/columns,done=0;var result=new List<Rectangle>();
  for(int row=0;row<rows;row++){
   int n=Math.Min(columns,count-done),top=screen.Top+screen.Height*done/count,bottom=screen.Top+screen.Height*(done+n)/count;
   for(int col=0;col<n;col++){
    int left=screen.Left+screen.Width*col/n,right=screen.Left+screen.Width*(col+1)/n;
    result.Add(Rectangle.FromLTRB(left,top,right,bottom));
   }
   done+=n;
  }
  return result.ToArray();
 }
 List<(string Id,IntPtr Handle)> GameWindows(IEnumerable<GameInstall> games){
  var result=new List<(string Id,IntPtr Handle)>();
  foreach(var g in games.Where(g=>Selected.Contains(g.Def.Id))){
   var paths=new[]{g.Exe,Path.Combine(g.Root,"bin/win64/gmod.exe")}.Take(g.Def.Id=="gmod"?2:1).Select(Path.GetFullPath).ToArray();
   foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(g.Exe)))using(p)try{
    if(p.MainModule==null||!paths.Contains(Path.GetFullPath(p.MainModule.FileName),StringComparer.OrdinalIgnoreCase))continue;
    var h=p.MainWindowHandle;if(h!=IntPtr.Zero&&IsWindowVisible(h))result.Add((g.Def.Id,h));
   }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
  }
  return result.Distinct().ToList();
 }
 public List<IntPtr> Windows(IEnumerable<GameInstall> games)=>GameWindows(games).Select(w=>w.Handle).Distinct().ToList();
 public void Apply(IEnumerable<GameInstall> games){
  if(Selected.Count==0)return;
  var ordered=games.Where(g=>Selected.Contains(g.Def.Id)).ToArray();
  var slots=Slots(Monitor,ordered.Select(g=>g.Def.Id));var windows=GameWindows(ordered);
  var handles=windows.Select(w=>w.Handle).ToArray();
  foreach(var window in windows){
   var h=window.Handle;
   // Reserve every selected game's slot even while it loads or reconnects.
   // Preserve the native client aspect if an engine clamps the requested mode.
   if(!nativeSizes.TryGetValue(h,out var native)){
    if(!GetClientRect(h,out var client)||client.Right<320||client.Bottom<200)continue;
    native=new Size(client.Right,client.Bottom);nativeSizes[h]=native;
   }
   var r=Fit(slots[window.Id],native);
   if(applied.TryGetValue(h,out var previous)&&previous==r&&GetWindowRect(h,out var current)&&current.Left==r.Left&&current.Top==r.Top&&current.Right==r.Right&&current.Bottom==r.Bottom)continue;
   // Games are launched windowed. Remove standard non-client borders only.
   var style=GetWindowLongW(h,-16);SetWindowLongW(h,-16,style&~0x00C40000);
   if(SetWindowPos(h,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x0010|0x0004|0x0020|0x4000))applied[h]=r;
  }
  foreach(var h in applied.Keys.Except(handles).ToArray()){applied.Remove(h);nativeSizes.Remove(h);}
 }
 public static void ReleaseTo(IntPtr window){ShowWindow(window,9);SetForegroundWindow(window);ClipCursor(IntPtr.Zero);}
 public void Next(IEnumerable<GameInstall> games){var handles=Windows(games);if(handles.Count==0)return;int index=handles.IndexOf(GetForegroundWindow());var next=handles[(index+1)%handles.Count];ReleaseTo(next);}
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
 [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr h,out NativeRect rect);
 [DllImport("user32.dll")]static extern int GetWindowLongW(IntPtr h,int index);
 [DllImport("user32.dll")]static extern int SetWindowLongW(IntPtr h,int index,int value);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]static extern bool ClipCursor(IntPtr rectangle);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id);
}
