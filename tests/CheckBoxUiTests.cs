using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AuroraProxy;

class CheckBoxUiTests {
 class Probe:SoftCheckBox {
  public void ClickIt(){OnClick(EventArgs.Empty);}
  public void Space(){OnKeyDown(new KeyEventArgs(Keys.Space));OnKeyUp(new KeyEventArgs(Keys.Space));}
  public bool Mnemonic(char key){return ProcessMnemonic(key);}
  public void MouseMessage(int message,int x,int y){var m=Message.Create(Handle,message,(IntPtr)(message==0x201?1:0),(IntPtr)((y<<16)|x));WndProc(ref m);}
 }
 static int count;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static void Pump(){var end=DateTime.UtcNow.AddMilliseconds(350);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 static Color Pixel(Control c,int x,int y){using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);return b.GetPixel(x,y);}}
 [STAThread] static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Theme.Dark=true;Motion.Enabled=true;
  using(var form=new Form(){ClientSize=new Size(620,200),Font=Theme.Font(9)}){var card=new Surface(){Dock=DockStyle.Fill};form.Controls.Add(card);var box=new Probe(){Text="启用轻量动效",AutoSize=true,Location=new Point(28,28)};card.Controls.Add(box);var other=new TextBox(){Location=new Point(28,90),Width=200};card.Controls.Add(Theme.Input(other));Theme.Apply(form);form.Show();Pump();
   Check(box.Height>=26&&box.Width>100,"custom checkbox reserves glyph, text and focus-ring spacing");Check(Pixel(box,0,0).ToArgb()==Theme.Card.ToArgb(),"checkbox background follows the actual card without a system-colored square");var idle=Pixel(box,10,box.Height/2);int events=0;box.CheckedChanged+=delegate{events++;};box.ClickIt();Pump();Check(box.Checked&&events==1,"native click changes checked state and raises one change event");Check(Pixel(box,10,box.Height/2).ToArgb()!=idle.ToArgb(),"checked state renders the custom accent-filled glyph");
   box.Focus();box.Space();Pump();Check(!box.Checked&&events==2,"Space toggles exactly once");box.Text="&Enable animations";Check(box.Mnemonic('e'),"native Alt mnemonic remains available");Pump();Check(box.Checked&&events==3,"mnemonic toggles once");
   Check(box.AccessibilityObject.Role==AccessibleRole.CheckButton&&(box.AccessibilityObject.State&AccessibleStates.Checked)!=0,"native accessibility reports checkbox role and checked state");box.ThreeState=true;box.CheckState=CheckState.Indeterminate;Pump();Check((box.AccessibilityObject.State&AccessibleStates.Mixed)!=0,"indeterminate state remains accessible");box.ThreeState=false;box.Checked=false;events=0;box.MouseMessage(0x201,box.Width-10,box.Height/2);box.MouseMessage(0x202,box.Width-10,box.Height/2);Pump();Check(box.Checked&&events==1,"clicking the label toggles the whole checkbox once");box.Checked=false;events=0;box.MouseMessage(0x201,10,box.Height/2);box.MouseMessage(0x202,10,box.Height/2);Pump();Check(box.Checked&&events==1,"clicking the custom glyph toggles once");
   box.Enabled=false;Pump();Check(!box.CanFocus&&box.Cursor==Cursors.Default,"disabled checkbox rejects focus and uses a neutral cursor");Check(Pixel(box,10,box.Height/2).ToArgb()!=Theme.Accent.ToArgb(),"disabled checked glyph uses the muted palette");box.Enabled=true;box.AutoCheck=false;bool before=box.Checked;box.ClickIt();Check(box.Checked==before,"AutoCheck false retains normal CheckBox semantics");box.AutoCheck=true;
   Theme.Dark=false;Theme.Apply(form);Pump();Check(Pixel(box,0,0).ToArgb()==Theme.Card.ToArgb(),"light theme redraws transparent corners cleanly");Check(!((System.Windows.Forms.Timer)typeof(SoftCheckBox).GetField("animation",flags).GetValue(box)).Enabled,"checkbox timer stops after state transitions settle");Motion.Enabled=false;box.Checked=!box.Checked;Check(!((System.Windows.Forms.Timer)typeof(SoftCheckBox).GetField("animation",flags).GetValue(box)).Enabled,"reduced motion updates checkbox state without a timer");form.Scale(new SizeF(1.5f,1.5f));Pump();Check(box.Height>=box.Font.Height+6,"scaled checkbox keeps its text and glyph within the row");box.Dispose();Check(typeof(SoftCheckBox).GetField("animation",flags).GetValue(box)==null,"disposing a checkbox releases its timer");form.Close();
  }Console.WriteLine("Completed "+count+" checkbox UI checks.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
