using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SourceBox;
public record BootstrapRequest(string Exe,string Root,string SessionDir);
public record BootstrapResult(bool Success,string Error,OwnedProcess? Process);
public static class CsBootstrap {
 public static async Task Run(string requestPath){
  var r=JsonSerializer.Deserialize<BootstrapRequest>(File.ReadAllText(requestPath))!;
  string file=Path.Combine(r.Root,"game/csgo/gameinfo.gi");byte[]? original=null;OwnedProcess? receipt=null;bool success=false;string error="";
  try{
   if(Path.GetFullPath(r.Exe)!=Path.GetFullPath(Path.Combine(r.Root,"game/bin/win64/cs2.exe")))throw new Exception("CS2 executable does not match discovered installation.");
   if(Runtime.ProcessAt(r.Exe)||Runtime.UdpBusy(27019))throw new Exception("CS2 is already running or its server port is occupied.");
   original=File.ReadAllBytes(file);string text=System.Text.Encoding.UTF8.GetString(original);
   if(text.Contains("csgo/addons/metamod",StringComparison.OrdinalIgnoreCase))throw new Exception("Existing Metamod gameinfo entry requires review; file left untouched.");
   var changed=Regex.Replace(text,@"(?m)^(\s*Game_LowViolence\s+csgo_lv.*)$","$1\r\n\t\t\tGame csgo/addons/metamod");
   if(changed==text)throw new Exception("CS2 gameinfo format changed; file left untouched.");
   File.WriteAllBytes(Path.Combine(r.SessionDir,"cs2-gameinfo.backup"),original);
   var pi=new ProcessStartInfo(r.Exe){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.Combine(r.Root,"game"),RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(var a in new[]{"-dedicated","-insecure","-condebug","-ip","127.0.0.1","-port","27019","+sv_lan","1","+mp_autokick","0","+bot_quota","0","+game_type","0","+game_mode","0","+map","sourcebox_room_001"})pi.ArgumentList.Add(a);
   File.WriteAllText(file,changed);
   using var p=Process.Start(pi)!;receipt=new(){Component="cs2-server",Exe=r.Exe,Pid=p.Id,StartTicks=p.StartTime.ToUniversalTime().Ticks};
   var log=Path.Combine(r.SessionDir,"cs2-server.log");var sync=new object();var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   void Output(string? line){if(line==null)return;lock(sync)File.AppendAllText(log,line+"\n");if(line.Contains("[Sourcebox] Core connected"))ready.TrySetResult();}
   p.OutputDataReceived+=(_,e)=>Output(e.Data);p.ErrorDataReceived+=(_,e)=>Output(e.Data);p.BeginOutputReadLine();p.BeginErrorReadLine();
   var exited=p.WaitForExitAsync();
   var first=await Task.WhenAny(ready.Task,exited,Task.Delay(TimeSpan.FromSeconds(60)));
   if(first==exited||p.HasExited){
    p.WaitForExit();
    throw new IOException("CS2's local server exited before it was ready (exit code "+p.ExitCode+"). See cs2-server.log in the session diagnostics.");
   }
   if(first!=ready.Task)throw new TimeoutException("CS2's local server did not connect within 60 seconds. See cs2-server.log in the session diagnostics.");
   success=true;
   // Keep output draining after restore/result; closing pipes would harm logging.
   File.WriteAllBytes(file,original);original=null;
   File.WriteAllText(Path.Combine(r.SessionDir,"cs2-bootstrap-result.json.tmp"),JsonSerializer.Serialize(new BootstrapResult(true,"",receipt)));
   File.Move(Path.Combine(r.SessionDir,"cs2-bootstrap-result.json.tmp"),Path.Combine(r.SessionDir,"cs2-bootstrap-result.json"),true);
   await p.WaitForExitAsync();return;
  }catch(Exception e){error=e.Message;try{receipt?.Stop();}catch{}}
  finally{if(original!=null)try{File.WriteAllBytes(file,original);}catch(Exception e){success=false;error+=" Restore failed: "+e.Message+". Backup is in session logs; do not launch normal CS2 until restored.";}}
  var result=Path.Combine(r.SessionDir,"cs2-bootstrap-result.json");File.WriteAllText(result+".tmp",JsonSerializer.Serialize(new BootstrapResult(success,error,receipt)));File.Move(result+".tmp",result,true);
 }
}
