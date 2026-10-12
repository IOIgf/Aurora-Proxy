using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AuroraProxy;

class SubscriptionNameTests {
 static int count;
 static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);count++;Console.WriteLine("PASS: "+text);}
 static string Enc(string text){return "base64:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(text));}
 static DownloadResult Response(string title,string disposition,bool redirect){
  var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  using(var listener=new HttpListener()){
   listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();Exception error=null;
   var thread=new Thread(delegate(){try{
    if(redirect){var first=listener.GetContext();first.Response.StatusCode=302;first.Response.RedirectLocation="/final";first.Response.Headers["profile-title"]="wrong redirect title";first.Response.Close();}
    var context=listener.GetContext();if(title!=null)context.Response.Headers["profile-title"]=title;if(disposition!=null)context.Response.Headers["Content-Disposition"]=disposition;
    context.Response.Headers["subscription-userinfo"]="upload=1024;download=2048;total=1073741824";
    byte[] body=Encoding.UTF8.GetBytes("proxies: []\n");context.Response.ContentLength64=body.Length;context.Response.OutputStream.Write(body,0,body.Length);context.Response.Close();
   }catch(Exception ex){error=ex;}});thread.IsBackground=true;thread.Start();
   try{var result=Engine.Download("http://127.0.0.1:"+port+"/sub?token=test-secret",false,7890);if(!thread.Join(3000))throw new Exception("Test server timeout");if(error!=null)throw error;return result;}
   finally{listener.Stop();thread.Join(3000);}
  }
 }
 static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-name-tests-"+Guid.NewGuid().ToString("N"));try{
  Check(SubscriptionNames.FromHeaders("机场 A","attachment; filename=other.yaml")=="机场 A","profile title takes priority over download filename");
  Check(SubscriptionNames.FromHeaders(Enc("中文机场 · Premium"),null)=="中文机场 · Premium","base64 UTF-8 profile title preserves Chinese");
  string unpadded=Enc("测试😀").TrimEnd('=').Replace('+','-').Replace('/','_');Check(SubscriptionNames.FromHeaders(unpadded,null)=="测试😀","unpadded URL-safe base64 titles are accepted");
  Check(SubscriptionNames.FromHeaders("base64:%%%","attachment; filename=backup.yml")=="backup","invalid base64 falls back to filename");
  Check(SubscriptionNames.FromHeaders("base64:/w==","attachment; filename=backup.yml")=="backup","invalid UTF-8 base64 cannot break import");
  Check(SubscriptionNames.FromHeaders("  ","attachment; filename=Airport.yaml")=="Airport","blank title uses filename without YAML extension");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=ascii.yaml; filename*=UTF-8''%E4%B8%AD%E6%96%87%20%E6%9C%BA%E5%9C%BA.yaml")=="中文 机场","RFC extended filename wins over ASCII filename");
  Check(SubscriptionNames.FromHeaders(null,"attachment; FILENAME*=UTF-8'en'%E6%B5%8B%E8%AF%95.YML")=="测试","extended filename supports language and case-insensitive parameters");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename*=ISO-8859-1''Caf%E9.yaml")=="Café","Latin-1 extended filenames are decoded");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=backup.yml; filename*=UTF-8''bad%XX")=="backup","malformed percent escapes use regular filename");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=backup.yml; filename*=UTF-8''%FF")=="backup","invalid encoded UTF-8 uses regular filename");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=backup.yml; filename*=unknown''Name")=="backup","unknown charsets use regular filename");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=\"Airport; VIP.yaml\"")=="Airport; VIP","quoted semicolons remain part of a filename");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=\"Airport \\\"VIP\\\".yaml\"")=="Airport \"VIP\"","quoted filename escapes are handled");
  Check(SubscriptionNames.FromHeaders(null,"attachment; filename=../folder/Airport.yaml")=="Airport","filename paths are reduced to display basename");
  string mojibake=Encoding.GetEncoding(28591).GetString(Encoding.UTF8.GetBytes("中文机场"));Check(SubscriptionNames.FromHeaders(mojibake,null)=="中文机场","raw UTF-8 header bytes decoded as Latin-1 are repaired");
  Check(SubscriptionNames.FromHeaders("Café",null)=="Café","legitimate Latin-1 text remains intact");
  Check(SubscriptionNames.FromHeaders(" A\r\n B\tC\0D ",null)=="A B C D","header control characters cannot create extra UI lines");
  Check(SubscriptionNames.FromHeaders(new string('x',5000),"attachment; filename=backup.yml")=="backup","oversized title is ignored");
  Check(SubscriptionNames.FromHeaders(new string('x',100),null).Length==80,"automatic names fit the existing name limit");
  Check(SubscriptionNames.FromHeaders(new string('x',79)+"😀tail",null).Length==79,"truncation does not split a Unicode surrogate pair");
  Check(SubscriptionNames.Resolve("我的别名","服务商名称","https://example.test/sub")=="我的别名","manual names always win");
  Check(SubscriptionNames.Resolve("手动  别名","服务商名称",null)=="手动  别名","manual alias spacing remains unchanged");
  Check(SubscriptionNames.Resolve(null,"服务商名称","https://example.test/sub")=="服务商名称","detected name is used for automatic import");
  Check(SubscriptionNames.Resolve(null,null,"https://example.test/private-secret?token=hidden")=="订阅 · example.test","fallback uses only host, never URL credentials or tokens");
  Check(SubscriptionNames.Resolve(null,null,"https://user:password@example.test/sub")=="订阅 · example.test","fallback excludes URL user information");
  Check(SubscriptionNames.Resolve(null,null,null)=="我的订阅","missing metadata still produces a usable name");
  var automatic=new Subscription{Name="旧名称",AutoName=true,Url="https://example.test/sub"};SubscriptionNames.ApplyDownloaded(automatic,new DownloadResult{Name="新名称"});Check(automatic.Name=="新名称","automatic update follows changed provider title");
  SubscriptionNames.ApplyDownloaded(automatic,new DownloadResult());Check(automatic.Name=="新名称","missing update metadata preserves an existing automatic name");
  var manual=new Subscription{Name="手动别名",Url=automatic.Url};SubscriptionNames.ApplyDownloaded(manual,new DownloadResult{Name="服务商新名称"});Check(manual.Name=="手动别名","updates do not rename manual or legacy profiles");
  var store=new Store(root);store.State.Profiles.Add(automatic);store.State.Profiles.Add(manual);store.Save();var loaded=new Store(root);
  Check(loaded.State.Profiles[0].AutoName&&loaded.State.Profiles[0].Name=="新名称","automatic naming mode survives save and reload");
  Check(!loaded.State.Profiles[1].AutoName&&loaded.State.Profiles[1].Name=="手动别名","manual naming mode survives save and reload");
  Store.Atomic(Path.Combine(root,"settings.json"),"{\"Profiles\":[{\"Id\":\"0123456789abcdef0123456789abcdef\",\"Name\":\"旧版名称\",\"Url\":\"\"}]}");loaded=new Store(root);
  Check(!loaded.State.Profiles[0].AutoName&&loaded.State.Profiles[0].Name=="旧版名称","old settings without AutoName retain their names");
  var response=Response(Enc("真实响应标题"),"attachment; filename=other.yaml",false);Check(response.Name=="真实响应标题"&&response.Yaml=="proxies: []\n"&&response.Usage.Contains("KB"),"actual HTTP download returns name, YAML and usage together");
  response=Response(null,"attachment; filename*=UTF-8''%E6%9C%BA%E5%9C%BA.yaml",false);Check(response.Name=="机场","HTTP extended filename is extracted after download");
  response=Response(Enc("重定向后的标题"),null,true);Check(response.Name=="重定向后的标题","redirects use the final response metadata");
  response=Response("base64:%%%",null,false);Check(response.Name==null&&response.Yaml=="proxies: []\n","malformed metadata does not reject valid downloaded content");
  Console.WriteLine("Completed "+count+" subscription name checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
}
