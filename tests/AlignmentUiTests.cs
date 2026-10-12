using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using AuroraProxy;

// Runs on Windows at the current display DPI. On Mono, additionally exercises
// controlled 125/150/200% geometry + font enlargements (not native DPI emulation).
class AlignmentUiTests {
 static int count;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static void Pump(){var until=DateTime.UtcNow.AddMilliseconds(120);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(4);}}
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,flags).GetValue(owner);}
 static IEnumerable<Control> Tree(Control c){yield return c;foreach(Control child in c.Controls)foreach(var item in Tree(child))yield return item;}
 static void Enlarge(Form form,float factor){
  var fonts=Tree(form).Select(c=>new KeyValuePair<Control,Font>(c,c.Font)).ToArray();
  // Mono omits TableLayoutPanel.ScaleAbsoluteStyles. Capture the baseline
  // and apply the expected absolute-cell geometry once on either runtime.
  var rows=Tree(form).OfType<TableLayoutPanel>().SelectMany(t=>t.RowStyles.Cast<RowStyle>()).Where(r=>r.SizeType==SizeType.Absolute).Select(r=>new KeyValuePair<RowStyle,float>(r,r.Height)).ToArray();
  var columns=Tree(form).OfType<TableLayoutPanel>().SelectMany(t=>t.ColumnStyles.Cast<ColumnStyle>()).Where(c=>c.SizeType==SizeType.Absolute).Select(c=>new KeyValuePair<ColumnStyle,float>(c,c.Width)).ToArray();
  form.Scale(new SizeF(factor,factor));
  foreach(var pair in rows)pair.Key.Height=(float)Math.Round(pair.Value*factor);
  foreach(var pair in columns)pair.Key.Width=(float)Math.Round(pair.Value*factor);
  foreach(var pair in fonts)pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*factor,pair.Value.Style);
  form.PerformLayout();Pump();
 }
 static void Rows(MainForm main,string field,string label){
  Control input=Field<Control>(main,field);Control outer=input.Parent is InputFrame?input.Parent:input;var grid=(TableLayoutPanel)outer.Parent;
  bool centered=true,contained=true;int top=grid.Padding.Top;var heights=grid.GetRowHeights();
  for(int row=0;row<grid.RowCount-1&&row<heights.Length;row++){
   foreach(Control c in grid.Controls){if(grid.GetRow(c)!=row)continue;double middle=top+heights[row]/2.0;centered&=Math.Abs(c.Top+c.Height/2.0-middle)<=1.5;contained&=c.Top>=top&&c.Bottom<=top+heights[row];}
   top+=heights[row];
  }
  Check(centered,label+": labels, fields, checkboxes and action buttons share row centers");
  Check(contained,label+": controls stay inside their rows without vertical overlap");
  var boxes=grid.Controls.Cast<Control>().OfType<SoftCheckBox>().ToArray();Check(boxes.All(c=>c.Width>=c.GetPreferredSize(Size.Empty).Width&&c.Height>=c.GetPreferredSize(Size.Empty).Height),label+": checkbox label and focus-ring space are not cropped");
  foreach(var frame in grid.Controls.Cast<Control>().OfType<InputFrame>()){
   Control edit=frame.Editor;Control line=edit is NumericUpDown?edit.Controls.Cast<Control>().OfType<TextBox>().First():edit;
   int offset=edit is NumericUpDown?line.Top:0;int inset=InputFrame.TextLineTop(line);
   Check(Math.Abs(edit.Top+offset+inset+InputFrame.TextLineHeight(line)/2.0-frame.Height/2.0)<=1.5,label+": native font line centered for "+edit.GetType().Name);
   Check(edit.Left>=1&&edit.Right<frame.Width,label+": editor stays within its frame horizontally");
  }
 }
 [STAThread]static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-alignment-"+Guid.NewGuid().ToString("N"));try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Motion.Enabled=false;
  using(var host=new Form{Font=Theme.Font(9),ClientSize=new Size(600,300)}){
   var text=new TextBox{Text="中文 Ag 7890",Width=260};var input=Theme.Input(text,36);host.Controls.Add(input);
   var custom=new TextBox{Font=Theme.Font(10),Width=260,Location=new Point(0,70)};var explicitInput=Theme.Input(custom,36);host.Controls.Add(explicitInput);host.Show();Pump();
   Check(text.Font.Equals(host.Font)&&input.Font.Equals(host.Font),"unparented default editor inherits the page font instead of the system font");
   Check(custom.Font.SizeInPoints==10,"an explicit search-field font survives wrapping");
   host.Font=Theme.Font(13.5f);Pump();Check(text.Font.Equals(host.Font)&&custom.Font.SizeInPoints==10,"font inheritance follows parent changes without overwriting explicit fonts");
   Check(Math.Abs(text.Top+InputFrame.TextLineTop(text)+InputFrame.TextLineHeight(text)/2.0-input.Height/2.0)<=1.5,"larger native font recenters after FontChanged");
   input.Scale(new SizeF(1.5f,1.5f));Pump();Check(Math.Abs(text.Top+InputFrame.TextLineTop(text)+InputFrame.TextLineHeight(text)/2.0-input.Height/2.0)<=1.5,"scaling does not move the editor a second time after arranging it");host.Close();
  }
  foreach(float factor in new float[]{1,1.25f,1.5f,2})foreach(int page in new int[]{4,5}){
   using(var main=new MainForm(new Store(root),true)){main.Show();main.PreviewPage(page);Pump();if(factor!=1)Enlarge(main,factor);
    string label=(page==4?"settings":"LAN")+" "+factor.ToString("0.##")+"x";Rows(main,page==4?"corePath":"lanUser",label);
    var controls=Tree(main).Where(c=>c is InputFrame||c is SoftCheckBox).ToArray();var before=controls.Select(c=>c.Bounds).ToArray();Theme.Dark=!Theme.Dark;Theme.Apply(main);Pump();Check(controls.Select(c=>c.Bounds).SequenceEqual(before),label+": theme changes do not shift fields or checkboxes");
    if(factor==1.5f){string file=Path.Combine(Path.GetDirectoryName(Application.ExecutablePath),page==4?"alignment-settings-150.png":"alignment-lan-150.png");main.CapturePage(file,page);}main.Close();
   }
  }
  using(var main=new MainForm(new Store(root),true)){main.Show();Pump();main.PreviewPage(4);Pump();var core=Field<TextBox>(main,"corePath");var initial=core.Parent.Bounds;var grid=(TableLayoutPanel)core.Parent.Parent;float row=grid.RowStyles[0].Height;
   main.PreviewPage(5);Pump();main.PreviewPage(4);Pump();Check(Field<TextBox>(main,"corePath").Parent.Bounds==initial&&((TableLayoutPanel)Field<TextBox>(main,"corePath").Parent.Parent).RowStyles[0].Height==row,"returning to a dynamically built page preserves its field bounds and row scale");
   using(var g=main.CreateGraphics())Check(Math.Abs(row-34*g.DpiY/96f)<=1,"dynamic setting rows use the current display DPI exactly once");main.Close();
  }
  using(var dialog=new SubscriptionDialog(null,true)){dialog.Show();Pump();var name=Field<TextBox>(dialog,"name");var url=Field<TextBox>(dialog,"url");Check(name.Font.Equals(dialog.Font)&&url.Font.Equals(dialog.Font),"subscription fields inherit the dialog font");Enlarge(dialog,1.5f);var check=dialog.Controls.OfType<SoftCheckBox>().OrderBy(c=>c.Top).ToArray();Check(name.Parent.Bottom<url.Parent.Top&&url.Parent.Bottom<=check[0].Top&&check[0].Bottom<=check[1].Top,"enlarged subscription fields and checkboxes do not overlap");dialog.Close();}
  Console.WriteLine("Completed "+count+" alignment checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
}
