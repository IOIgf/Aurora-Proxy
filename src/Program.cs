using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("Aurora Proxy")]
[assembly: AssemblyDescription("A Windows 7 compatible mihomo desktop client")]
[assembly: AssemblyVersion("1.1.9.0")]
[assembly: AssemblyFileVersion("1.1.9.0")]
namespace AuroraProxy {
 static class Program {
  [STAThread] static void Main(string[] args){
   // TLS 1.2 enum value exists in patched .NET 4.0 although the named constant is newer.
   try{ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072|(SecurityProtocolType)768;}catch{}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool preview=args.Length>0&&args[0]=="--preview";string root=preview?Path.Combine(Path.GetTempPath(),"AuroraPreview"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AuroraProxy");
   bool first;using(var mutex=new Mutex(true,preview?"AuroraProxy.Preview":"Local\\AuroraProxy.Desktop",out first)){
    if(!first){MessageBox.Show("Aurora Proxy 已在运行，请检查系统托盘。","Aurora Proxy");return;}
    try{var store=new Store(root);if(preview){store.State.Profiles.Clear();var sample=new Subscription(){Name="示例订阅 · 仅展示界面",Updated=DateTime.UtcNow.ToString("o"),Usage="8.4 GB / 100 GB"};store.State.Profiles.Add(sample);store.State.Active=sample.Id;if(args.Length>3)store.State.Dark=args[3]!="light";}
     if(!preview&&store.State.StartWithWindows&&SystemProxy.Windows){try{Startup.SetEnabled(true,Application.ExecutablePath);}catch{}}
     using(var form=new MainForm(store,preview,Array.IndexOf(args,"--tray")>=0)){if(preview&&args.Length>1){int page=0;if(args.Length>2)Int32.TryParse(args[2],out page);form.PreviewPage(page);form.Shown+=delegate{var delay=new System.Windows.Forms.Timer(){Interval=600};delay.Tick+=delegate{delay.Stop();delay.Dispose();form.CapturePage(args[1],page);form.Close();};delay.Start();};}Application.Run(form);}
    }catch(Exception ex){if(preview){Console.Error.WriteLine(ex);Environment.ExitCode=1;}else MessageBox.Show(ex.Message,"Aurora Proxy 启动失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
 }
}
