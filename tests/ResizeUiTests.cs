using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AuroraProxy;
class ResizeUiTests {
 [STAThread] static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-resize-"+Guid.NewGuid().ToString("N"));try{Application.EnableVisualStyles();using(var main=new MainForm(new Store(root),true)){main.Show();var content=(Control)typeof(MainForm).GetField("content",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);for(int i=0;i<28;i++){main.PreviewPage(i%7);main.ClientSize=new Size(1050+i%4*45,760+i%3*35);Application.DoEvents();if(content.Controls.Count!=1||content.Controls[0].Dock!=DockStyle.Fill||content.Controls[0].Left!=0||content.Controls[0].Width!=content.ClientSize.Width)throw new Exception("page lost docking during resize: "+i);using(var b=new Bitmap(main.Width,main.Height))main.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));}Console.WriteLine("PASS: 28 page switches and resizes retain docking and complete rendering");main.Close();}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
}
