using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AuroraProxy {
 // Keep the native editor for IME, selection, clipboard and accessibility;
 // this container owns only its surrounding surface and the small affordances.
 public class InputFrame:Panel {
  readonly Control editor;readonly int designHeight;bool hover,arranging;Rectangle editorClip;double emphasis;long last;
  System.Windows.Forms.Timer animation;
  public Control Editor {get{return editor;}}
  public Color FillColor {get{return Theme.InputFill;}}
  double Target {get{return !editor.Enabled?0:ContainsFocus?1:hover?.28:0;}}
  public InputFrame(Control control,int height=32){
   if(control==null)throw new ArgumentNullException("control");editor=control;designHeight=height;
   // Save placement before resizing the frame triggers its first Layout event.
   Point originalLocation=control.Location;int originalWidth=control.Width;
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   SetStyle(ControlStyles.SupportsTransparentBackColor,false);
   BackColor=Theme.Bg;TabStop=false;Size=new Size(originalWidth,height);Location=originalLocation;Margin=control.Margin;Anchor=control.Anchor;TabIndex=control.TabIndex;
   // Preserve an intentional font (e.g. node search), otherwise inherit the
   // page font instead of freezing the editor's unparented system default.
   if(TypeDescriptor.GetProperties(control)["Font"].ShouldSerializeValue(control))Font=control.Font;
   control.Dock=DockStyle.None;control.Anchor=AnchorStyles.Top|AnchorStyles.Left;control.Margin=new Padding(0);control.TabIndex=0;
   var text=control as TextBox;if(text!=null){text.BorderStyle=BorderStyle.None;text.AutoSize=false;}
   var numeric=control as NumericUpDown;if(numeric!=null){numeric.BorderStyle=BorderStyle.None;}
   var combo=control as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=DrawChoice;combo.DropDown+=delegate{Invalidate();};combo.DropDownClosed+=delegate{Invalidate();};}
   Controls.Add(control);control.FontChanged+=delegate{ArrangeEditor();};control.HandleCreated+=delegate{ArrangeEditor();};control.SizeChanged+=delegate{ArrangeEditor();};Watch(control);animation=new System.Windows.Forms.Timer(){Interval=16};animation.Tick+=Frame;ApplyTheme();ArrangeEditor();
  }
  void Watch(Control c){c.GotFocus+=StateChanged;c.LostFocus+=StateChanged;c.EnabledChanged+=StateChanged;c.MouseEnter+=Entered;c.MouseLeave+=LeftField;if(c is TextBox){c.FontChanged+=delegate{ArrangeEditor();};c.SizeChanged+=delegate{ArrangeEditor();};c.LocationChanged+=delegate{ArrangeEditor();};}foreach(Control child in c.Controls)Watch(child);}
  void StateChanged(object sender,EventArgs e){Animate();}
  void Entered(object sender,EventArgs e){hover=true;Animate();}
  void LeftField(object sender,EventArgs e){hover=ClientRectangle.Contains(PointToClient(Cursor.Position));Animate();}
  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Entered(this,e);}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);LeftField(this,e);}
  void Animate(){if(animation==null)return;if(!Motion.Enabled||!IsHandleCreated){emphasis=Target;animation.Stop();Invalidate();return;}last=Stopwatch.GetTimestamp();animation.Start();Invalidate();}
  void Frame(object sender,EventArgs e){long now=Stopwatch.GetTimestamp();double dt=Math.Min(.05,(double)(now-last)/Stopwatch.Frequency);last=now;emphasis+=(Target-emphasis)*(1-Math.Exp(-dt/.04));if(!Motion.Enabled||Math.Abs(Target-emphasis)<.005){emphasis=Target;animation.Stop();}Invalidate();}
  public void ApplyTheme(){BackColor=Theme.BackgroundBehind(this);Colors(editor);Invalidate();}
  protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);BackColor=Theme.BackgroundBehind(this);Invalidate();}
  protected override void OnParentBackColorChanged(EventArgs e){base.OnParentBackColorChanged(e);BackColor=Theme.BackgroundBehind(this);Invalidate();}
  void Colors(Control c){c.BackColor=FillColor;c.ForeColor=Theme.Text;foreach(Control child in c.Controls)Colors(child);}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ArrangeEditor();}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(editor!=null){editor.Font=Font;ArrangeEditor();}}
  // The frame owns the editor's geometry; automatic scaling must not scale
  // that child a second time after ArrangeEditor has already positioned it.
  protected override bool ScaleChildren {get{return false;}}
  int Unit(int value){return Math.Max(1,(int)Math.Round(value*Height/(double)designHeight));}
  [StructLayout(LayoutKind.Sequential)] struct TextMetric {public int Height,Ascent,Descent,InternalLeading,ExternalLeading,AveCharWidth,MaxCharWidth,Weight,Overhang,DigitizedAspectX,DigitizedAspectY;public ushort FirstChar,LastChar,DefaultChar,BreakChar;public byte Italic,Underlined,StruckOut,PitchAndFamily,CharSet;}
  [StructLayout(LayoutKind.Sequential)] struct EditRect {public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr handle);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr handle,IntPtr dc);
  [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr GetEditRect(IntPtr handle,int message,IntPtr wParam,out EditRect rect);
  [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll",EntryPoint="GetTextMetricsW")] static extern bool GetTextMetrics(IntPtr dc,out TextMetric metric);
  public static int TextLineHeight(Control c){
   // Query the EDIT's actual HFONT rather than mixing GDI+ and GDI metrics.
   // Mono's managed editor uses Font.Height; Windows uses native GDI.
   if(!SystemProxy.Windows||!c.IsHandleCreated)return c.Font.Height;
   IntPtr dc=GetDC(c.Handle);if(dc==IntPtr.Zero)return c.Font.Height;
   IntPtr old=IntPtr.Zero;try{var font=SendMessage(c.Handle,0x0031,IntPtr.Zero,IntPtr.Zero);if(font!=IntPtr.Zero)old=SelectObject(dc,font);TextMetric metric;return GetTextMetrics(dc,out metric)&&metric.Height>0?metric.Height:c.Font.Height;}finally{if(old!=IntPtr.Zero)SelectObject(dc,old);ReleaseDC(c.Handle,dc);}
  }
  public static int TextLineTop(Control c){if(!SystemProxy.Windows||!c.IsHandleCreated)return 0;EditRect rect;GetEditRect(c.Handle,0x00B2,IntPtr.Zero,out rect);return Math.Max(0,rect.Top);}
  void ArrangeEditor(){if(editor==null||arranging||Width<20)return;arranging=true;try{
   int inset=Unit(10);var combo=editor as ComboBox;var numeric=editor as NumericUpDown;
   if(combo!=null){combo.ItemHeight=Math.Max(Font.Height+4,Unit(22));combo.DropDownHeight=Unit(200);}
   editor.Width=Math.Max(16,Width-(numeric!=null?Unit(44):2*inset));
   var text=editor as TextBox;int lineHeight=editor.Height,innerTop=0;
   // Read the actual native formatting rectangle instead of assuming a
   // fixed border/vertical inset under Aero, Classic or display scaling.
   if(text!=null){lineHeight=TextLineHeight(text);text.Height=lineHeight+2;innerTop=TextLineTop(text);}
   if(numeric!=null)foreach(Control child in numeric.Controls)if(child is TextBox){lineHeight=TextLineHeight(child);innerTop=child.Top+TextLineTop(child);break;}
   editor.Location=new Point(inset,Math.Max(1,(Height-lineHeight)/2-innerTop));
   // Clip the platform border and buttons; draw matching arrows in the frame.
   if(numeric!=null){Control edit=null;int right=numeric.Width;
    foreach(Control child in numeric.Controls)if(child is TextBox)edit=child;else{if(child.Width>0&&child.Left>0)right=Math.Min(right,child.Left);child.Visible=false;}
    // Some native layouts keep painting the hidden spinner's reserved area;
    // some managed layouts expand the EDIT into it. Exclude it in both cases.
    if(edit!=null)ClipEditor(new Rectangle(edit.Left,edit.Top,Math.Max(1,Math.Min(edit.Right,right)-edit.Left),edit.Height));
   }
   if(combo!=null){int trim=Unit(24);ClipEditor(new Rectangle(1,1,Math.Max(1,editor.Width-trim),Math.Max(1,editor.Height-2)));}
  }finally{arranging=false;}}
  void ClipEditor(Rectangle bounds){if(editor.Region!=null&&editorClip==bounds)return;editorClip=bounds;var old=editor.Region;editor.Region=new Region(bounds);if(old!=null)old.Dispose();}
  void DrawChoice(object sender,DrawItemEventArgs e){var combo=(ComboBox)sender;bool selected=(e.State&DrawItemState.Selected)!=0;Color bg=selected?Theme.Blend(FillColor,Theme.Blue,Theme.Dark?.2:.18):FillColor;using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);if(e.Index>=0&&e.Index<combo.Items.Count)Theme.DrawText(e.Graphics,combo.GetItemText(combo.Items[e.Index]),combo.Font,new Rectangle(e.Bounds.X+4,e.Bounds.Y,Math.Max(1,e.Bounds.Width-8),e.Bounds.Height),editor.Enabled?Theme.Text:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||!editor.Enabled)return;editor.Focus();var combo=editor as ComboBox;if(combo!=null){combo.DroppedDown=!combo.DroppedDown;return;}var numeric=editor as NumericUpDown;if(numeric!=null&&e.X>=Width-Unit(32)){if(e.Y<Height/2)numeric.UpButton();else numeric.DownButton();}}
  // These fields sit on solid page/card surfaces. Replaying a transparent
  // WinForms parent here can copy unrelated text at ancestor coordinates.
  protected override void OnPaintBackground(PaintEventArgs e){using(var b=new SolidBrush(Theme.BackgroundBehind(this)))e.Graphics.FillRectangle(b,e.ClipRectangle);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Width<10||Height<10)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color edge=Theme.Blend(Theme.Edge,Theme.Blue,emphasis*.85);using(var path=Theme.Rounded(new Rectangle(1,1,Width-3,Height-3),Unit(8)))using(var b=new SolidBrush(FillColor))using(var pen=new Pen(edge,1)){g.FillPath(b,path);g.DrawPath(pen,path);}
   if(editor is ComboBox||editor is NumericUpDown){int x=Width-Unit(18),y=Height/2;using(var pen=new Pen(editor.Enabled?Theme.Muted:Theme.Blend(Theme.Muted,FillColor,.5),1.4f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;if(editor is ComboBox)g.DrawLines(pen,new Point[]{new Point(x-Unit(4),y-Unit(2)),new Point(x,y+Unit(2)),new Point(x+Unit(4),y-Unit(2))});else{g.DrawLines(pen,new Point[]{new Point(x-Unit(3),y-Unit(5)),new Point(x,y-Unit(8)),new Point(x+Unit(3),y-Unit(5))});g.DrawLines(pen,new Point[]{new Point(x-Unit(3),y+Unit(5)),new Point(x,y+Unit(8)),new Point(x+Unit(3),y+Unit(5))});}}}
  }
  protected override void Dispose(bool disposing){if(disposing&&animation!=null){animation.Stop();animation.Dispose();animation=null;}base.Dispose(disposing);}
 }
}
