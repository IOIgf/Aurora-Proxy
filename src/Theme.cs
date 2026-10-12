using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;

namespace AuroraProxy {
 public enum PaletteTone {Neutral,Teal,Blue,Violet,Amber}
 // Background-only renderers must never paint controls, labels or native HWNDs.
 public interface IBackgroundLayer {void PaintBackgroundLayer(Graphics graphics);}
 public static class Theme {
  public static bool Dark=true;
  public static Color Bg {get{return Dark?Color.FromArgb(9,13,22):Color.FromArgb(221,228,240);}}
  public static Color Side {get{return Dark?Color.FromArgb(13,19,31):Color.FromArgb(237,241,249);}}
  public static Color Card {get{return Dark?Color.FromArgb(19,28,43):Color.FromArgb(247,249,253);}}
  public static Color Edge {get{return Dark?Color.FromArgb(43,57,80):Color.FromArgb(181,194,215);}}
  public static Color Text {get{return Dark?Color.FromArgb(228,234,246):Color.FromArgb(24,33,55);}}
  public static Color Muted {get{return Dark?Color.FromArgb(143,159,186):Color.FromArgb(78,95,123);}}
  public static Color InputFill {get{return Dark?Color.FromArgb(12,19,31):Color.FromArgb(231,237,247);}}
  public static Color Accent {get{return Dark?Color.FromArgb(52,172,150):Color.FromArgb(18,117,105);}}
  public static Color AccentText {get{return Dark?Color.FromArgb(8,30,34):Color.White;}}
  public static Color Blue {get{return Dark?Color.FromArgb(105,145,239):Color.FromArgb(53,80,174);}}
  public static Color Violet {get{return Dark?Color.FromArgb(162,136,224):Color.FromArgb(110,72,165);}}
  public static Color Amber {get{return Dark?Color.FromArgb(213,168,89):Color.FromArgb(148,94,30);}}
  public static Color Primary {get{return Dark?Color.FromArgb(59,84,185):Color.FromArgb(45,70,163);}}
  public static Color Hover {get{return Dark?Color.FromArgb(29,42,63):Color.FromArgb(216,226,242);}}
  public static Color ToneColor(PaletteTone tone){return tone==PaletteTone.Teal?Accent:tone==PaletteTone.Blue?Blue:tone==PaletteTone.Violet?Violet:tone==PaletteTone.Amber?Amber:Muted;}
  public static Color Blend(Color a,Color b,double amount){amount=Math.Max(0,Math.Min(1,amount));return Color.FromArgb((int)(a.R+(b.R-a.R)*amount),(int)(a.G+(b.G-a.G)*amount),(int)(a.B+(b.B-a.B)*amount));}
  public static Font Font(float size,FontStyle style=FontStyle.Regular){return new Font("Microsoft YaHei",size,style);}
  public static void DrawText(Graphics graphics,string text,Font font,Rectangle bounds,Color color,TextFormatFlags flags){if(bounds.Width<=0||bounds.Height<=0)return;var state=graphics.Save();try{graphics.SetClip(bounds,CombineMode.Intersect);TextRenderer.DrawText(graphics,text,font,bounds,color,flags|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform);}finally{graphics.Restore(state);}}
  public static Color BackgroundBehind(Control c){for(Control p=c.Parent;p!=null;p=p.Parent){var surface=p as Surface;if(surface!=null)return surface.FillColor;if(p.BackColor.A==255)return p.BackColor;}return Bg;}
  public static void PaintBehind(Control child,Graphics graphics,Rectangle clip){
   var state=graphics.Save();try{graphics.SetClip(clip,CombineMode.Intersect);var parent=child.Parent;
    if(parent==null){using(var brush=new SolidBrush(Bg))graphics.FillRectangle(brush,clip);return;}
    graphics.TranslateTransform(-child.Left,-child.Top);PaintLayer(parent,graphics);
   }finally{graphics.Restore(state);}
  }
  static void PaintLayer(Control control,Graphics graphics){
   var layer=control as IBackgroundLayer;
   if(layer!=null||control.BackColor.A!=255)PaintBehind(control,graphics,control.ClientRectangle);
   else using(var brush=new SolidBrush(control.BackColor))graphics.FillRectangle(brush,control.ClientRectangle);
   if(control.BackgroundImage!=null){var image=control.BackgroundImage;var area=control.ClientRectangle;Rectangle target=area;
    if(control.BackgroundImageLayout==ImageLayout.Tile){using(var brush=new TextureBrush(image,WrapMode.Tile))graphics.FillRectangle(brush,area);}
    else{if(control.BackgroundImageLayout==ImageLayout.None)target=new Rectangle(0,0,image.Width,image.Height);
     if(control.BackgroundImageLayout==ImageLayout.Center)target=new Rectangle((area.Width-image.Width)/2,(area.Height-image.Height)/2,image.Width,image.Height);
     if(control.BackgroundImageLayout==ImageLayout.Zoom){float scale=Math.Min(area.Width/(float)image.Width,area.Height/(float)image.Height);int width=(int)Math.Round(image.Width*scale),height=(int)Math.Round(image.Height*scale);target=new Rectangle((area.Width-width)/2,(area.Height-height)/2,width,height);}
     graphics.DrawImage(image,target);
    }
   }
   if(layer!=null){var state=graphics.Save();try{graphics.SetClip(control.ClientRectangle,CombineMode.Intersect);layer.PaintBackgroundLayer(graphics);}finally{graphics.Restore(state);}}
  }
  public static GraphicsPath Rounded(Rectangle r,int radius){var p=new GraphicsPath();if(r.Width<=0||r.Height<=0)return p;radius=Math.Max(0,Math.Min(radius,Math.Min(r.Width,r.Height)/2));if(radius==0){p.AddRectangle(r);return p;}int d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static void Apply(Control c){
   if(c is InputFrame){((InputFrame)c).ApplyTheme();return;}
   c.ForeColor=Text;if(c is SoftCheckBox){((SoftCheckBox)c).ApplyTheme();return;}if(c is SoftButton){((SoftButton)c).ApplyTheme();return;}if(c is Logo){c.Invalidate();return;}
   Color background=Bg;for(Control p=c;p!=null;p=p.Parent){if(p is Surface){background=((Surface)p).FillColor;break;}if((p.Tag as string)=="sidebar"){background=Side;break;}}
   if(c is Surface){c.BackColor=((Surface)c).FillColor;c.Invalidate();}
   else if(c is TextBoxBase){c.BackColor=Card;}
   else if(c is ListView){var v=(ListView)c;v.BackColor=Card;v.ForeColor=Text;}
   else if(c is ComboBox||c is NumericUpDown){c.BackColor=Card;if(c is ComboBox)((ComboBox)c).FlatStyle=FlatStyle.Flat;}
   else c.BackColor=background;
   if((c.Tag as string)=="muted")c.ForeColor=Muted;
   if((c.Tag as string)=="teal")c.ForeColor=Accent;
   if((c.Tag as string)=="blue")c.ForeColor=Blue;
   if((c.Tag as string)=="violet")c.ForeColor=Violet;
   if((c.Tag as string)=="amber")c.ForeColor=Amber;
   foreach(Control child in c.Controls)Apply(child);
  }
  public static InputFrame Input(Control editor,int height=32){return new InputFrame(editor,height);}
 }
 public class NoWheelComboBox:ComboBox {
  protected override void OnMouseWheel(MouseEventArgs e){var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
  protected override void WndProc(ref Message m){if(m.Msg==0x020A||m.Msg==0x020E){m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 }
 public class Surface:Panel,IBackgroundLayer {
  PaletteTone tone;
  public PaletteTone Tone {get{return tone;}set{tone=value;BackColor=FillColor;Invalidate();}}
  public Color FillColor {get{return tone==PaletteTone.Neutral?Theme.Card:Theme.Blend(Theme.Card,Theme.ToneColor(tone),Theme.Dark?.075:.045);}}
  public Surface(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);SetStyle(ControlStyles.SupportsTransparentBackColor,false);Padding=new Padding(22);BackColor=FillColor;}
  protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics,e.ClipRectangle);}
  protected override void OnPaint(PaintEventArgs e){PaintBackgroundLayer(e.Graphics);base.OnPaint(e);}
  public void PaintBackgroundLayer(Graphics graphics){if(Width<30||Height<30)return;var state=graphics.Save();try{graphics.SetClip(ClientRectangle,CombineMode.Intersect);graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Rounded(new Rectangle(0,0,Width-1,Height-1),14))using(var b=new SolidBrush(FillColor))using(var pen=new Pen(tone==PaletteTone.Neutral?Theme.Edge:Theme.Blend(Theme.Edge,Theme.ToneColor(tone),.24))){graphics.FillPath(b,p);graphics.DrawPath(pen,p);}if(tone!=PaletteTone.Neutral&&Width>70)using(var pen=new Pen(Theme.ToneColor(tone),2)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;graphics.DrawLine(pen,Width-48,20,Width-24,20);}}finally{graphics.Restore(state);}}
 }
 // Own the complete paint pipeline instead of mixing the platform ButtonBase
 // renderer and transparent-background behavior with our rounded shape.
 public class SoftButton:Control,IButtonControl {
  bool primary,navigation,selected,hover,mouseHeld,keyHeld,isDefault;double hoverAmount,pressAmount,selectAmount;long last;
  DialogResult dialogResult;
  System.Windows.Forms.Timer animation;
  public bool Primary {get{return primary;}set{if(primary==value)return;primary=value;Invalidate();}}
  public bool Navigation {get{return navigation;}set{navigation=value;Invalidate();}}
  public bool Selected {get{return selected;}set{if(selected==value)return;selected=value;Animate();}}
  public PaletteTone Tone {get;set;}
  public SoftButton(){
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
   SetStyle(ControlStyles.SupportsTransparentBackColor,false);
   SetStyle(ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,false);
   BackColor=Theme.Bg;TabStop=true;AccessibleRole=AccessibleRole.PushButton;
   Font=Theme.Font(10);Cursor=Cursors.Hand;Size=new Size(116,40);Margin=new Padding(0,0,10,0);
   animation=new System.Windows.Forms.Timer(){Interval=16};animation.Tick+=Frame;
  }
  double HoverTarget {get{return Enabled&&hover?1:0;}}
  public void ApplyTheme(){BackColor=Theme.BackgroundBehind(this);ForeColor=Theme.Text;Invalidate();}
  protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);ApplyTheme();}
  protected override void OnParentBackColorChanged(EventArgs e){base.OnParentBackColorChanged(e);ApplyTheme();}
  double PressTarget {get{return Enabled&&(keyHeld||(mouseHeld&&hover))?1:0;}}
  void Animate(){if(animation==null)return;if(!Motion.Enabled||!IsHandleCreated){hoverAmount=HoverTarget;pressAmount=PressTarget;selectAmount=selected?1:0;animation.Stop();Invalidate();return;}last=Stopwatch.GetTimestamp();animation.Start();Invalidate();}
  void Frame(object sender,EventArgs e){long now=Stopwatch.GetTimestamp();double dt=Math.Min(.05,(double)(now-last)/Stopwatch.Frequency);last=now;double step=1-Math.Exp(-dt/0.045);
   hoverAmount+=(HoverTarget-hoverAmount)*step;pressAmount+=(PressTarget-pressAmount)*step;selectAmount+=((selected?1:0)-selectAmount)*step;
   if(!Motion.Enabled||(Math.Abs(HoverTarget-hoverAmount)<.005&&Math.Abs(PressTarget-pressAmount)<.005&&Math.Abs((selected?1:0)-selectAmount)<.005)){hoverAmount=HoverTarget;pressAmount=PressTarget;selectAmount=selected?1:0;animation.Stop();}Invalidate();
  }
  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Animate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Animate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Enabled&&Focus()){mouseHeld=true;Capture=true;hover=ClientRectangle.Contains(e.Location);Animate();}}
  protected override void OnMouseUp(MouseEventArgs e){bool click=e.Button==MouseButtons.Left&&mouseHeld&&Capture&&ClientRectangle.Contains(e.Location);if(e.Button==MouseButtons.Left){mouseHeld=false;Capture=false;Animate();}base.OnMouseUp(e);if(click)PerformClick();}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(mouseHeld){bool next=ClientRectangle.Contains(e.Location);if(next!=hover){hover=next;Animate();}}}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){mouseHeld=false;Animate();}}
  protected override bool IsInputKey(Keys keyData){if((keyData&Keys.KeyCode)==Keys.Space&&(keyData&(Keys.Alt|Keys.Control))==0)return true;return base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(!e.Handled&&e.KeyCode==Keys.Space&&!e.Alt&&!e.Control&&Enabled){keyHeld=true;Animate();e.Handled=true;e.SuppressKeyPress=true;}}
  protected override void OnKeyUp(KeyEventArgs e){base.OnKeyUp(e);if(e.KeyCode==Keys.Space){bool click=keyHeld&&!e.Handled&&Focused&&!e.Alt&&!e.Control;keyHeld=false;Animate();e.Handled=true;e.SuppressKeyPress=true;if(click)PerformClick();}}
  protected override bool ProcessDialogKey(Keys keyData){if(keyData==Keys.Escape){keyHeld=false;mouseHeld=false;Capture=false;Animate();}if(keyData==Keys.Enter&&Focused&&Enabled){PerformClick();return true;}return base.ProcessDialogKey(keyData);}
  protected override bool ProcessMnemonic(char charCode){if(CanSelect&&IsMnemonic(charCode,Text)){Focus();PerformClick();return true;}return base.ProcessMnemonic(charCode);}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);keyHeld=false;mouseHeld=false;Capture=false;Animate();}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);if(!Enabled){hover=false;mouseHeld=false;keyHeld=false;Capture=false;}Cursor=Enabled?Cursors.Hand:Cursors.Default;Animate();}
  public DialogResult DialogResult {get{return dialogResult;}set{if(value<DialogResult.None||value>DialogResult.No)throw new ArgumentOutOfRangeException("value");dialogResult=value;}}
  public void NotifyDefault(bool value){isDefault=value;Invalidate();}
  public void PerformClick(){if(IsDisposed||!CanSelect)return;var container=GetContainerControl() as ContainerControl;if(CausesValidation&&container!=null&&!container.Validate(true))return;if(IsDisposed||!CanSelect)return;var form=FindForm();if(form!=null&&dialogResult!=DialogResult.None)form.DialogResult=dialogResult;OnClick(EventArgs.Empty);}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Invalidate();}
  protected override void OnPaintBackground(PaintEventArgs e){
   // Paint only background geometry/images. Never invoke an ancestor's
   // WinForms paint pipeline, which can replay its foreground into a sibling.
   Theme.PaintBehind(this,e.Graphics,e.ClipRectangle);
  }
  protected override AccessibleObject CreateAccessibilityInstance(){return new ButtonAccessibility(this);}
  class ButtonAccessibility:ControlAccessibleObject {
   SoftButton owner;
   public ButtonAccessibility(SoftButton button):base(button){owner=button;}
   public override AccessibleRole Role {get{return AccessibleRole.PushButton;}}
   public override string Name {get{return owner.AccessibleName??owner.Text.Replace("&","");}set{owner.AccessibleName=value;}}
   public override string DefaultAction {get{return "按下";}}
   public override AccessibleStates State {get{return base.State|(owner.PressTarget>0?AccessibleStates.Pressed:AccessibleStates.None)|(owner.isDefault?AccessibleStates.Default:AccessibleStates.None);}}
   public override void DoDefaultAction(){owner.PerformClick();}
  }
  protected override void OnPaint(PaintEventArgs e){if(Width<8||Height<8)return;var g=e.Graphics;var state=g.Save();try{g.SetClip(ClientRectangle,CombineMode.Intersect);g.SmoothingMode=SmoothingMode.AntiAlias;
   Color tint=Theme.ToneColor(Tone==PaletteTone.Neutral?PaletteTone.Blue:Tone);
   Color primaryFill=Tone==PaletteTone.Neutral||Tone==PaletteTone.Blue?Theme.Primary:Theme.Blend(Theme.ToneColor(Tone),Theme.Dark?Color.Black:Theme.Text,.24);
   Color bg=navigation?Theme.Blend(Theme.Side,tint,selectAmount*(Theme.Dark?.13:.1)):Theme.Card;
   if(primary&&Enabled)bg=primaryFill;
   Color hovered=primary?Theme.Blend(primaryFill,Color.White,.12):Theme.Hover;
   if(Enabled){bg=Theme.Blend(bg,hovered,hoverAmount);bg=Theme.Blend(bg,primary?Theme.Blend(primaryFill,Color.Black,.18):Theme.Blend(Theme.Hover,Theme.Bg,.3),pressAmount);}else bg=Theme.Blend(bg,Theme.Bg,.55);
   Color edge=Theme.Blend(Theme.Edge,tint,Enabled?hoverAmount*.55:0);if(primary&&Enabled)edge=bg;
   int offset=(int)Math.Round(pressAmount);using(var p=Theme.Rounded(new Rectangle(2,2+offset,Width-5,Height-5-offset),10))using(var b=new SolidBrush(bg))using(var pen=new Pen(edge,1)){g.FillPath(b,p);if(!navigation)g.DrawPath(pen,p);}
   if(navigation&&selectAmount>.01){using(var b=new SolidBrush(Theme.Blend(bg,tint,selectAmount)))using(var p=Theme.Rounded(new Rectangle(4,13,3,Math.Max(6,Height-26)),1))g.FillPath(b,p);}
   var textBounds=new Rectangle(navigation?22:8,offset,Math.Max(1,Width-(navigation?34:16)),Height-offset);
   var textFlags=(navigation?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform|(ShowKeyboardCues?TextFormatFlags.Default:TextFormatFlags.HidePrefix);
   Theme.DrawText(g,Text,Font,textBounds,Enabled?(primary?Color.White:Tone==PaletteTone.Neutral||navigation?Theme.Text:tint):Theme.Muted,textFlags);
   if(Enabled&&((Focused&&ShowFocusCues)||isDefault)){using(var p=Theme.Rounded(new Rectangle(1,1,Width-3,Height-3),11))using(var pen=new Pen(Theme.Blend(tint,Theme.Text,.25),1))g.DrawPath(pen,p);}
   }finally{g.Restore(state);}
  }
  protected override void Dispose(bool disposing){if(disposing&&animation!=null){animation.Stop();animation.Dispose();animation=null;}base.Dispose(disposing);}
 }
 public static class Motion {public static bool Enabled=true;}
 public class BufferedPanel:Panel { public BufferedPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);} }
 public class BufferedTableLayoutPanel:TableLayoutPanel { public BufferedTableLayoutPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);} }
 public class BufferedFlowLayoutPanel:FlowLayoutPanel { public BufferedFlowLayoutPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);} }
 public class TransitionPanel:BufferedPanel {
  System.Windows.Forms.Timer animation;Stopwatch watch=new Stopwatch();Control body;
  public TransitionPanel(){DoubleBuffered=true;animation=new System.Windows.Forms.Timer(){Interval=16};animation.Tick+=delegate{if(body==null||body.IsDisposed){Cancel();return;}double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/180.0);int offset=(int)Math.Round(14*Math.Pow(1-t,3));body.Location=new Point(offset,0);if(t>=1||!Motion.Enabled)Cancel();};}
  public void AnimateIn(){Cancel();if(!Motion.Enabled||!Visible||!IsHandleCreated||Controls.Count==0)return;body=Controls[0];body.Dock=DockStyle.None;body.Bounds=new Rectangle(14,0,ClientSize.Width,ClientSize.Height);watch.Restart();animation.Start();}
  public void Cancel(){if(animation!=null)animation.Stop();if(body!=null&&!body.IsDisposed){body.Location=Point.Empty;body.Dock=DockStyle.Fill;}body=null;}
  protected override void OnResize(EventArgs e){Cancel();base.OnResize(e);}
  protected override void Dispose(bool disposing){if(disposing){Cancel();if(animation!=null){animation.Dispose();animation=null;}}base.Dispose(disposing);}
 }
 public class ActivityIndicator:Control {
  public bool ErrorState {get;set;}
  System.Windows.Forms.Timer animation;Stopwatch watch=new Stopwatch();bool busy;
  public bool Busy {get{return busy;}set{if(busy==value)return;busy=value;if(busy&&Motion.Enabled&&Visible){watch.Restart();animation.Start();}else animation.Stop();Invalidate();}}
  public ActivityIndicator(){DoubleBuffered=true;Font=Theme.Font(8);Height=26;animation=new System.Windows.Forms.Timer(){Interval=32};animation.Tick+=delegate{if(!busy||!Motion.Enabled||!Visible){animation.Stop();}Invalidate();};}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;int y=Math.Max(0,(Height-12)/2);if(busy){using(var p=new Pen(Theme.Edge,2))g.DrawEllipse(p,2,y,11,11);using(var p=new Pen(Theme.Accent,2)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;g.DrawArc(p,2,y,11,11,Motion.Enabled?(float)(watch.Elapsed.TotalMilliseconds/3%360):-90,100);}}else using(var b=new SolidBrush(ErrorState?Theme.Amber:Theme.Muted))g.FillEllipse(b,5,y+4,5,5);
   Theme.DrawText(g,Text,Font,new Rectangle(24,0,Math.Max(1,Width-24),Height),ErrorState?Theme.Amber:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(animation==null)return;if(busy&&Visible&&Motion.Enabled)animation.Start();else animation.Stop();}
  protected override void Dispose(bool disposing){if(disposing&&animation!=null){animation.Stop();animation.Dispose();animation=null;}base.Dispose(disposing);}
 }
 public class Logo:Control {
  public Logo(){DoubleBuffered=true;Size=new Size(160,62);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new LinearGradientBrush(new Rectangle(0,7,38,38),Theme.Accent,Color.FromArgb(88,139,242),45))using(var p=Theme.Rounded(new Rectangle(0,7,38,38),12))e.Graphics.FillPath(b,p);
   using(var pen=new Pen(Color.FromArgb(16,45,56),2.5f)){e.Graphics.DrawLines(pen,new Point[]{new Point(10,34),new Point(19,16),new Point(28,34)});e.Graphics.DrawLine(pen,14,27,24,27);}
   using(var f=Theme.Font(15,FontStyle.Bold))Theme.DrawText(e.Graphics,"Aurora",f,new Rectangle(50,9,Width-50,28),Theme.Text,TextFormatFlags.SingleLine);
   using(var f=Theme.Font(8))Theme.DrawText(e.Graphics,"MIHOMO CLIENT",f,new Rectangle(52,35,Width-52,20),Theme.Muted,TextFormatFlags.SingleLine);
  }
 }
 public class StyledListView:ListView {
  ImageList spacing;
  public StyledListView(){OwnerDraw=true;DoubleBuffered=true;spacing=new ImageList(){ImageSize=new Size(1,36)};spacing.Images.Add(new Bitmap(1,36));SmallImageList=spacing;}
  protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Theme.Card))e.Graphics.FillRectangle(b,e.Bounds);using(var pen=new Pen(Theme.Edge))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);Theme.DrawText(e.Graphics,e.Header.Text,Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
  protected override void OnDrawItem(DrawListViewItemEventArgs e){if(View!=View.Details)e.DrawDefault=true;}
  protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e){Color bg=e.Item.Selected?Theme.Blend(Theme.Card,Theme.Blue,Theme.Dark?.18:.13):Theme.Card;using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);EmojiText.Draw(e.Graphics,e.SubItem.Text,Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),Theme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
  protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&spacing!=null){spacing.Dispose();spacing=null;}}
 }
}
