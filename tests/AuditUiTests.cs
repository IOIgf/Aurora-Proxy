using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using AuroraProxy;
class AuditUiTests {
 static int count;static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,Flags).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,Flags).Invoke(o,args);}
 static void Reject(Action action,string name){try{action();}catch(TargetInvocationException){Check(true,name);return;}Check(false,name);}
 static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
 static void Idle(MainForm main){var watch=Stopwatch.StartNew();while(Field<bool>(main,"busy")){Pump(20);if(watch.ElapsedMilliseconds>10000)throw new TimeoutException("UI did not settle");}Pump(50);}
 static int Port(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();int p=((IPEndPoint)l.LocalEndpoint).Port;l.Stop();return p;}
 [STAThread] static int Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"aurora-audit-ui-"+Guid.NewGuid().ToString("N"));MainForm main=null;Engine engine=null;
  try{Application.EnableVisualStyles();var store=new Store(root);store.State.CorePath=Path.GetFullPath(args[0]);store.State.Port=Port();store.State.ApiPort=Port();var profile=new Subscription(){Name="Old title",Usage="old usage",AutoName=false};store.State.Profiles.Add(profile);store.State.Active=profile.Id;
   string yaml="proxies:\n  - name: A\n    type: http\n    server: 127.0.0.1\n    port: 9\n  - name: B\n    type: http\n    server: 127.0.0.1\n    port: 9\nproxy-groups:\n  - name: A-FIRST\n    type: select\n    proxies: [A, B]\nrules:\n  - MATCH,DIRECT\n";Store.Atomic(store.ProfilePath(profile),yaml);store.Save();main=new MainForm(store,true);main.Show();engine=Field<Engine>(main,"engine");engine.Start(profile);engine.Api("PUT","/proxies/GLOBAL",new Dictionary<string,object>{{"name","A-FIRST"}});
   var mode=Field<ComboBox>(main,"mode");mode.SelectedIndex=1;Idle(main);Check(store.State.Mode=="global"&&Field<int>(main,"page")==0,"configured global-mode switch stays on the overview");main.PreviewPage(2);Idle(main);Check((Field<ComboBox>(main,"groups").SelectedItem as string)=="GLOBAL","node page initially focuses GLOBAL in global mode");
   main.PreviewPage(0);mode=Field<ComboBox>(main,"mode");Check(mode.SelectedIndex==1,"rebuilt overview reflects saved mode");mode.SelectedIndex=0;Idle(main);Check(store.State.Mode=="rule"&&mode.SelectedIndex==0,"switching back keeps overview and controller aligned");
   main.GetType().GetField("proxyData",Flags).SetValue(main,new Dictionary<string,object>());main.GetType().GetField("pendingGroup",Flags).SetValue(main,"GLOBAL");main.PreviewPage(2);Idle(main);Check((Field<ComboBox>(main,"groups").SelectedItem as string)=="GLOBAL","GLOBAL guidance survives an initially empty proxy cache in rule mode");main.PreviewPage(0);
   string settingsPath=Path.Combine(root,"settings.json"),backup=settingsPath+".backup";File.Move(settingsPath,backup);Directory.CreateDirectory(settingsPath);
   string changed=yaml.Replace("name: A","name: C").Replace("[A, B]","[C, B]");Reject(delegate{Call(main,"CommitProfile",profile,changed,(Action)delegate{profile.Name="Changed title";profile.Usage="changed usage";profile.AutoName=true;profile.Url="https://example.test/new";});},"failed profile metadata save is reported");
   Check(profile.Name=="Old title"&&profile.Usage=="old usage"&&!profile.AutoName&&profile.Url=="","failed subscription save rolls metadata back");Check(File.ReadAllText(store.ProfilePath(profile))==yaml,"failed subscription save restores cached YAML");var proxies=(Dictionary<string,object>)engine.Api("GET","/proxies",null)["proxies"];Check(proxies.ContainsKey("A")&&!proxies.ContainsKey("C")&&engine.Running,"failed subscription save restores active core config");
   var added=new Subscription(){Name="failed import"};Store.Atomic(store.ProfilePath(added),yaml);Reject(delegate{Call(main,"AddProfile",added);},"failed import settings save is reported");Check(store.State.Profiles.Count==1&&store.Active==profile&&!File.Exists(store.ProfilePath(added)),"failed import removes collection entry and orphaned cache");Directory.Delete(settingsPath);File.Move(backup,settingsPath);
   main.PreviewPage(3);for(int i=0;i<5000;i++)Call(main,"AddLog","line "+i);Check(Field<Queue<string>>(main,"pendingLogs").Count==1000,"log burst has bounded pending memory");Pump(200);Check(Field<Queue<string>>(main,"logLines").Count==1000&&Field<Queue<string>>(main,"pendingLogs").Count==0,"logs flush in batches within bounded history");var logs=Field<RichTextBox>(main,"logs");logs.SelectionStart=0;logs.SelectionLength=4;Call(main,"AddLog","new line");Pump(180);Check(logs.SelectionStart==0&&logs.SelectionLength==4,"new log output preserves a selection while browsing history");
   main.PreviewPage(2);Idle(main);Check(Field<ListView>(main,"nodes").Items.Count>0,"node page still refreshes after a rolled-back reload");int generation=engine.Generation;main.Dispose();Check(!engine.Running&&engine.Generation!=generation,"disposing a form without Close also stops the core");bool rejected=false;try{engine.Start(profile);}catch(ObjectDisposedException){rejected=true;}Check(rejected,"disposed engine cannot be restarted by queued work");Check(!Field<System.Windows.Forms.Timer>(main,"timer").Enabled&&!Field<System.Windows.Forms.Timer>(main,"logTimer").Enabled,"disposal stops poll and log timers");main=null;
   Console.WriteLine("Completed "+count+" audit UI checks.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(main!=null)main.Dispose();if(engine!=null)engine.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
