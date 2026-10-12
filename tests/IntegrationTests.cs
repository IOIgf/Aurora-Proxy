using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using AuroraProxy;

class IntegrationTests {
 static int count;
 static void Assert(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
 static void Throws(Action a,string name){try{a();}catch{Assert(true,name);return;}Assert(false,name);}
 const string Yaml="proxies:\n  - name: 香港 A\n    type: ss\n    server: 127.0.0.1\n    port: 9\n    cipher: aes-128-gcm\n    password: example-only\n  - name: 日本 B\n    type: ss\n    server: 127.0.0.1\n    port: 9\n    cipher: aes-128-gcm\n    password: example-only\nproxy-groups:\n  - name: 节点选择\n    type: select\n    proxies:\n      - 香港 A\n      - 日本 B\nrules:\n  - MATCH,节点选择\n";
 static string S(Dictionary<string,object> d,string k){return d[k].ToString();}
 static int Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"aurora-tests-"+Guid.NewGuid().ToString("N"));Engine e=null;
  try{var store=new Store(root);store.State.CorePath=Path.GetFullPath(args[0]);store.State.Port=28790;store.State.ApiPort=29090;
   var p=new Subscription(){Name="测试订阅",Url="https://example.test/private-token",Updated=DateTime.UtcNow.ToString("o")};store.State.Profiles.Add(p);store.State.Active=p.Id;Store.Atomic(store.ProfilePath(p),Yaml);store.Save();
   var reload=new Store(root);Assert(reload.State.Profiles.Count==1&&reload.Active.Name==p.Name&&reload.Active.Url==p.Url,"settings and selected subscription round-trip");
   Store.Atomic(store.ProfilePath(p),Yaml+"# updated\n");Assert(File.ReadAllText(store.ProfilePath(p)).EndsWith("# updated\n"),"atomic profile replacement");
   string overrideYaml=Config.Build("'mixed-port': 1234\nexternal-controller: 0.0.0.0:9090\nsecret: old\ntun:\n  enable: true\nlisteners:\n  - name: unsafe\n    type: mixed\n    port: 7891\n"+Yaml,28790,29090,"rule","test");
   Assert(!overrideYaml.Contains("external-controller: 0.0.0.0")&&!overrideYaml.Contains("bind-address: 0.0.0.0")&&!overrideYaml.Contains("enable: true")&&!overrideYaml.Contains("unsafe")&&overrideYaml.Contains("mixed-port: 28790"),"owned ports, controller, TUN and listeners override");
   Throws(delegate{Config.Build("vmess://abc",7890,9090,"rule","x");},"raw node links rejected");
   Throws(delegate{Config.Build("proxies: []\nproxies: []",7890,9090,"rule","x");},"duplicate YAML fields rejected");
   Throws(delegate{Config.Build(Yaml+"---\nproxies: []",7890,9090,"rule","x");},"multiple YAML documents rejected");
   Throws(delegate{Config.Build("<<: *base\n"+Yaml,7890,9090,"rule","x");},"root merge keys rejected");
   e=new Engine(store);e.Validate(overrideYaml);Assert(true,"real mihomo -t accepts generated full config");
   string providerOnly="proxies:\n  - name: Demo\n    type: ss\n    server: 127.0.0.1\n    port: 9\n    cipher: aes-128-gcm\n    password: example\n";e.Validate(e.Build(providerOnly));Assert(true,"real mihomo accepts generated fallback group for nodes-only YAML");
   Throws(delegate{e.Validate(e.Build("proxies: [invalid-node]\n"));},"real core rejects malformed node config");
   Assert(!e.Running,"validation does not start proxy service");
   using(var listener=new HttpListener()){
    listener.Prefixes.Add("http://127.0.0.1:28788/");listener.Start();Exception serverError=null;var thread=new Thread(delegate(){try{var ctx=listener.GetContext();Assert(ctx.Request.UserAgent.Contains("clash.meta"),"Clash subscription user-agent");byte[] b=Encoding.UTF8.GetBytes(Yaml);ctx.Response.Headers["subscription-userinfo"]="upload=1024; download=2048; total=1073741824; expire=1893456000";ctx.Response.ContentLength64=b.Length;ctx.Response.OutputStream.Write(b,0,b.Length);ctx.Response.Close();}catch(Exception ex){serverError=ex;}});thread.Start();var result=Engine.Download("http://127.0.0.1:28788/sub",false,28790);thread.Join();if(serverError!=null)throw serverError;
    string expectedExpiry=new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd");
    Assert(result.Yaml==Yaml&&result.Usage.Contains((3.0).ToString("0.0")+" KB")&&result.Usage.Contains(expectedExpiry),"subscription download, UTF-8 nodes and usage header");
   }
   Throws(delegate{Engine.Download("file:///private",false,28790);},"non-HTTP subscription rejected");
   e.Start(p);Assert(e.Running,"actual core starts and controller becomes ready");Assert(S(e.Api("GET","/version",null),"version").Contains("1.19.32"),"controller version endpoint");
   e.Api("PUT","/proxies/"+Uri.EscapeDataString("节点选择"),new Dictionary<string,object>{{"name","日本 B"}});Assert(S(e.Api("GET","/proxies/"+Uri.EscapeDataString("节点选择"),null),"now")=="日本 B","unicode group and node selection");
   e.ChangeMode("global");Assert(S(e.Api("GET","/configs",null),"mode")=="global","routing mode switches through API");
   string before=File.ReadAllText(store.ProfilePath(p));Throws(delegate{e.Apply(p,"proxies: [invalid-node]\n");},"invalid reload rejected before active configuration changes");Assert(e.Running&&File.ReadAllText(store.ProfilePath(p))==before,"invalid reload preserves running core and cached profile");
   e.Apply(p,Yaml.Replace("日本 B","日本 C"));var proxies=(Dictionary<string,object>)e.Api("GET","/proxies",null)["proxies"];Assert(proxies.ContainsKey("日本 C"),"live reload accepts YAML payload");
   var c=e.Api("GET","/connections",null);Assert(c.ContainsKey("uploadTotal")&&c.ContainsKey("downloadTotal"),"connection metrics endpoint");
   e.Stop();Assert(!e.Running,"core stops cleanly");
   e.Start(p);Assert(e.Running,"core restarts after clean stop");e.Stop();
   Assert(Engine.FormatUsage("total=5;expire=999999999999999999").Length>0,"malformed expiry does not break subscription import");
   Console.WriteLine("Completed "+count+" checks.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(e!=null)e.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
