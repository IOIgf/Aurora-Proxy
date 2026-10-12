using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using AuroraProxy;

class WheelProbe:NoWheelComboBox {
 public void NativeWheel(int delta,int message){var m=Message.Create(Handle,message,new IntPtr(delta<<16),IntPtr.Zero);WndProc(ref m);}
 public void ManagedWheel(){OnMouseWheel(new HandledMouseEventArgs(MouseButtons.None,0,5,5,120));}
}
class NodeUiTests {
 // Deterministic latency responses exercise the actual buttons and async
 // refresh path without contacting external test sites. Selection tests below
 // still use the real core; only delay endpoints use this local controller.
 class DelayController:IDisposable {
  HttpListener listener=new HttpListener();Thread worker;Dictionary<string,object> snapshot;
  public int SingleTests,GroupTests,Refreshes;
  public DelayController(Dictionary<string,object> data){snapshot=data;listener.Prefixes.Add("http://127.0.0.1:29093/");listener.Start();worker=new Thread(Serve){IsBackground=true};worker.Start();}
  void Serve(){try{while(listener.IsListening){var context=listener.GetContext();var proxies=(Dictionary<string,object>)snapshot["proxies"];string path=Uri.UnescapeDataString(context.Request.Url.AbsolutePath);object response;
   if(path=="/proxies"){Interlocked.Increment(ref Refreshes);response=snapshot;}
   else if(path.StartsWith("/proxies/")&&path.EndsWith("/delay")){string name=path.Substring(9,path.Length-15);((Dictionary<string,object>)proxies[name])["history"]=new object[]{new Dictionary<string,object>{{"delay",87}}};Interlocked.Increment(ref SingleTests);response=new Dictionary<string,object>{{"delay",87}};}
   else if(path.StartsWith("/group/")&&path.EndsWith("/delay")){var results=new Dictionary<string,object>();for(int i=0;i<120;i++){string name="节点 "+i.ToString("000");int delay=i==80?0:100+i;((Dictionary<string,object>)proxies[name])["history"]=new object[]{new Dictionary<string,object>{{"delay",delay}}};results[name]=delay;}Interlocked.Increment(ref GroupTests);response=results;}
   else {context.Response.StatusCode=404;response=new Dictionary<string,object>{{"message","unexpected endpoint"}};}
   byte[] bytes=Encoding.UTF8.GetBytes(Store.Json.Serialize(response));context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;context.Response.OutputStream.Write(bytes,0,bytes.Length);context.Response.Close();
  }}catch(HttpListenerException){}catch(ObjectDisposedException){}}
  public void Dispose(){listener.Close();worker.Join(2000);}
 }
 static int count;
 static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
 static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,Flags).GetValue(obj);}
 static void Idle(MainForm main){var clock=Stopwatch.StartNew();do{Pump(20);if(clock.ElapsedMilliseconds>5000)throw new Exception("UI operation timed out");}while(Field<bool>(main,"busy"));Pump(40);}
 static Control Find(Control root,string text){if(root.Text==text)return root;foreach(Control c in root.Controls){var found=Find(c,text);if(found!=null)return found;}return null;}
 [STAThread] static int Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"aurora-node-ui-"+Guid.NewGuid().ToString("N"));Engine cleanupEngine=null;
  try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var form=new Form())using(var combo=new WheelProbe(){DropDownStyle=ComboBoxStyle.DropDownList}){
    combo.Items.AddRange(new object[]{"分类 A","分类 B","分类 C"});form.Controls.Add(combo);form.Show();combo.SelectedIndex=1;combo.Focus();int changes=0;combo.SelectedIndexChanged+=delegate{changes++;};
    combo.NativeWheel(120,0x020A);combo.NativeWheel(-120,0x020A);combo.ManagedWheel();Check(combo.SelectedIndex==1&&changes==0,"focused category ignores native and managed vertical wheel input");
    combo.NativeWheel(120,0x020E);Check(combo.SelectedIndex==1&&changes==0,"horizontal wheel cannot change category");
    combo.DroppedDown=true;combo.NativeWheel(-120,0x020A);Check(combo.SelectedIndex==1&&changes==0,"wheel directed to the expanded combo cannot commit another category");combo.SelectedIndex=2;combo.DroppedDown=false;Check(combo.SelectedIndex==2&&changes==1,"explicit category selection is still available");form.Close();
   }
   var store=new Store(root);store.State.CorePath=Path.GetFullPath(args[0]);store.State.Port=28792;store.State.ApiPort=29092;var profile=new Subscription(){Name="Node UI regression"};store.State.Profiles.Add(profile);store.State.Active=profile.Id;
   var yaml=new StringBuilder("proxies:\n");for(int i=0;i<120;i++)yaml.Append("  - name: 节点 "+i.ToString("000")+"\n    type: ss\n    server: 127.0.0.1\n    port: 9\n    cipher: aes-128-gcm\n    password: example-only\n");yaml.Append("proxy-groups:\n  - name: 节点分类\n    type: select\n    proxies:\n");for(int i=0;i<120;i++)yaml.Append("      - 节点 "+i.ToString("000")+"\n");yaml.Append("rules:\n  - MATCH,节点分类\n");Store.Atomic(store.ProfilePath(profile),yaml.ToString());store.Save();
   using(var main=new MainForm(store,true)){
    var engine=Field<Engine>(main,"engine");cleanupEngine=engine;engine.Start(profile);main.Show();main.PreviewPage(2);Idle(main);var nodes=Field<ListView>(main,"nodes");var groups=Field<ComboBox>(main,"groups");groups.SelectedItem="节点分类";Pump(40);Check(nodes.Items.Count==120&&groups is NoWheelComboBox,"node page loads a long real-core group with wheel-safe category control");
    nodes.Items[105].Selected=true;nodes.Items[105].Focused=true;nodes.TopItem=nodes.Items[70];Pump(50);int top=nodes.TopItem.Index;var row=nodes.Items[105];Check(top>0,"test starts with the list scrolled away from the first row");
    ((SoftButton)Find(main,"使用选中节点")).PerformClick();Idle(main);var result=engine.Api("GET","/proxies/"+Uri.EscapeDataString("节点分类"),null);Check(result["now"].ToString()=="节点 105","use-selected action switches the real mihomo group");
    Check(nodes.TopItem.Index==top&&Object.ReferenceEquals(nodes.Items[105],row),"successful node switch keeps scroll position and existing row objects");Check(nodes.SelectedItems.Count==1&&nodes.SelectedItems[0]==row&&nodes.FocusedItem==row,"successful node switch preserves selected and focused node");Check(nodes.Items[105].SubItems[3].Text=="使用中"&&nodes.Items[0].SubItems[3].Text=="","only the old and new in-use markers change");
    nodes.Items[105].Selected=false;nodes.Items[90].Selected=true;nodes.Items[90].Focused=true;nodes.TopItem=nodes.Items[top];typeof(Control).GetMethod("OnDoubleClick",Flags).Invoke(nodes,new object[]{EventArgs.Empty});Idle(main);
    Check(nodes.TopItem.Index==top&&engine.Api("GET","/proxies/"+Uri.EscapeDataString("节点分类"),null)["now"].ToString()=="节点 090","double-click node switching also preserves scrolling");
    using(var delays=new DelayController(engine.Api("GET","/proxies",null))){typeof(Engine).GetField("apiPort",Flags).SetValue(engine,29093);row=nodes.Items[90];top=nodes.TopItem.Index;int selectedChanges=0;groups.SelectedIndexChanged+=delegate{selectedChanges++;};
     ((SoftButton)Find(main,"测试选中")).PerformClick();Idle(main);Check(delays.SingleTests==1&&delays.Refreshes==1&&nodes.Items[90].SubItems[2].Text=="87 ms","single latency button updates its result through the actual controller / refresh path");Check(nodes.TopItem.Index==top&&Object.ReferenceEquals(nodes.Items[90],row)&&row.Selected&&row.Focused,"single latency completion keeps scrolling, row identity, selection and focus");Check((groups.SelectedItem as string)=="节点分类"&&selectedChanges==0,"latency refresh does not clear or reselect an unchanged category");
     ((SoftButton)Find(main,"测试整组")).PerformClick();Idle(main);Check(delays.GroupTests==1&&delays.Refreshes==2&&row.SubItems[2].Text=="190 ms"&&nodes.Items[80].SubItems[2].Text=="超时","whole-group test updates all results including timeout markers");Check(nodes.TopItem.Index==top&&Object.ReferenceEquals(nodes.Items[90],row)&&row.Selected&&row.Focused,"whole-group latency completion retains the existing viewport and selected row");
     ((SoftButton)Find(main,"刷新")).PerformClick();Idle(main);Check(nodes.TopItem.Index==top&&row.Selected&&Object.ReferenceEquals(nodes.Items[90],row),"manual refresh also preserves the viewport and row objects");
     var search=Field<TextBox>(main,"search");search.Text="节点 0";Pump(40);nodes.Items[75].Selected=true;nodes.Items[75].Focused=true;nodes.TopItem=nodes.Items[50];row=nodes.Items[75];top=nodes.TopItem.Index;((SoftButton)Find(main,"测试选中")).PerformClick();Idle(main);Check(search.Text=="节点 0"&&nodes.Items.Count==100&&nodes.TopItem.Index==top&&row.Selected&&row.Focused&&Object.ReferenceEquals(nodes.Items[75],row)&&row.SubItems[2].Text=="87 ms","latency testing preserves an active search filter and its scrolled selection");
     // A refreshed configuration may genuinely add / remove group members.
     // That uncommon path rebuilds, then restores surviving names and position.
     var data=Field<Dictionary<string,object>>(main,"proxyData");var group=(Dictionary<string,object>)data["节点分类"];var all=((System.Collections.IEnumerable)group["all"]).Cast<object>().Where(x=>x.ToString()!="节点 010").ToArray();group["all"]=all;typeof(MainForm).GetMethod("FillGroups",Flags).Invoke(main,null);Pump(40);Check(nodes.Items.Count==99&&nodes.TopItem.Text=="节点 050"&&nodes.SelectedItems[0].Text=="节点 075"&&nodes.FocusedItem.Text=="节点 075","membership changes restore surviving viewport, selection and focus by name");
    }
    main.Close();
   }
   Console.WriteLine("Completed "+count+" node UI checks.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(cleanupEngine!=null)cleanupEngine.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
