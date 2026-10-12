using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AuroraProxy {
 // Use CheckBox's checked state, keyboard and accessibility behavior, but own
 // all rendering so Windows visual styles cannot add a native checkbox glyph.
 public class SoftCheckBox:CheckBox {
  bool hover,pressed;double hoverAmount,checkAmount;long last;float dpiScale=1;
  System.Windows.Forms.Timer animation;
  public SoftCheckBox(){
   using(var graphics=Graphics.FromHwnd(IntPtr.Zero))dpiScale=graphics.DpiY/96f;
   FlatStyle=FlatStyle.Flat;
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Opaque,true);
   SetStyle(ControlStyles.SupportsTransparentBackColor,false);
   BackColor=Theme.Bg;Font=Theme.Font(9);Size=new Size(170,26);Cursor=Cursors.Hand;
   animation=new System.Windows.Forms.Timer(){Interval=16};animation.Tick+=Frame;
  }
  int Unit(int n){return Math.Max(1,(int)Math.Round(n*Font.SizeInPoints/9.0*dpiScale));}
  public void ApplyTheme(){BackColor=Theme.BackgroundBehind(this);ForeColor=Theme.Text;Invalidate();}
  protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);ApplyTheme();}
  protected override void OnParentBackColorChanged(EventArgs e){base.OnParentBackColorChanged(e);ApplyTheme();}
  public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);return new Size(text.Width+Unit(30)+Padding.Horizontal,Math.Max(Unit(26),text.Height+Unit(6))+Padding.Vertical);}
  void Animate(){if(animation==null)return;if(!Motion.Enabled||!IsHandleCreated){hoverAmount=Enabled&&hover?1:0;checkAmount=CheckState==CheckState.Unchecked?0:1;animation.Stop();Invalidate();return;}last=Stopwatch.GetTimestamp();animation.Start();Invalidate();}
  void Frame(object sender,EventArgs e){long now=Stopwatch.GetTimestamp();double step=1-Math.Exp(-Math.Min(.05,(double)(now-last)/Stopwatch.Frequency)/.045);last=now;double h=Enabled&&hover?1:0,c=CheckState==CheckState.Unchecked?0:1;hoverAmount+=(h-hoverAmount)*step;checkAmount+=(c-checkAmount)*step;if(!Motion.Enabled||(Math.Abs(h-hoverAmount)<.005&&Math.Abs(c-checkAmount)<.005)){hoverAmount=h;checkAmount=c;animation.Stop();}Invalidate();}
  protected override void OnCheckStateChanged(EventArgs e){base.OnCheckStateChanged(e);Animate();}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Animate();}
  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Animate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Animate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=false;Invalidate();}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){pressed=false;Invalidate();}}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}}
  protected override void OnKeyUp(KeyEventArgs e){base.OnKeyUp(e);pressed=false;Invalidate();}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);pressed=false;Invalidate();}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);pressed=false;Cursor=Enabled?Cursors.Hand:Cursors.Default;Animate();}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Invalidate();}
  protected override AccessibleObject CreateAccessibilityInstance(){return new CheckAccessibility(this);}
  class CheckAccessibility:ControlAccessibleObject {
   SoftCheckBox owner;
   public CheckAccessibility(SoftCheckBox control):base(control){owner=control;}
   public override AccessibleRole Role {get{return AccessibleRole.CheckButton;}}
   public override string Name {get{return owner.AccessibleName??owner.Text.Replace("&","");}set{owner.AccessibleName=value;}}
   public override string DefaultAction {get{return owner.Checked?"取消勾选":"勾选";}}
   public override AccessibleStates State {get{return base.State|(owner.CheckState==CheckState.Indeterminate?AccessibleStates.Mixed:owner.Checked?AccessibleStates.Checked:AccessibleStates.None);}}
   public override void DoDefaultAction(){if(owner.IsDisposed||!owner.CanSelect)return;owner.Focus();owner.OnClick(EventArgs.Empty);}
  }
  void PaintOwnBackground(Graphics graphics,Rectangle clip){
   var state=graphics.Save();try{graphics.SetClip(ClientRectangle,CombineMode.Intersect);graphics.SetClip(clip,CombineMode.Intersect);
    using(var brush=new SolidBrush(Theme.BackgroundBehind(this)))graphics.FillRectangle(brush,ClientRectangle);
   }finally{graphics.Restore(state);}
  }
  protected override void OnPaintBackground(PaintEventArgs e){PaintOwnBackground(e.Graphics,e.ClipRectangle);}
  protected override void OnPaint(PaintEventArgs e){
   // ButtonBase sets Opaque: the Windows WM_PAINT pipeline skips
   // OnPaintBackground. A complete frame must initialize its own background
   // before drawing the glyph/text; otherwise shared buffer pixels survive.
   // Do not call base.OnPaint, which would draw the platform checkbox.
   var g=e.Graphics;var state=g.Save();try{g.SetClip(ClientRectangle,CombineMode.Intersect);g.SetClip(e.ClipRectangle,CombineMode.Intersect);
   PaintOwnBackground(g,e.ClipRectangle);
   if(Width<1||Height<1)return;
   g.SmoothingMode=SmoothingMode.AntiAlias;int size=Unit(17),x=Padding.Left+Unit(2),y=Padding.Top+(Height-Padding.Vertical-size)/2;
   Color uncheckedFill=Theme.InputFill;
   Color selectedFill=Enabled?Theme.Accent:Theme.Blend(Theme.Muted,Theme.Card,.55);
   Color fill=Theme.Blend(uncheckedFill,selectedFill,checkAmount);
   if(Enabled&&pressed)fill=Theme.Blend(fill,Theme.Text,.12);
   Color edge=Theme.Blend(Theme.Edge,Enabled?Theme.Accent:Theme.Muted,Math.Max(checkAmount,hoverAmount*.65));
   using(var path=Theme.Rounded(new Rectangle(x,y,size,size),Unit(4)))using(var brush=new SolidBrush(fill))using(var pen=new Pen(edge,1)){g.FillPath(brush,path);g.DrawPath(pen,path);}
   if(checkAmount>.02){Color ink=Theme.Blend(fill,Enabled?Theme.AccentText:Theme.Text,checkAmount);using(var pen=new Pen(ink,Math.Max(1.6f,Unit(2)))){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;if(CheckState==CheckState.Indeterminate)g.DrawLine(pen,x+Unit(4),y+size/2,x+size-Unit(4),y+size/2);else g.DrawLines(pen,new Point[]{new Point(x+Unit(4),y+Unit(8)),new Point(x+Unit(7),y+Unit(11)),new Point(x+Unit(13),y+Unit(5))});}}
   if(Enabled&&Focused&&ShowFocusCues)using(var path=Theme.Rounded(new Rectangle(x-Unit(2),y-Unit(2),size+Unit(4),size+Unit(4)),Unit(6)))using(var pen=new Pen(Theme.Blend(Theme.Accent,Theme.Card,.35),1))g.DrawPath(pen,path);
   Theme.DrawText(g,Text,Font,new Rectangle(x+size+Unit(9),Padding.Top,Math.Max(1,Width-x-size-Unit(9)-Padding.Right),Height-Padding.Vertical),Enabled?Theme.Text:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform|(ShowKeyboardCues?TextFormatFlags.Default:TextFormatFlags.HidePrefix));
   }finally{g.Restore(state);}
  }
  protected override void Dispose(bool disposing){if(disposing&&animation!=null){animation.Stop();animation.Dispose();animation=null;}base.Dispose(disposing);}
 }
}
