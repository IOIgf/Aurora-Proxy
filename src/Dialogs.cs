using System;
using System.Drawing;
using System.Windows.Forms;

namespace AuroraProxy {
 public class SubscriptionDialog:Form {
  TextBox name,url;CheckBox proxy;string suggestedUrl="";
  public string NameValue{get{return name.Text.Trim();}}public string UrlValue{get{return url.Text.Trim();}}public bool ThroughProxy{get{return proxy.Checked;}}
  public SubscriptionDialog(Subscription s,bool running){SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Text=s==null?"添加订阅":"编辑订阅";Font=Theme.Font(9);ClientSize=new Size(570,325);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   var n=new Label(){Text="名称（留空自动获取）",Location=new Point(28,24),AutoSize=true};name=new TextBox(){Text=s==null?"":s.Name,Location=new Point(28,48),Width=510};
   if(s==null)try{string text=Clipboard.ContainsText()?Clipboard.GetText().Trim():"";Uri candidate;if(text.Length<=4096&&Uri.TryCreate(text,UriKind.Absolute,out candidate)&&(candidate.Scheme=="http"||candidate.Scheme=="https"))suggestedUrl=text;}catch{}
   var u=new Label(){Text="Clash / mihomo 订阅链接",Location=new Point(28,94),AutoSize=true};url=new TextBox(){Text=s==null?suggestedUrl:s.Url,Location=new Point(28,119),Width=510,UseSystemPasswordChar=true};
   var reveal=new SoftCheckBox(){Text="显示链接",Location=new Point(28,158),AutoSize=true};reveal.CheckedChanged+=delegate{url.UseSystemPasswordChar=!reveal.Checked;};proxy=new SoftCheckBox(){Text="通过正在运行的本地代理下载",Location=new Point(28,193),AutoSize=true,Enabled=running};
   var note=new Label(){Text=s==null?"需要 YAML 订阅；原始 ss / vmess 链接与 Base64 暂不支持。":"修改链接会立即下载并校验；清空链接会保留配置为本地文件。",Location=new Point(28,230),AutoSize=true,Font=Theme.Font(8),Tag="muted"};
   var ok=new SoftButton(){Text=s==null?"导入订阅":"保存修改",Primary=true,Location=new Point(304,270)};ok.Click+=delegate{if(NameValue.Length>80){MessageBox.Show(this,"名称不能超过 80 字符；留空可自动获取。");return;}if(s==null&&UrlValue==""){MessageBox.Show(this,"请输入订阅链接。");return;}if(UrlValue!=""){Uri uri;if(!Uri.TryCreate(UrlValue,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")){MessageBox.Show(this,"请输入 HTTP / HTTPS 链接。");return;}}DialogResult=DialogResult.OK;};
   var cancel=new SoftButton(){Text="取消",Location=new Point(432,270),Width=106,DialogResult=DialogResult.Cancel};Controls.AddRange(new Control[]{n,Theme.Input(name,36),u,Theme.Input(url,36),reveal,proxy,note,ok,cancel});AcceptButton=ok;CancelButton=cancel;Theme.Apply(this);ResumeLayout(true);
  }
 }
 public class DownloadDialog:Form {
  CheckBox proxy;public bool ThroughProxy{get{return proxy.Checked;}}
  public DownloadDialog(bool running){SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Text="更新订阅";Font=Theme.Font(9);ClientSize=new Size(430,164);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   var l=new Label(){Text="选择下载方式，更新失败将保留原配置。",Location=new Point(24,22),AutoSize=true};proxy=new SoftCheckBox(){Text="通过正在运行的本地代理下载",Enabled=running,Location=new Point(24,57),AutoSize=true};var ok=new SoftButton(){Text="开始更新",Primary=true,DialogResult=DialogResult.OK,Location=new Point(165,107)};var cancel=new SoftButton(){Text="取消",DialogResult=DialogResult.Cancel,Location=new Point(292,107)};Controls.AddRange(new Control[]{l,proxy,ok,cancel});AcceptButton=ok;CancelButton=cancel;Theme.Apply(this);ResumeLayout(true);
  }
 }
}
