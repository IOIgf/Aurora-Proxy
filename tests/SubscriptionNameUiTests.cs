using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AuroraProxy;

class SubscriptionNameUiTests {
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static int count;static volatile string title="机场 A",body="proxies: []\n";
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,flags).GetValue(owner);}
 static void Invoke(object owner,string name,params object[] args){owner.GetType().GetMethod(name,flags).Invoke(owner,args);}
 static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);count++;Console.WriteLine("PASS: "+message);}
 static void Pump(){for(int i=0;i<10;i++){Application.DoEvents();Thread.Sleep(5);}}
 static void Wait(MainForm form){var until=DateTime.UtcNow.AddSeconds(10);while(Field<bool>(form,"busy")&&DateTime.UtcNow<until)Pump();Check(!Field<bool>(form,"busy"),"subscription operation completes asynchronously");}
 static void Dialog(MainForm form,string method,string name,string url){
  Exception failure=null;bool opened=false;
  using(var timer=new System.Windows.Forms.Timer{Interval=40}){
   timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().OfType<SubscriptionDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();opened=true;
    try{Field<TextBox>(dialog,"name").Text=name;Field<TextBox>(dialog,"url").Text=url;dialog.Controls.OfType<SoftButton>().First(b=>b.Text=="导入订阅"||b.Text=="保存修改").PerformClick();}
    catch(Exception ex){failure=ex;dialog.DialogResult=DialogResult.Cancel;dialog.Close();}
   };
   timer.Start();Invoke(form,method);if(failure!=null)throw failure;Check(opened,"actual subscription dialog is used");Wait(form);
  }
 }
 static void Select(MainForm form,Subscription profile){Invoke(form,"FillProfiles",profile.Id);var list=Field<ListView>(form,"subscriptions");foreach(ListViewItem item in list.Items)item.Selected=((Subscription)item.Tag).Id==profile.Id;list.Focus();Pump();}
 [STAThread]static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-name-ui-"+Guid.NewGuid().ToString("N"));HttpListener listener=null;Thread thread=null;try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  var portProbe=new TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
  string url="http://127.0.0.1:"+port+"/sub?token=test-only";listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
  thread=new Thread(delegate(){try{while(listener.IsListening){var context=listener.GetContext();if(title!=null)context.Response.Headers["profile-title"]="base64:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(title));byte[] bytes=Encoding.UTF8.GetBytes(body);context.Response.ContentLength64=bytes.Length;context.Response.OutputStream.Write(bytes,0,bytes.Length);context.Response.Close();}}catch(HttpListenerException){}catch(ObjectDisposedException){}});thread.IsBackground=true;thread.Start();
  var store=new Store(root);store.State.CorePath=Path.Combine(root,"not-installed.exe");store.State.Animations=false;
  using(var main=new MainForm(store,true)){
   main.Show();main.PreviewPage(1);Pump();
   using(var dialog=new SubscriptionDialog(null,false)){Check(dialog.NameValue=="","new import defaults to automatic naming without extra clicks");}
   Dialog(main,"ImportUrl","",url);var automatic=store.State.Profiles[0];Check(automatic.AutoName&&automatic.Name=="机场 A","blank-name import saves the downloaded provider title");
   Dialog(main,"ImportUrl","手动名称",url);var manual=store.State.Profiles[1];Check(!manual.AutoName&&manual.Name=="手动名称","explicit-name import preserves the user's alias");
   title="机场 B";Invoke(main,"RefreshProfile",automatic,false);Check(automatic.Name=="机场 B","actual refresh path updates an automatic provider title");
   Invoke(main,"RefreshProfile",manual,false);Check(manual.Name=="手动名称","actual refresh path leaves a manual name unchanged");
   Select(main,manual);Dialog(main,"EditProfile","",url);Check(manual.AutoName&&manual.Name=="机场 B","clearing an existing name enables automatic naming even with an unchanged URL");
   Select(main,manual);Dialog(main,"EditProfile",manual.Name,url);Check(manual.AutoName,"saving an unchanged automatic name retains automatic mode");
   Select(main,manual);Dialog(main,"EditProfile","自定义别名",url);Check(!manual.AutoName&&manual.Name=="自定义别名","editing the detected name explicitly switches to manual mode");
   string previous=automatic.Name,cache=File.ReadAllText(store.ProfilePath(automatic));title="不应写入的标题";body="not a valid configuration";bool rejected=false;
   try{Invoke(main,"RefreshProfile",automatic,false);}catch(TargetInvocationException){rejected=true;}
   Check(rejected&&automatic.Name==previous&&File.ReadAllText(store.ProfilePath(automatic))==cache,"invalid configuration cannot change a name or cached profile");
   body="proxies: []\n";title=null;Invoke(main,"RefreshProfile",automatic,false);Check(automatic.Name==previous,"missing headers on refresh retain the last detected title");
   var reload=new Store(root);Check(reload.State.Profiles[0].AutoName&&!reload.State.Profiles[1].AutoName&&reload.State.Profiles[1].Name=="自定义别名","actual import/edit naming modes persist across restart");main.Close();
  }Console.WriteLine("Completed "+count+" subscription name UI checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(listener!=null){listener.Stop();listener.Close();}if(thread!=null)thread.Join(3000);if(Directory.Exists(root))Directory.Delete(root,true);}}
}
