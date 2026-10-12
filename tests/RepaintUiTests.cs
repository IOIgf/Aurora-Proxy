using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AuroraProxy;

// This isolates parent-paint replay and native spinner clipping. It does not
// claim to reproduce Windows' full Aero/Classic transparent HWND pipeline.
class RepaintUiTests {
 class ParentProbe:Panel {
  public int Paints;
  protected override void OnPaint(PaintEventArgs e){Paints++;using(var b=new SolidBrush(Color.Magenta))e.Graphics.FillRectangle(b,e.ClipRectangle);TextRenderer.DrawText(e.Graphics,"05 设置 / unrelated text",Font,e.ClipRectangle,Color.Black);}
 }
 class InputProbe:InputFrame {
  public InputProbe(Control editor):base(editor){}
  public void Background(Graphics g,Rectangle clip){OnPaintBackground(new PaintEventArgs(g,clip));}
 }
 class CheckProbe:SoftCheckBox {
  public void Background(Graphics g,Rectangle clip){OnPaintBackground(new PaintEventArgs(g,clip));}
 }
 static int count;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static void Pump(){var until=DateTime.UtcNow.AddMilliseconds(160);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(4);}}
 static void Background(Control c,Action<Graphics,Rectangle> paint,ParentProbe parent,string label){
  using(var image=new Bitmap(c.Width,c.Height))using(var g=Graphics.FromImage(image)){
   g.Clear(Color.Lime);parent.Paints=0;paint(g,c.ClientRectangle);
   Check(parent.Paints==0,label+": background does not replay ancestor drawing or unrelated labels");
   Check(image.GetPixel(0,0).ToArgb()==parent.BackColor.ToArgb()&&image.GetPixel(c.Width-1,c.Height-1).ToArgb()==parent.BackColor.ToArgb(),label+": the entire background uses its local opaque surface");
   var clip=new Rectangle(4,4,8,8);g.Clear(Color.Lime);g.SetClip(clip);paint(g,clip);
   Check(image.GetPixel(5,5).ToArgb()==parent.BackColor.ToArgb()&&image.GetPixel(0,0).ToArgb()==Color.Lime.ToArgb(),label+": partial repaint stays inside the invalidated area");
  }
 }
 static void NumericRegion(NumericUpDown number,string label){
  var text=number.Controls.Cast<Control>().OfType<TextBox>().First();var region=number.Region;
  Check(region!=null,label+": native numeric host has a clipping region");
  Check(region.IsVisible(text.Left+text.Width/2,text.Top+text.Height/2),label+": the native editable text remains visible");
  bool excludes=true;foreach(Control child in number.Controls)if(!(child is TextBox)&&child.Width>0&&child.Height>0)excludes&=!region.IsVisible(child.Left+child.Width/2,child.Top+child.Height/2);
  Check(excludes&&!region.IsVisible(number.Width-1,number.Height/2),label+": native spinner and its reserved square cannot show through");
 }
 [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Motion.Enabled=false;
  using(var form=new Form{ClientSize=new Size(620,340),Font=Theme.Font(9)}){
   var card=new Surface{Dock=DockStyle.Fill,Tone=PaletteTone.Blue};form.Controls.Add(card);
   var parent=new ParentProbe{Dock=DockStyle.Fill};card.Controls.Add(parent);
   var input=new InputProbe(new TextBox{Width=260,Text="本地输入"}){Location=new Point(100,25)};parent.Controls.Add(input);
   var box=new CheckProbe{Text="深色主题",AutoSize=true,Location=new Point(100,80)};parent.Controls.Add(box);
   var number=new NumericUpDown{Width=130,Minimum=1024,Maximum=65535,Value=7890};var numeric=Theme.Input(number);numeric.Location=new Point(100,140);parent.Controls.Add(numeric);
   form.Show();Pump();
   foreach(bool dark in new bool[]{true,false}){
    Theme.Dark=dark;Theme.Apply(form);Pump();string label=dark?"dark":"light";
    Background(input,input.Background,parent,label+" input");Background(box,box.Background,parent,label+" checkbox");
Check(input.BackColor.A==255&&box.BackColor.A==255,label+": field and checkbox disable transparent native background replay");
    Check(box.BackColor.ToArgb()==card.FillColor.ToArgb()&&input.BackColor.ToArgb()==card.FillColor.ToArgb(),label+": tinted card and child surfaces match");
    NumericRegion(number,label+" port");
   }
   var edit=number.Controls.Cast<Control>().OfType<TextBox>().First();edit.Focus();edit.SelectAll();edit.SelectedText="8080";typeof(NumericUpDown).GetMethod("ValidateEditText",flags).Invoke(number,null);Check(number.Value==8080,"clipped port editor still accepts and validates native keyboard text");
   number.UpButton();Check(number.Value==8081,"clipped port editor retains increment behavior");number.DownButton();Check(number.Value==8080,"clipped port editor retains decrement behavior");
   numeric.Scale(new SizeF(1.5f,1.5f));numeric.Font=Theme.Font(13.5f);Pump();NumericRegion(number,"enlarged port");
   typeof(Control).GetMethod("RecreateHandle",flags).Invoke(number,null);Pump();NumericRegion(number,"recreated port handle");
   var position=box.Bounds;for(int i=0;i<8;i++){box.Checked=!box.Checked;box.Invalidate();input.Invalidate();number.Enabled=i%2==0;form.Refresh();Pump();}
   Check(box.Bounds==position&&box.BackColor.A==255&&input.BackColor.A==255,"repeated checks, disabled states and repaints do not move or re-enable transparency");
   parent.BackColor=Color.FromArgb(30,60,90);Pump();Background(input,input.Background,parent,"changed parent input");Background(box,box.Background,parent,"changed parent checkbox");
   using(var image=new Bitmap(box.Width,box.Height)){parent.Paints=0;box.DrawToBitmap(image,box.ClientRectangle);Check(parent.Paints==0,"full checkbox paint does not import the parent's unrelated text");}
   form.Close();
  }Console.WriteLine("Completed "+count+" repaint checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
