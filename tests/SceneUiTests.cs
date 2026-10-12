using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using AuroraProxy;

// Controlled repaint regression, not a Windows HWND/GDI acceptance test.
class SceneUiTests {
 class ParentProbe:Panel {
  public int ForegroundPaints;
  protected override void OnPaint(PaintEventArgs e){ForegroundPaints++;using(var brush=new SolidBrush(Color.Magenta))e.Graphics.FillRectangle(brush,e.ClipRectangle);TextRenderer.DrawText(e.Graphics,"05 设置 / unrelated sibling",Font,e.ClipRectangle,Color.Black);base.OnPaint(e);}
 }
 class ButtonProbe:SoftButton {
  public bool TransparentStyle {get{return GetStyle(ControlStyles.SupportsTransparentBackColor);}}
  public void Background(Graphics g,Rectangle clip){OnPaintBackground(new PaintEventArgs(g,clip));}
  public void Foreground(Graphics g,Rectangle clip){OnPaint(new PaintEventArgs(g,clip));}
 }
 class SurfaceProbe:Surface {
  public int ForegroundPaints;
  public bool TransparentStyle {get{return GetStyle(ControlStyles.SupportsTransparentBackColor);}}
  public void Background(Graphics g,Rectangle clip){OnPaintBackground(new PaintEventArgs(g,clip));}
  protected override void OnPaint(PaintEventArgs e){ForegroundPaints++;base.OnPaint(e);using(var brush=new SolidBrush(Color.Magenta))e.Graphics.FillRectangle(brush,40,40,50,30);}
 }
 static int count;
 static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);count++;Console.WriteLine("PASS: "+text);}
 static void Background(Control control,Action<Graphics,Rectangle> paint,ParentProbe parent,string label){
  using(var bitmap=new Bitmap(control.Width,control.Height))using(var g=Graphics.FromImage(bitmap)){
   g.Clear(Color.Lime);parent.ForegroundPaints=0;paint(g,control.ClientRectangle);
   Check(parent.ForegroundPaints==0,label+": background does not replay ancestor foreground");
   Check(bitmap.GetPixel(0,0).ToArgb()==parent.BackColor.ToArgb()&&bitmap.GetPixel(control.Width-1,control.Height-1).ToArgb()==parent.BackColor.ToArgb(),label+": background uses parent color at the correct coordinates");
   var clip=new Rectangle(5,5,10,10);g.Clear(Color.Lime);g.SetClip(clip);paint(g,clip);
   Check(bitmap.GetPixel(8,8).ToArgb()==parent.BackColor.ToArgb()&&bitmap.GetPixel(0,0).ToArgb()==Color.Lime.ToArgb(),label+": partial background invalidation remains clipped");
  }
 }
 [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Motion.Enabled=false;
  using(var form=new Form{ClientSize=new Size(600,400)}){
   var parent=new ParentProbe{Dock=DockStyle.Fill};form.Controls.Add(parent);
   var button=new ButtonProbe{Location=new Point(70,30),Size=new Size(170,44),Text="05 设置",Navigation=true};parent.Controls.Add(button);
   var surface=new SurfaceProbe{Location=new Point(70,110),Size=new Size(400,240),Tone=PaletteTone.Blue};parent.Controls.Add(surface);
   var nested=new ButtonProbe{Location=new Point(45,45),Size=new Size(150,38),Text="保留配色"};surface.Controls.Add(nested);
   form.Show();Application.DoEvents();
   foreach(bool dark in new bool[]{true,false}){
    Theme.Dark=dark;Theme.Apply(form);Application.DoEvents();string label=dark?"dark":"light";
    Background(button,button.Background,parent,label+" button");Background(surface,surface.Background,parent,label+" surface");
    Check(button.BackColor.A==255&&!button.TransparentStyle&&surface.BackColor.A==255&&!surface.TransparentStyle,label+": button and card disable native transparency");
    using(var bitmap=new Bitmap(nested.Width,nested.Height))using(var g=Graphics.FromImage(bitmap)){
     g.Clear(Color.Lime);surface.ForegroundPaints=0;parent.ForegroundPaints=0;nested.Background(g,nested.ClientRectangle);
     Check(surface.ForegroundPaints==0&&parent.ForegroundPaints==0,label+": nested button paints only card background geometry");
     Check(bitmap.GetPixel(25,20).ToArgb()==surface.FillColor.ToArgb(),label+": card tint survives nested background paint without importing labels");
    }
    using(var bitmap=new Bitmap(300,100))using(var g=Graphics.FromImage(bitmap)){
     g.Clear(Color.Lime);g.TranslateTransform(30,20);g.SetClip(new Rectangle(0,0,button.Width,button.Height));var beforeClip=g.ClipBounds;var beforeTransform=g.Transform.Elements;
     button.Background(g,button.ClientRectangle);button.Foreground(g,button.ClientRectangle);
     Check(g.ClipBounds==beforeClip&&g.Transform.Elements[4]==beforeTransform[4]&&g.Transform.Elements[5]==beforeTransform[5],label+": paint restores caller transform and clip");
     Check(bitmap.GetPixel(0,0).ToArgb()==Color.Lime.ToArgb()&&bitmap.GetPixel(250,80).ToArgb()==Color.Lime.ToArgb(),label+": navigation redraw cannot write into neighboring controls");
    }
   }
   // Reflection allows the same executable to first expose the old ancestor
   // replay failure, without depending on the new background interface.
   var draw=typeof(Theme).GetMethod("DrawText",BindingFlags.Static|BindingFlags.Public);
   Check(draw!=null,"shared bounded text renderer is present");
   using(var bitmap=new Bitmap(240,100))using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Arial",36)){
    g.Clear(Color.Lime);g.TranslateTransform(20,10);var bounds=new Rectangle(10,10,35,20);g.SetClip(new Rectangle(0,0,190,70));var before=g.ClipBounds;
    draw.Invoke(null,new object[]{g,"05 设置 long label",font,bounds,Color.Black,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding});
    bool outsideUntouched=true;var allowed=new Rectangle(30,20,35,20);for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(!allowed.Contains(x,y)&&bitmap.GetPixel(x,y).ToArgb()!=Color.Lime.ToArgb())outsideUntouched=false;
    Check(outsideUntouched,"oversized text is bounded even on translated graphics");
    Check(g.ClipBounds==before&&g.Transform.Elements[4]==20&&g.Transform.Elements[5]==10,"bounded text restores graphics state");
   }
   form.Close();
  }Console.WriteLine("Completed "+count+" scene repaint checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
