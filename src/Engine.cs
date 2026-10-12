using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuroraProxy {
 public class DownloadResult {public string Yaml,Usage,Name;}
 public sealed class Engine : IDisposable {
  public readonly Store Store;public readonly SystemProxy Proxy;
  public event Action<string> Log;public event Action Exited;
  Process process;ChildJob job;readonly object sync=new object();string secret;int apiPort;public string ActiveId{get;private set;}
  public Engine(Store s){Store=s;Proxy=new SystemProxy(s.Root);secret=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");}
  public bool Running {get{var p=process;try{return p!=null&&!p.HasExited;}catch{return false;}}}
  public string CoreFile {get{string p=Store.State.CorePath;if(String.IsNullOrWhiteSpace(p))p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"core","mihomo.exe");return Path.GetFullPath(p);}}
  void Emit(string s){var e=Log;if(e!=null)e(s);}
  static string Quote(string s){return "\""+s.Replace("\"","")+"\"";}
  public static DownloadResult Download(string url,bool throughProxy,int port){
   Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||(u.Scheme!="https"&&u.Scheme!="http"))throw new ArgumentException("请输入完整的 HTTP / HTTPS 订阅链接。");
   var req=(HttpWebRequest)WebRequest.Create(u);req.UserAgent="clash.meta/AuroraProxy-1.0";req.Accept="application/yaml,text/yaml,text/plain,*/*";
   req.Timeout=25000;req.ReadWriteTimeout=25000;req.AllowAutoRedirect=true;req.MaximumAutomaticRedirections=5;
   req.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;req.Proxy=throughProxy?new WebProxy("http://127.0.0.1:"+port):null;
   try{using(var res=(HttpWebResponse)req.GetResponse())using(var input=res.GetResponseStream())using(var output=new MemoryStream()){
    byte[] buf=new byte[8192];int n;while((n=input.Read(buf,0,buf.Length))>0){if(output.Length+n>8*1024*1024)throw new InvalidDataException("订阅超过 8 MiB。");output.Write(buf,0,n);}
    string yaml=new UTF8Encoding(false,true).GetString(output.ToArray());return new DownloadResult(){Yaml=yaml,Usage=FormatUsage(res.Headers["subscription-userinfo"]),Name=SubscriptionNames.FromHeaders(res.Headers["profile-title"],res.Headers["Content-Disposition"])};
   }}catch(WebException ex){if(ex.Response!=null){using(var res=(HttpWebResponse)ex.Response){throw new IOException("下载失败：HTTP "+(int)res.StatusCode+"。请检查订阅是否过期。",ex);}}
    throw new IOException("订阅下载失败。请检查网络、链接和 Win7 的 TLS 1.2 更新；内核运行后可尝试“通过代理下载”。",ex);}
  }
  public static string FormatUsage(string header){if(String.IsNullOrWhiteSpace(header))return "服务商未提供流量信息";
   var d=new Dictionary<string,long>();foreach(Match m in Regex.Matches(header,@"(upload|download|total|expire)\s*=\s*(\d+)")){long n;if(Int64.TryParse(m.Groups[2].Value,out n))d[m.Groups[1].Value]=n;}
   string s="";if(d.ContainsKey("total")){long used=(d.ContainsKey("upload")?d["upload"]:0)+(d.ContainsKey("download")?d["download"]:0);s=Bytes(used)+" / "+Bytes(d["total"]);}
   if(d.ContainsKey("expire")&&d["expire"]>0){try{s+=" · 到期 "+new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(d["expire"]).ToLocalTime().ToString("yyyy-MM-dd");}catch{}}
   return String.IsNullOrEmpty(s)?"服务商未提供流量信息":s;
  }
  public static string Bytes(double n){string[] units={"B","KB","MB","GB","TB"};int i=0;while(n>=1024&&i<4){n/=1024;i++;}return n.ToString(i==0?"0":"0.0")+" "+units[i];}
  public string Build(string yaml){return Config.Build(yaml,Store.State,secret);}
  public void Validate(string runtime){
   if(!File.Exists(CoreFile))return;
   string file=Path.Combine(Store.Work,"validate-"+Guid.NewGuid().ToString("N")+".yaml");File.WriteAllText(file,runtime,new UTF8Encoding(false));
   try{RunCheck("-t -d "+Quote(Store.Work)+" -f "+Quote(file),45000);}finally{File.Delete(file);}
  }
  string RunCheck(string arguments,int timeout){
   using(var p=new Process()){p.StartInfo=new ProcessStartInfo(CoreFile,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Store.Work};
    var lines=new StringBuilder();object gate=new object();DataReceivedEventHandler add=delegate(object o,DataReceivedEventArgs a){if(a.Data!=null)lock(gate)lines.AppendLine(a.Data);};
    p.OutputDataReceived+=add;p.ErrorDataReceived+=add;p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
    if(!p.WaitForExit(timeout)){try{p.Kill();p.WaitForExit();}catch{}throw new TimeoutException("内核配置检查超时。检查 GEO 数据、规则集下载或内核是否兼容 Win7。");}p.WaitForExit();
    if(p.ExitCode!=0)throw new InvalidDataException("mihomo 配置检查失败：\n"+lines.ToString());return lines.ToString();
   }
  }
  static void PortAvailable(int port,bool all=false){var l=new TcpListener(all?IPAddress.Any:IPAddress.Loopback,port);try{l.Start();}catch(SocketException){throw new IOException("端口 "+port+" 已被占用，请在设置中更换。");}finally{l.Stop();}}
  public void Start(Subscription profile){lock(sync){
   if(Running)return;if(profile==null)throw new InvalidOperationException("请先导入并选中一个订阅。");
   if(!File.Exists(CoreFile))throw new FileNotFoundException("未找到 mihomo 内核。请把官方 Windows 内核放到 core\\mihomo.exe，或在设置中选择内核文件。");
   string yaml=File.ReadAllText(Store.ProfilePath(profile));string runtime=Build(yaml);Validate(runtime);
   PortAvailable(Store.State.Port,Store.State.AllowLan);PortAvailable(Store.State.ApiPort);apiPort=Store.State.ApiPort;
   string path=Path.Combine(Store.Work,"config.yaml");Store.Atomic(path,runtime);
   var p=new Process();p.StartInfo=new ProcessStartInfo(CoreFile,"-d "+Quote(Store.Work)+" -f "+Quote(path)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Store.Work};
   p.OutputDataReceived+=delegate(object o,DataReceivedEventArgs a){if(a.Data!=null)Emit(a.Data);};p.ErrorDataReceived+=delegate(object o,DataReceivedEventArgs a){if(a.Data!=null)Emit(a.Data);};
   process=p;try{p.Start();job=new ChildJob();if(!job.Attach(p))Emit("提示：当前环境未启用进程联动退出；正常退出仍会停止内核。");p.BeginOutputReadLine();p.BeginErrorReadLine();
    bool ready=false;for(int i=0;i<60;i++){if(p.HasExited)throw new IOException("mihomo 提前退出，请查看运行日志。");try{Api("GET","/version",null,500);ready=true;break;}catch{}Thread.Sleep(150);}
    if(!ready)throw new IOException("内核控制接口未就绪，请查看日志和端口设置。");
    ActiveId=profile.Id;p.Exited+=delegate{if(process==p){try{Proxy.Restore();}catch(Exception ex){Emit("代理恢复失败："+ex.Message);}var e=Exited;if(e!=null)e();}};p.EnableRaisingEvents=true;Emit("已启动订阅："+profile.Name);
   }catch{Stop();throw;}
  }}
  public void Apply(Subscription profile,string yaml){lock(sync){string runtime=Build(yaml);Validate(runtime);
   if(!Running)return;
   Api("PUT","/configs?force=true",new Dictionary<string,object>{{"payload",runtime}});
   ActiveId=profile.Id;Store.Atomic(Path.Combine(Store.Work,"config.yaml"),runtime);Emit("已应用订阅："+profile.Name);
  }}
  public Dictionary<string,object> Api(string method,string path,object body,int timeout=8000){
   var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+apiPort+path);req.Method=method;req.Proxy=null;req.Timeout=timeout;req.ReadWriteTimeout=timeout;req.Headers["Authorization"]="Bearer "+secret;
   if(body!=null){byte[] data=Encoding.UTF8.GetBytes(Store.Json.Serialize(body));req.ContentType="application/json";req.ContentLength=data.Length;using(var s=req.GetRequestStream())s.Write(data,0,data.Length);}
   try{using(var res=req.GetResponse())using(var reader=new StreamReader(res.GetResponseStream())){string s=reader.ReadToEnd();return String.IsNullOrWhiteSpace(s)?new Dictionary<string,object>():Store.Json.Deserialize<Dictionary<string,object>>(s);}}
   catch(WebException ex){if(ex.Response!=null)using(var res=ex.Response)using(var reader=new StreamReader(res.GetResponseStream())){throw new IOException("控制接口请求失败："+reader.ReadToEnd(),ex);}throw;}
  }
  public void ChangeMode(string mode){lock(sync){if(Running)Api("PATCH","/configs",new Dictionary<string,object>{{"mode",mode}});Store.State.Mode=mode;Store.Save();}}
  public void ToggleSystemProxy(){lock(sync){if(Proxy.Owned){Proxy.Restore();return;}if(!Running)throw new InvalidOperationException("请先启动内核。");Proxy.Enable(Store.State.Port);if(!Running){Proxy.Restore();throw new IOException("内核已退出，系统代理已恢复。");}}}
  public void Stop(){lock(sync){Exception restoreError=null;try{Proxy.Restore();}catch(Exception ex){restoreError=ex;}
   var p=process;process=null;ActiveId=null;if(p!=null){try{if(!p.HasExited){p.Kill();p.WaitForExit(3000);}}catch(Exception ex){Emit("停止内核："+ex.Message);}p.Dispose();}if(job!=null){job.Dispose();job=null;}
   if(restoreError!=null)throw new IOException("系统代理恢复失败，恢复记录已保留。请重新启动客户端恢复，或在 Windows Internet 选项中手动关闭代理。",restoreError);
  }}
  public void Dispose(){Stop();}
 }
}
