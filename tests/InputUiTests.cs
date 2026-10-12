using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using AuroraProxy;

class InputUiTests {
 static int count;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
 static void Pump(){var end=DateTime.UtcNow.AddMilliseconds(350);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 static void Click(InputFrame frame,int x,int y){typeof(InputFrame).GetMethod("OnMouseDown",flags).Invoke(frame,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});Pump();}
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,flags).GetValue(owner);}
 static Color Pixel(Control c,int x,int y){using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);return b.GetPixel(x,y);}}
 [STAThread] static int Main(){string root=Path.Combine(Path.GetTempPath(),"aurora-input-ui-"+Guid.NewGuid().ToString("N"));
  try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Theme.Dark=true;Motion.Enabled=true;
   using(var form=new Form(){ClientSize=new Size(540,320),Font=Theme.Font(9)}){
    var card=new Surface(){Dock=DockStyle.Fill};form.Controls.Add(card);
    var text=new TextBox(){Location=new Point(28,30),Width=260,Text="原生输入",MaxLength=32};var input=Theme.Input(text,36);card.Controls.Add(input);
    var other=new TextBox(){Location=new Point(28,90),Width=260,UseSystemPasswordChar=true};var password=Theme.Input(other,36);card.Controls.Add(password);
    var combo=new NoWheelComboBox(){Location=new Point(28,150),Width=260,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(new object[]{"规则模式","全局模式"});combo.SelectedIndex=0;var choice=Theme.Input(combo,36);card.Controls.Add(choice);
    var number=new NumericUpDown(){Location=new Point(28,210),Width=130,Minimum=1024,Maximum=65535,Value=7890};var numeric=Theme.Input(number);card.Controls.Add(numeric);Theme.Apply(form);form.Show();Pump();
    Check(input.Location==new Point(28,30)&&password.Location==new Point(28,90)&&choice.Location==new Point(28,150),"wrapping preserves each editor's original absolute position");
    Check(text.BorderStyle==BorderStyle.None&&text.BackColor==input.FillColor&&text.Left>=9&&text.Top>=4,"native text editor uses the inset surface without platform borders");
    Check(!text.AutoSize&&text.Height>=InputFrame.TextLineHeight(text)&&Math.Abs(text.Top+InputFrame.TextLineTop(text)+InputFrame.TextLineHeight(text)/2.0-input.Height/2.0)<=2,"plain input centers the font line instead of the native preferred height");
    var numberText=number.Controls.Cast<Control>().OfType<TextBox>().First();Check(Math.Abs(number.Top+numberText.Top+InputFrame.TextLineTop(numberText)+InputFrame.TextLineHeight(numberText)/2.0-numeric.Height/2.0)<=2,"numeric input centers its nested native editor font line");
    Check(Pixel(input,0,0).ToArgb()==Theme.Card.ToArgb(),"rounded corners show the actual parent card background");
    int changes=0;text.TextChanged+=delegate{changes++;};text.SelectAll();text.SelectedText="中文输入与粘贴";Check(text.Text=="中文输入与粘贴"&&changes==1,"native selection replacement preserves Unicode text and normal change events");
    other.Text="test-password";Check(other.UseSystemPasswordChar&&other.Text=="test-password","password masking retains the editable native password value");other.UseSystemPasswordChar=false;Check(!other.UseSystemPasswordChar&&other.Text=="test-password","reveal toggle does not lose or replace the password");
    other.Focus();Pump();Color idle=Pixel(input,input.Width/2,1);text.Focus();Pump();Check(Pixel(input,input.Width/2,1).ToArgb()!=idle.ToArgb(),"focus adds an animated accent border");Check(!Field<System.Windows.Forms.Timer>(input,"animation").Enabled,"focus timer stops after the border settles");
    text.Enabled=false;Pump();Check(!text.CanFocus&&text.BackColor==input.FillColor,"disabled fields retain a matching fill and reject focus");text.Enabled=true;
    Click(choice,choice.Width-18,choice.Height/2);Check(combo.DroppedDown,"custom dropdown arrow opens the native choices");combo.SelectedIndex=1;combo.DroppedDown=false;Check(combo.SelectedIndex==1,"dropdown continues to commit explicit native selections");
    Check(number.Controls.Cast<Control>().Where(c=>!(c is TextBox)).All(c=>!c.Visible),"native numeric spinner buttons remain hidden");decimal before=number.Value;Click(numeric,numeric.Width-18,8);Check(number.Value==before+1,"custom upper arrow increments the port");Click(numeric,numeric.Width-18,numeric.Height-8);Check(number.Value==before,"custom lower arrow decrements the port");number.Value=number.Maximum;Click(numeric,numeric.Width-18,8);Check(number.Value==number.Maximum,"port arrow respects the maximum value");number.Value=number.Minimum;Click(numeric,numeric.Width-18,numeric.Height-8);Check(number.Value==number.Minimum,"port arrow respects the minimum value");
    Theme.Dark=false;Theme.Apply(form);Pump();Check(text.BackColor==input.FillColor&&Pixel(input,input.Width/2,input.Height-5).ToArgb()==input.FillColor.ToArgb(),"light theme updates both native text and the rounded field surface");Theme.Apply(form);Check(text.Parent==input&&input.Controls.Count==1,"repeated theme application keeps one wrapper and the existing editor");
    form.Scale(new SizeF(1.5f,1.5f));Pump();Check(text.Bounds.Right<input.Width&&text.Bounds.Bottom<input.Height,"scaled field keeps native text within the rounded frame");input.Dispose();Check(Field<System.Windows.Forms.Timer>(input,"animation")==null,"disposing an input releases its animation timer");form.Close();
   }
   Theme.Dark=true;using(var dialog=new SubscriptionDialog(null,false)){dialog.Show();Pump();var name=Field<TextBox>(dialog,"name");var url=Field<TextBox>(dialog,"url");Check(name.Parent is InputFrame&&url.Parent is InputFrame&&name.Parent.Location==new Point(28,48)&&url.Parent.Location==new Point(28,119),"subscription dialog shares the same fields with correct label spacing");using(var image=new Bitmap(dialog.ClientSize.Width,dialog.ClientSize.Height)){using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(dialog.PointToScreen(Point.Empty),Point.Empty,dialog.ClientSize);image.Save(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath),"subscription-inputs.png"));}dialog.Close();}
   var store=new Store(root);using(var main=new MainForm(store,true)){main.Show();main.PreviewPage(5);Pump();var user=Field<TextBox>(main,"lanUser");var pass=Field<TextBox>(main,"lanPassword");var address=Field<ComboBox>(main,"lanAddressBox");Check(user.Parent is InputFrame&&pass.Parent is InputFrame&&address.Parent.Location==new Point(24,45),"LAN text, password and address fields use consistent frames without shifting");Field<CheckBox>(main,"lanEnabled").Checked=true;user.Focus();Pump();main.CapturePage(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath),"network-inputs.png"),5);main.PreviewPage(4);Pump();var port=Field<NumericUpDown>(main,"port");Check(port.Parent is InputFrame&&port.Parent.Height<=32,"settings fields fit their existing compact rows");main.ClientSize=new Size(960,630);Pump();var core=Field<TextBox>(main,"corePath");Check(core.Parent.Right<=core.Parent.Parent.ClientSize.Width,"path input stays within its grid at the minimum window size");main.Close();}
   Console.WriteLine("Completed "+count+" input UI checks.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
