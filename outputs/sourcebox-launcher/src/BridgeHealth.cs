using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace SourceBox;
public static class BridgeHealth {
 public static async Task<bool> Matches(string directory,string executable,int port=27842){
  try{
   using var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false};
   using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(2)};
   var nonce=Guid.NewGuid().ToString();
   using var response=await http.GetAsync($"http://127.0.0.1:{port}/sourcebox-bridge?nonce={nonce}",HttpCompletionOption.ResponseHeadersRead);
   if(!response.IsSuccessStatusCode)return false;
   using var stream=await response.Content.ReadAsStreamAsync();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2));
   var bytes=new byte[4097];int size=0,count;
   while(size<bytes.Length&&(count=await stream.ReadAsync(bytes.AsMemory(size),deadline.Token))>0)size+=count;
   if(size>4096)return false;
   using var json=JsonDocument.Parse(bytes.AsMemory(0,size));var data=json.RootElement;
   if(data.GetProperty("protocol").GetInt32()!=1||data.GetProperty("component").GetString()!="sourcebox-gmod-bridge"||data.GetProperty("nonce").GetString()!=nonce)return false;
   if(!Path.TrimEndingDirectorySeparator(Path.GetFullPath(data.GetProperty("directory").GetString()!)).Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),StringComparison.OrdinalIgnoreCase))return false;
   using var process=Process.GetProcessById(data.GetProperty("pid").GetInt32());
   if(OwnerPid(port)!=process.Id)return false;
   if(!Path.GetFullPath(process.MainModule!.FileName).Equals(Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase))return false;
   var start=DateTimeOffset.FromUnixTimeMilliseconds(data.GetProperty("startedAt").GetInt64()).UtcDateTime;
   return Math.Abs((start-process.StartTime.ToUniversalTime()).TotalSeconds)<60;
  }catch{return false;}
 }
 public static string PortOwner(int port){
  var pid=OwnerPid(port);if(pid==null)return "unknown process";
  try{using var p=Process.GetProcessById(pid.Value);return $"{p.ProcessName} (PID {pid})";}catch{return $"PID {pid}";}
 }
 public static int? OwnerPid(int port){
  IntPtr table=IntPtr.Zero;
  try{
   int size=0;GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0);
   table=Marshal.AllocHGlobal(size);if(GetExtendedTcpTable(table,ref size,false,2,3,0)!=0)return null;
   for(int i=0;i<Marshal.ReadInt32(table);i++){
    var row=IntPtr.Add(table,4+i*24);int raw=Marshal.ReadInt32(row,8);
    int localPort=((raw&255)<<8)|((raw>>8)&255);
    if(localPort!=port)continue;
    return Marshal.ReadInt32(row,20);
   }
  }catch{}finally{if(table!=IntPtr.Zero)Marshal.FreeHGlobal(table);}
  return null;
 }
 [DllImport("iphlpapi.dll")]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
}
