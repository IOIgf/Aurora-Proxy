using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using AuroraProxy;

// Model the documented Opaque paint dispatch: foreground runs without a
// separate background pass. This tests buffer initialization, not Win7 itself.
class CheckBoxFrameTests {
 class ParentProbe:Panel {
  public int ForegroundPaints;
  protected override void OnPaint(PaintEventArgs e){ForegroundPaints++;using(var brush=new SolidBrush(Color.Magenta))e.Graphics.FillRectangle(brush,e.ClipRectangle);base.OnPaint(e);}
 }
 class Probe:SoftCheckBox {
  public bool OpaquePaint {get{return GetStyle(ControlStyles.Opaque);}}
  public void Frame(Graphics g,Rectangle clip){if(!OpaquePaint)OnPaintBackground(new PaintEventArgs(g,clip));OnPaint(new PaintEventArgs(g,clip));}
  public void BackgroundAndFrame(Graphics g,Rectangle clip){OnPaintBackground(new PaintEventArgs(g,clip));OnPaint(new PaintEventArgs(g,clip));}
  public void HoverIn(){OnMouseEnter(EventArgs.Empty);}
  public void HoverOut(){OnMouseLeave(EventArgs.Empty);}
  public void Press(){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,8,Height/2,0));}
  public void CancelPress(){OnLostFocus(EventArgs.Empty);}
  public void Recreate(){RecreateHandle();}
 }
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);count++;Console.WriteLine("PASS: "+message);}
 static bool Equal(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())return false;return true;}
 static void DirtyFrame(Probe box,ParentProbe parent,string label){
  using(var expected=new Bitmap(box.Width,box.Height))using(var actual=new Bitmap(box.Width,box.Height)){
   using(var g=Graphics.FromImage(expected)){g.Clear(Theme.BackgroundBehind(box));box.BackgroundAndFrame(g,box.ClientRectangle);}
   using(var g=Graphics.FromImage(actual)){
    g.Clear(Color.Magenta);using(var font=new Font("Arial",12))TextRenderer.DrawText(g,"05 设置 / previous sibling label",font,box.ClientRectangle,Color.Black);
    parent.ForegroundPaints=0;box.Frame(g,box.ClientRectangle);
   }
   Check(Equal(expected,actual),label+": opaque foreground pass clears stale buffer pixels without a background callback");
   Check(parent.ForegroundPaints==0,label+": clearing does not invoke ancestor foreground painting");
  }
 }
 [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Motion.Enabled=false;
  using(var form=new Form{ClientSize=new Size(600,240),Font=Theme.Font(9)}){
   var surface=new Surface{Dock=DockStyle.Fill,Tone=PaletteTone.Blue};form.Controls.Add(surface);var parent=new ParentProbe{Dock=DockStyle.Fill};surface.Controls.Add(parent);
   var box=new Probe{Location=new Point(70,30),Size=new Size(370,30),Text="启用轻量动效"};parent.Controls.Add(box);form.Show();Application.DoEvents();
   Check(box.OpaquePaint,"test follows the inherited opaque paint contract");
   foreach(bool dark in new bool[]{true,false}){
    Theme.Dark=dark;Theme.Apply(form);Application.DoEvents();string label=dark?"dark":"light";
    box.ThreeState=true;foreach(CheckState state in new CheckState[]{CheckState.Unchecked,CheckState.Checked,CheckState.Indeterminate}){box.CheckState=state;DirtyFrame(box,parent,label+" "+state);}
    box.Enabled=false;DirtyFrame(box,parent,label+" disabled");box.Enabled=true;
    box.HoverIn();DirtyFrame(box,parent,label+" hover");box.Press();DirtyFrame(box,parent,label+" pressed");box.CancelPress();box.HoverOut();
    box.Text="短标签";box.Checked=false;DirtyFrame(box,parent,label+" shorter label and unchecked state");
    using(var image=new Bitmap(box.Width,box.Height))using(var g=Graphics.FromImage(image)){
     g.Clear(Color.Magenta);var clip=new Rectangle(box.Width-12,box.Height-12,8,8);box.Frame(g,clip);
     Check(image.GetPixel(box.Width-8,box.Height-8).ToArgb()==parent.BackColor.ToArgb(),label+": partial foreground repaint initializes its invalidated background");
     Check(image.GetPixel(0,0).ToArgb()==Color.Magenta.ToArgb(),label+": partial foreground repaint preserves pixels outside invalidation");
    }
    using(var image=new Bitmap(500,100))using(var g=Graphics.FromImage(image)){
     g.Clear(Color.Lime);g.TranslateTransform(20,10);g.SetClip(box.ClientRectangle);g.SmoothingMode=SmoothingMode.None;var clip=g.ClipBounds;
     box.Frame(g,box.ClientRectangle);
     Check(g.ClipBounds==clip&&g.Transform.Elements[4]==20&&g.Transform.Elements[5]==10&&g.SmoothingMode==SmoothingMode.None,label+": complete checkbox frame restores graphics clip, transform and smoothing");
     Check(image.GetPixel(0,0).ToArgb()==Color.Lime.ToArgb()&&image.GetPixel(450,70).ToArgb()==Color.Lime.ToArgb(),label+": complete frame cannot overwrite adjacent controls");
    }
   }
   box.Recreate();Application.DoEvents();DirtyFrame(box,parent,"recreated handle");
   box.Scale(new SizeF(1.5f,1.5f));box.Font=Theme.Font(13.5f);Application.DoEvents();DirtyFrame(box,parent,"enlarged geometry");
   parent.BackColor=Color.FromArgb(30,60,90);Application.DoEvents();DirtyFrame(box,parent,"changed parent background");
   // Capture only the new control rendering for local visual inspection.
   using(var image=new Bitmap(box.Width,box.Height))using(var g=Graphics.FromImage(image)){g.Clear(Color.Magenta);box.Frame(g,box.ClientRectangle);image.Save(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath),"checkbox-complete-frame.png"));}
   form.Close();
  }Console.WriteLine("Completed "+count+" checkbox frame checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
