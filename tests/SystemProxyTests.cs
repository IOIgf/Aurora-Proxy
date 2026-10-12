using System;
using System.IO;
using System.Reflection;
using AuroraProxy;
class SystemProxyTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS: "+name);}
 static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
 static ProxySnapshot Read(){return (ProxySnapshot)typeof(SystemProxy).GetMethod("Read",Flags).Invoke(null,null);}
 static void Write(ProxySnapshot value){typeof(SystemProxy).GetMethod("Write",Flags).Invoke(null,new object[]{value});}
 static bool Same(ProxySnapshot a,ProxySnapshot b){return a.Flags==b.Flags&&a.Server==b.Server&&a.Bypass==b.Bypass&&a.Pac==b.Pac;}
 static int Main(){if(!SystemProxy.Windows){Console.WriteLine("SKIP: native WinInet checks require Windows");return 0;}string root=Path.Combine(Path.GetTempPath(),"aurora-proxy-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);ProxySnapshot original=null;
  try{original=Read();var proxy=new SystemProxy(root);Check(!proxy.Owned,"no journal means proxy is not owned");proxy.Enable(28999);Check(proxy.Owned&&Read().Server=="127.0.0.1:28999","enable applies owned system proxy");proxy.Enable(28999);proxy.Restore();Check(!proxy.Owned&&Same(Read(),original),"repeated enable preserves original recovery snapshot");proxy.Enable(28999);var external=new ProxySnapshot(){Flags=3,Server="127.0.0.2:29999",Bypass="<local>",Pac=""};Write(external);Check(!proxy.Owned,"external proxy changes are not shown as our active proxy");proxy.Restore();Check(!File.Exists(Path.Combine(root,"proxy-recovery.json"))&&Same(Read(),external),"restore preserves another application's proxy and removes stale journal");proxy.Enable(28999);Write(external);proxy.Enable(28999);Check(proxy.Owned,"explicit re-enable can replace stale ownership");proxy.Restore();Check(Same(Read(),external),"re-enabled proxy restores latest external state");File.WriteAllText(Path.Combine(root,"proxy-recovery.json"),"{}");bool rejected=false;try{proxy.Restore();}catch(InvalidDataException){rejected=true;}Check(rejected&&File.Exists(Path.Combine(root,"proxy-recovery.json")),"incomplete journal is preserved and reported");Console.WriteLine("Completed "+count+" native proxy checks.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(original!=null)Write(original);Directory.Delete(root,true);}
 }
}
