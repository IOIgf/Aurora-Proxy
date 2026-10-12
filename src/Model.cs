using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AuroraProxy {
 public class Subscription {
  public string Id {get;set;} public string Name {get;set;} public string Url {get;set;}
  public string Updated {get;set;} public string Usage {get;set;}
  public bool AutoName {get;set;}
  public Subscription(){Id=Guid.NewGuid().ToString("N");Name="新订阅";Url="";Updated="尚未更新";Usage="";}
  public override string ToString(){return Name;}
 }
 public class Settings {
  public List<Subscription> Profiles {get;set;} public string Active {get;set;}
  public string CorePath {get;set;} public int Port {get;set;} public int ApiPort {get;set;}
  public string Mode {get;set;} public bool Dark {get;set;} public bool MinimizeToTray {get;set;}
  public bool UpdateAutomatically {get;set;} public int UpdateHours {get;set;}
  public bool Animations {get;set;}
  public bool AllowLan {get;set;} public bool LanAuthentication {get;set;} public string LanUser {get;set;} public string LanPassword {get;set;}
  public bool StartWithWindows {get;set;}
  public Settings(){Profiles=new List<Subscription>();Active="";CorePath="";Port=7890;ApiPort=19090;Mode="rule";Dark=true;MinimizeToTray=true;UpdateHours=24;Animations=true;LanAuthentication=true;LanUser="aurora";LanPassword=Guid.NewGuid().ToString("N").Substring(0,20);}
 }
 public class Store {
  public readonly string Root; public Settings State;
  public static JavaScriptSerializer Json {get{return new JavaScriptSerializer(){MaxJsonLength=16*1024*1024};}}
  public Store(string root){Root=root;Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.Combine(Root,"profiles"));Directory.CreateDirectory(Work);Load();}
  public string Work {get{return Path.Combine(Root,"runtime");}}
  public string ProfilePath(Subscription s){return Path.Combine(Root,"profiles",s.Id+".yaml");}
  public Subscription Active {get{return State.Profiles.Find(x=>x.Id==State.Active);}}
  public static void Atomic(string path,string text){
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  void Load(){
   string path=Path.Combine(Root,"settings.json");State=new Settings();if(!File.Exists(path))return;
   try{State=Json.Deserialize<Settings>(File.ReadAllText(path));if(State==null||State.Profiles==null)throw new Exception();
    var ids=new HashSet<string>();foreach(var s in State.Profiles){if(s==null||!ids.Add(s.Id??""))throw new Exception();if(!Regex.IsMatch(s.Id??"","^[a-f0-9]{32}$"))throw new Exception();s.Url=Unprotect(s.Url??"");}
    if(State.Port<1024||State.Port>65535||State.ApiPort<1024||State.ApiPort>65535||State.Port==State.ApiPort)throw new Exception();
    if(State.Mode!="rule"&&State.Mode!="global"&&State.Mode!="direct")State.Mode="rule";
    State.UpdateHours=Math.Max(1,Math.Min(720,State.UpdateHours));
    State.LanUser=State.LanUser??"aurora";State.LanPassword=Unprotect(State.LanPassword??"");
    Config.ValidateLan(State.AllowLan,State.LanAuthentication,State.LanUser,State.LanPassword);
   }catch(Exception ex){throw new InvalidDataException("本地设置无法读取。请先备份数据目录，再检查 settings.json；程序没有覆盖原文件。",ex);}
  }
  public void Save(){var copy=Json.Deserialize<Settings>(Json.Serialize(State));foreach(var s in copy.Profiles)s.Url=Protect(s.Url);copy.LanPassword=Protect(copy.LanPassword);Atomic(Path.Combine(Root,"settings.json"),Json.Serialize(copy));}
  public void UpdateSettings(Action<Settings> change){var before=State;var candidate=Json.Deserialize<Settings>(Json.Serialize(before));candidate.Profiles=before.Profiles;change(candidate);State=candidate;try{Save();}catch{State=before;throw;}}
  static bool Windows {get{return Environment.OSVersion.Platform==PlatformID.Win32NT;}}
  static string Protect(string s){if(!Windows||String.IsNullOrEmpty(s))return s;return "dpapi:"+Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s),null,DataProtectionScope.CurrentUser));}
  static string Unprotect(string s){if(!s.StartsWith("dpapi:"))return s;return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s.Substring(6)),null,DataProtectionScope.CurrentUser));}
 }
 public static class Config {
  // Block-root YAML stays byte-for-byte intact except app-owned top-level sections.
  // Flow-root, directives, merge roots and multi-document YAML are rejected explicitly.
  static readonly Regex Key=new Regex("^(?:([A-Za-z0-9_-]+)|\"([A-Za-z0-9_-]+)\"|'([A-Za-z0-9_-]+)')\\s*:");
  static readonly HashSet<string> Owned=new HashSet<string>(new string[]{"mixed-port","port","socks-port","redir-port","tproxy-port","allow-lan","bind-address","mode","external-controller","external-controller-tls","external-controller-unix","external-controller-pipe","external-controller-cors","secret","external-ui","external-ui-url","external-ui-name","tun","listeners","authentication","skip-auth-prefixes","lan-allowed-ips","lan-disallowed-ips","tls"});
  public static void ValidateLan(bool enabled,bool auth,string user,string password){if(!enabled||!auth)return;if(!Regex.IsMatch(user??"",@"\A[A-Za-z0-9._-]{1,32}\z"))throw new ArgumentException("共享用户名需为 1–32 位字母、数字、点、下划线或短横线。");if(!Regex.IsMatch(password??"",@"\A[!-9;-~]{8,128}\z"))throw new ArgumentException("共享密码需为 8–128 位可打印英文字符，不能含空格、换行或冒号。");}
  public static string Build(string yaml,int port,int api,string mode,string secret){return Build(yaml,new Settings(){Port=port,ApiPort=api,Mode=mode},secret);}
  public static string Build(string yaml,Settings settings,string secret){
   int port=settings.Port,api=settings.ApiPort;string mode=settings.Mode;if(port<1024||port>65535||api<1024||api>65535||port==api)throw new ArgumentException("代理端口和控制端口需为不同的 1024–65535 端口。");if(mode!="rule"&&mode!="global"&&mode!="direct")throw new ArgumentException("路由模式无效。");ValidateLan(settings.AllowLan,settings.LanAuthentication,settings.LanUser,settings.LanPassword);
   if(String.IsNullOrWhiteSpace(yaml)||yaml.Length>8*1024*1024)throw new InvalidDataException("订阅为空或超过 8 MiB。");
   yaml=yaml.TrimStart('\uFEFF');var lines=yaml.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
   var output=new StringBuilder();var keys=new HashSet<string>();bool skip=false,ended=false,document=false;
   foreach(string line in lines){string t=line.Trim();if(t==""||t.StartsWith("#")){if(!skip)output.AppendLine(line);continue;}
    if(Regex.IsMatch(line,@"^---(?:[ \t]+#.*)?[ \t]*$")){if(document||keys.Count>0||ended)throw new InvalidDataException("只支持单文档 YAML。");document=true;continue;}
    if(Regex.IsMatch(line,@"^\.\.\.(?:[ \t]+#.*)?[ \t]*$")){ended=true;continue;}if(ended)throw new InvalidDataException("只支持单文档 YAML。");
    if(line[0]!=' '&&line[0]!='\t'){
     // YAML allows root sequence values with no indentation after a mapping key.
     if((t=="-"||line.StartsWith("- "))&&keys.Count>0){if(!skip)output.AppendLine(line);continue;}
     Match m=Key.Match(line);if(!m.Success)throw new InvalidDataException("请使用 Clash/mihomo 的块式 YAML 配置。原始节点链接、Base64、根级合并键和流式根对象暂不支持。");
     string k=m.Groups[1].Value+m.Groups[2].Value+m.Groups[3].Value;
     if(!keys.Add(k))throw new InvalidDataException("配置包含重复顶层字段："+k);
     skip=Owned.Contains(k);
    }
    if(!skip)output.AppendLine(line);
   }
   if(!keys.Contains("proxies")&&!keys.Contains("proxy-providers")&&!keys.Contains("proxy-groups"))throw new InvalidDataException("订阅不包含 proxies / proxy-providers / proxy-groups，请在服务商处选择 Clash 或 mihomo 订阅格式。");
   if(!keys.Contains("proxy-groups")){
    output.AppendLine("proxy-groups:\n  - name: AURORA\n    type: select\n    include-all: true\n    proxies:\n      - DIRECT");
   }
   if(!keys.Contains("rules"))output.AppendLine("rules:\n  - MATCH,"+(keys.Contains("proxy-groups")?"DIRECT":"AURORA"));
   output.AppendLine("mixed-port: "+port+"\nport: 0\nsocks-port: 0\nredir-port: 0\ntproxy-port: 0\nallow-lan: "+(settings.AllowLan?"true":"false")+"\nbind-address: "+(settings.AllowLan?"0.0.0.0":"127.0.0.1")+"\nmode: "+mode+"\nexternal-controller: 127.0.0.1:"+api+"\nsecret: "+Store.Json.Serialize(secret)+"\ntun:\n  enable: false\nlisteners: []");
   output.AppendLine("lan-allowed-ips: [127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16]\nlan-disallowed-ips: []\nskip-auth-prefixes: [127.0.0.0/8, '::1/128']");
   output.AppendLine("authentication: "+(settings.AllowLan&&settings.LanAuthentication?"["+Store.Json.Serialize(settings.LanUser+":"+settings.LanPassword)+"]":"[]"));
   return output.ToString();
  }
 }
}
