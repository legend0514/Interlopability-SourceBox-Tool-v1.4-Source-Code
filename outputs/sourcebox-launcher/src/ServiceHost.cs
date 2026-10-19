using System.Diagnostics;
using System.Text.Json;
namespace SourceBox;
public record ServiceRequest(string Exe,string[] Args,string Cwd,string Component,string Log,string Receipt);
public static class ServiceHost {
 public static async Task Run(string path){
  var r=JsonSerializer.Deserialize<ServiceRequest>(File.ReadAllText(path))!;
  try{
   var info=new ProcessStartInfo(r.Exe){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=r.Cwd,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in r.Args)info.ArgumentList.Add(a);
   using var p=Process.Start(info)!;var receipt=new OwnedProcess{Component=r.Component,Exe=Path.GetFullPath(r.Exe),Pid=p.Id,StartTicks=p.StartTime.ToUniversalTime().Ticks};var sync=new object();
   void Log(string? line){if(line!=null)lock(sync)File.AppendAllText(r.Log,line+"\n");}
   p.OutputDataReceived+=(_,e)=>Log(e.Data);p.ErrorDataReceived+=(_,e)=>Log(e.Data);p.BeginOutputReadLine();p.BeginErrorReadLine();
   File.WriteAllText(r.Receipt+".tmp",JsonSerializer.Serialize(receipt));File.Move(r.Receipt+".tmp",r.Receipt,true);
   await p.WaitForExitAsync();
  }catch(Exception e){File.WriteAllText(r.Receipt+".error",e.Message);}
 }
 public static OwnedProcess Start(ServiceRequest request,string file){
  File.WriteAllText(file,JsonSerializer.Serialize(request));var exe=Environment.ProcessPath!;
  var pi=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};
  if(Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))pi.ArgumentList.Add(typeof(ServiceHost).Assembly.Location);
  pi.ArgumentList.Add("--service-host");pi.ArgumentList.Add(file);using var helper=Process.Start(pi)!;
  for(int i=0;i<80;i++){if(File.Exists(request.Receipt))return JsonSerializer.Deserialize<OwnedProcess>(File.ReadAllText(request.Receipt))!;if(File.Exists(request.Receipt+".error"))throw new Exception(File.ReadAllText(request.Receipt+".error"));Thread.Sleep(100);}
  throw new Exception("Service helper did not report startup. Inspect session logs before retrying.");
 }
}
