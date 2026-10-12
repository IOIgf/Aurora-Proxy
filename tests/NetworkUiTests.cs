using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AuroraProxy;

class NetworkUiTests {
 static int count;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static T Field<T>(MainForm main,string name){return (T)typeof(MainForm).GetField(name,flags).GetValue(main);}
 static Control Find(Control root,string text){if(root.Text==text)return root;foreach(Control child in root.Controls){var match=Find(child,text);if(match!=null)return match;}return null;}
 static Dictionary<string,object> Snapshot(int count,int traffic){var rows=new List<object>();for(int i=0;i<count;i++)rows.Add(new Dictionary<string,object>{{"id","connection-"+i},{"metadata",new Dictionary<string,object>{{"host","example"+i+".test"},{"destinationPort","443"},{"sourceIP","192.168.1.20"},{"sourcePort","12345"},{"network","tcp"},{"type","HTTP"}}},{"upload",traffic},{"download",2048},{"rule","Match"}});return new Dictionary<string,object>{{"connections",rows.ToArray()}};}
 [STAThread] static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-network-ui-"+Guid.NewGuid().ToString("N"));
  try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var store=new Store(root);using(var main=new MainForm(store,true)){
   main.Show();main.PreviewPage(5);Application.DoEvents();var enabled=Field<CheckBox>(main,"lanEnabled");var auth=Field<CheckBox>(main,"lanAuth");var user=Field<TextBox>(main,"lanUser");var pass=Field<TextBox>(main,"lanPassword");Check(!enabled.Checked&&auth.Checked&&pass.UseSystemPasswordChar,"LAN page defaults to sharing off with a masked authentication password");enabled.Checked=true;Check(auth.Enabled&&user.Enabled&&pass.Enabled,"enabling sharing unlocks authentication fields");auth.Checked=false;Check(!user.Enabled&&!pass.Enabled,"no-auth mode disables unnecessary credentials");auth.Checked=true;user.Text="ui_user";pass.Text="ui-test-password";((SoftButton)Find(main,"保存共享设置")).PerformClick();var loaded=new Store(root);Check(loaded.State.AllowLan&&loaded.State.LanUser=="ui_user"&&loaded.State.LanPassword=="ui-test-password","save-sharing button persists all configured fields");Check(Field<ComboBox>(main,"lanAddressBox") is NoWheelComboBox,"adapter selection also ignores accidental wheel changes");
   main.PreviewPage(6);Application.DoEvents();typeof(MainForm).GetMethod("FillConnections",flags).Invoke(main,new object[]{Snapshot(80,1024)});var list=Field<ListView>(main,"connections");Check(list.Items.Count==80&&list.Items[0].SubItems[1].Text.StartsWith("192.168.1.20:"),"connection page renders source-device addresses and all active entries");list.Items[60].Selected=true;list.TopItem=list.Items[50];Application.DoEvents();int top=list.TopItem.Index;var row=list.Items[60];typeof(MainForm).GetMethod("FillConnections",flags).Invoke(main,new object[]{Snapshot(80,4096)});Check(list.TopItem.Index==top&&Object.ReferenceEquals(list.Items[60],row)&&row.Selected,"periodic connection updates retain scrolling, row identity and selection");Check(row.SubItems[4].Text.StartsWith("4.0 KB"),"connection traffic totals update in place");typeof(MainForm).GetMethod("FillConnections",flags).Invoke(main,new object[]{Snapshot(0,0)});Check(list.Items.Count==0,"closed connections disappear without stale rows");
   main.PreviewPage(4);Application.DoEvents();Check(!Field<CheckBox>(main,"startup").Checked,"startup remains opt-in for existing users");main.ClientSize=new Size(960,630);Application.DoEvents();var nav=Field<SoftButton[]>(main,"nav");Check(nav.Length==7&&nav[6].Bottom<=nav[6].Parent.ClientSize.Height,"all seven navigation entries fit the minimum window");main.Close();
  }Console.WriteLine("Completed "+count+" network UI checks.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
