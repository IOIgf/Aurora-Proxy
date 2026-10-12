using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AuroraProxy;

class ProbeButton:SoftButton {
 public void HoverEnter(){OnMouseEnter(EventArgs.Empty);}
 public void HoverLeave(){OnMouseLeave(EventArgs.Empty);}
 public void Down(){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,Width/2,Height/2,0));}
 public void Up(){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,Width/2,Height/2,0));}
 public void UpOutside(){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,-4,-4,0));}
 public void EnterKey(){ProcessDialogKey(Keys.Enter);}
 public void EscapeKey(){ProcessDialogKey(Keys.Escape);}
 public void Mnemonic(char c){ProcessMnemonic(c);}
 public void PaintRegion(Bitmap bitmap,Rectangle clip){using(var g=Graphics.FromImage(bitmap)){g.SetClip(clip);var e=new PaintEventArgs(g,clip);OnPaintBackground(e);OnPaint(e);}}
 public void KeyDownSpace(){OnKeyDown(new KeyEventArgs(Keys.Space));}
 public void KeyUpSpace(){OnKeyUp(new KeyEventArgs(Keys.Space));}
}
class ProbeForm:Form {public void Accept(){ProcessDialogKey(Keys.Enter);}public void Cancel(){ProcessDialogKey(Keys.Escape);}}
class GradientPanel:Panel,IBackgroundLayer {
 public void PaintBackgroundLayer(Graphics graphics){using(var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(45,65,115),Color.FromArgb(125,65,75),0f))graphics.FillRectangle(b,ClientRectangle);}
 protected override void OnPaint(PaintEventArgs e){PaintBackgroundLayer(e.Graphics);base.OnPaint(e);}
}
class UiSmokeTests {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
 static void Pump(int milliseconds){var s=Stopwatch.StartNew();while(s.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
 static Color Pixel(Control c,int x,int y){using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));return b.GetPixel(x,y);}}
 static bool Ticking(object c){return ((System.Windows.Forms.Timer)c.GetType().BaseType.GetField("animation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c)).Enabled;}
 static bool Corners(ProbeButton b,Bitmap parent){using(var image=new Bitmap(b.Width,b.Height)){b.DrawToBitmap(image,b.ClientRectangle);foreach(var p in new Point[]{Point.Empty,new Point(b.Width-1,0),new Point(0,b.Height-1),new Point(b.Width-1,b.Height-1),new Point(b.Width-1,b.Height/2)})if(image.GetPixel(p.X,p.Y).ToArgb()!=parent.GetPixel(b.Left+p.X,b.Top+p.Y).ToArgb())return false;return true;}}
 [STAThread] static int Main(string[] args){
  try{if(args.Length==0||args[0]!="--classic")Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var f=new ProbeForm(){ClientSize=new Size(550,420),BackColor=Color.FromArgb(70,30,90)}){
   var b=new ProbeButton(){Text="启动内核",Primary=true,Location=new Point(20,20),Size=new Size(136,42)};f.Controls.Add(b);f.Show();Pump(80);
   Check(Pixel(b,0,0).ToArgb()==f.BackColor.ToArgb(),"rounded button corner uses parent background, without native square fill");
   int clicked=0;b.Click+=delegate{clicked++;};Color normal=Pixel(b,20,12);b.HoverEnter();Pump(350);Color hover=Pixel(b,20,12);Check(normal.ToArgb()!=hover.ToArgb(),"primary hover produces visible color feedback");Check(!Ticking(b),"hover transition timer stops once settled");
   b.Down();Pump(150);Color pressed=Pixel(b,20,12);Check(pressed.ToArgb()!=hover.ToArgb(),"mouse press produces a distinct pressed appearance");b.Up();b.HoverLeave();Pump(350);Check(!Ticking(b),"release and mouse-leave settle without an idle timer");
   b.Focus();int old=clicked;b.KeyDownSpace();Pump(100);b.KeyUpSpace();Pump(300);Check(clicked==old+1,"Space activates button exactly once");
   f.AcceptButton=b;old=clicked;f.Accept();Check(clicked==old+1,"default button still supports Enter");
   f.AcceptButton=null;b.Focus();old=clicked;b.EnterKey();Check(clicked==old+1,"focused button handles Enter without a form default");
   b.Down();old=clicked;b.Up();Check(clicked==old+1,"mouse release inside fires exactly once");b.Down();old=clicked;b.UpOutside();Check(clicked==old,"mouse release outside cancels the click");
   b.Down();old=clicked;b.Capture=false;b.Up();Check(clicked==old,"lost mouse capture cancels activation");
   b.Focus();b.KeyDownSpace();old=clicked;b.EscapeKey();b.KeyUpSpace();Check(clicked==old,"Escape cancels an outstanding Space press");
   b.Text="启动内核(&S)";old=clicked;b.Mnemonic('s');Check(clicked==old+1,"Alt mnemonic retains button activation");b.Text="启动内核";
   old=clicked;b.AccessibilityObject.DoDefaultAction();Check(clicked==old+1&&b.AccessibilityObject.Role==AccessibleRole.PushButton,"accessible button exposes an actionable push-button role");
   b.Enabled=false;old=clicked;b.PerformClick();Check(clicked==old,"disabled button cannot trigger its action");Check(Pixel(b,20,12).ToArgb()!=normal.ToArgb(),"disabled primary button no longer retains enabled accent fill");b.Enabled=true;
   f.Enabled=false;Check(!b.Enabled,"parent busy state disables nested buttons");f.Enabled=true;Check(b.Enabled,"button becomes enabled again after busy state ends");
   Motion.Enabled=false;b.HoverEnter();Check(!Ticking(b),"reduced-motion preference responds immediately without a timer");b.HoverLeave();Motion.Enabled=true;
   Theme.Dark=false;Theme.Apply(f);Check(Pixel(b,0,0).ToArgb()==f.BackColor.ToArgb(),"light theme redraw keeps transparent corners");Theme.Dark=true;Theme.Apply(f);
   Check(!typeof(ButtonBase).IsAssignableFrom(typeof(SoftButton)),"custom button no longer uses native ButtonBase painting");
   using(var gradient=new GradientPanel(){Location=new Point(20,210),Size=new Size(490,120)}){
    f.Controls.Add(gradient);using(var parent=new Bitmap(gradient.Width,gradient.Height)){
     gradient.DrawToBitmap(parent,gradient.ClientRectangle);var nested=new ProbeButton(){Text="背景检查",Location=new Point(35,24),Size=new Size(145,44),Primary=true};gradient.Controls.Add(nested);Pump(50);
     Check(Corners(nested,parent),"all corners match a painted parent background at the child offset");nested.HoverEnter();nested.Down();Pump(160);Check(Corners(nested,parent),"pressed repaint does not overwrite painted-parent corners");nested.Up();nested.Enabled=false;Pump(300);Check(Corners(nested,parent),"disabled repaint does not add a square system fill");nested.Enabled=true;nested.HoverLeave();nested.Location=new Point(220,52);nested.Size=new Size(180,50);Pump(300);Check(Corners(nested,parent),"moving and resizing rebuild the actual underlying background");
     using(var partial=new Bitmap(nested.Width,nested.Height)){using(var g=Graphics.FromImage(partial))g.Clear(Color.Magenta);nested.PaintRegion(partial,new Rectangle(0,0,12,12));Check(partial.GetPixel(0,0).ToArgb()==parent.GetPixel(nested.Left,nested.Top).ToArgb()&&partial.GetPixel(20,20).ToArgb()==Color.Magenta.ToArgb(),"partial invalidation repaints only the requested area without stale corner pixels");}
    }
   }
   using(var card=new Surface(){Location=new Point(20,210),Size=new Size(490,120)}){f.Controls.Add(card);using(var parent=new Bitmap(card.Width,card.Height)){card.DrawToBitmap(parent,card.ClientRectangle);var nested=new ProbeButton(){Text="圆角卡片",Location=new Point(2,2),Size=new Size(145,44)};card.Controls.Add(nested);Check(Corners(nested,parent),"button background preserves the rounded Surface edge rather than a flat card rectangle");}}
   using(var dialog=new ProbeForm()){var ok=new ProbeButton(){DialogResult=DialogResult.OK};dialog.Controls.Add(ok);dialog.AcceptButton=ok;dialog.Show();old=0;ok.Click+=delegate{old++;};dialog.Accept();Check(dialog.DialogResult==DialogResult.OK&&old==1,"default dialog button sets OK and activates once");dialog.Close();}
   using(var dialog=new ProbeForm()){var cancel=new ProbeButton(){DialogResult=DialogResult.Cancel};dialog.Controls.Add(cancel);dialog.CancelButton=cancel;dialog.Show();dialog.Cancel();Check(dialog.DialogResult==DialogResult.Cancel,"Escape activates the dialog cancel button");dialog.Close();}
   var host=new TransitionPanel(){Location=new Point(190,20),Size=new Size(280,130)};var body=new Panel(){Dock=DockStyle.Fill};host.Controls.Add(body);f.Controls.Add(host);host.AnimateIn();Check(body.Left>0,"page transition starts with a small offset");Pump(260);Check(body.Left==0&&body.Dock==DockStyle.Fill,"page transition restores normal docking after settling");host.AnimateIn();host.Size=new Size(270,120);Check(body.Dock==DockStyle.Fill,"resizing cancels transition and restores layout");
   b.HoverEnter();var t=(System.Windows.Forms.Timer)typeof(SoftButton).GetField("animation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(b);b.Dispose();Check(!t.Enabled,"disposing a button also stops its animation timer");host.Dispose();f.Close();
  }
  // Mono's managed MessageBox is enumerable; the real Windows native dialog is not.
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT){string root=Path.Combine(Path.GetTempPath(),"aurora-ui-test-"+Guid.NewGuid().ToString("N"));try{using(var main=new MainForm(new Store(root),true))using(var dismiss=new System.Windows.Forms.Timer(){Interval=25}){
   main.Show();Pump(80);bool successCalled=false;dismiss.Tick+=delegate{foreach(Form dialog in Application.OpenForms.Cast<Form>().ToArray())if(dialog.Modal&&!Object.ReferenceEquals(dialog,main))dialog.Close();};dismiss.Start();
   typeof(MainForm).GetMethod("Run",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{(Action)delegate{throw new IOException("Expected failed subscription operation");},(Action)delegate{successCalled=true;},"正在测试失败回退…"});Pump(700);
   Check(!successCalled,"failed operation does not invoke its success callback");
   var content=(Control)typeof(MainForm).GetField("content",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);Check(content.Enabled,"failed operation re-enables content controls");
   var nav=(SoftButton[])typeof(MainForm).GetField("nav",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);Check(nav.All(x=>x.Enabled),"failed operation restores navigation buttons");
   var status=(ActivityIndicator)typeof(MainForm).GetField("activity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);Check(!status.Busy,"failed operation stops the busy indicator");dismiss.Stop();main.Close();
  }}finally{Directory.Delete(root,true);}}
  Console.WriteLine("Completed "+count+" UI checks.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}
