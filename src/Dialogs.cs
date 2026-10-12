using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AuroraProxy {
 public class CoreLogDialog:Form {
  // Rolling tail viewer for the kernel log file (runtime\mihomo.log), replacing
  // the old one-shot Notepad open that showed a stale snapshot of a live file.
  readonly string file;readonly RichTextBox view;readonly SoftCheckBox follow;readonly Label info;
  int shown=-1;long offset;bool loading,dirty=true;System.Windows.Forms.Timer pump;
  static readonly Regex Level=new Regex(@"(?i)\b(level|time)[=:\s]+\S*(error|warning|warn|info|debug)",RegexOptions.Compiled);
  public CoreLogDialog(string path){
   SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   Text="内核实时日志";Font=Theme.Font(9);ClientSize=new Size(760,480);MinimumSize=new Size(420,260);
   StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
   file=path;
   info=new Label(){Text="",Dock=DockStyle.Top,Height=26,Padding=new Padding(10,5,10,3),Font=Theme.Font(8),Tag="muted"};
   view=new RichTextBox(){Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,Font=new Font("Consolas",9.5f),WordWrap=false,DetectUrls=false,HideSelection=false};
   var bar=new BufferedFlowLayoutPanel(){Dock=DockStyle.Bottom,Height=48,WrapContents=false,Padding=new Padding(8,8,8,0)};
   follow=new SoftCheckBox(){Text="自动跟随最新输出",Checked=true,AutoSize=true,Margin=new Padding(4,9,14,0)};
   var reload=ToolButton("立即刷新",96,delegate{dirty=true;});
   var clear=ToolButton("清空显示",88,delegate{view.Clear();shown=-1;offset=0;dirty=true;});
   var open=ToolButton("用记事本打开",104,delegate{try{if(File.Exists(file))Process.Start(new ProcessStartInfo("notepad.exe","\""+file+"\""){UseShellExecute=true});else MessageBox.Show(this,"日志文件尚未生成。内核启动后会自动写入 runtime\\mihomo.log。","内核日志",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show(this,ex.Message,"打开文件",MessageBoxButtons.OK,MessageBoxIcon.Warning);}});
   var close=new SoftButton(){Text="关闭",DialogResult=DialogResult.Cancel,Width=88};close.Margin=new Padding(0,4,0,0);
   bar.Controls.Add(follow);bar.Controls.Add(reload);bar.Controls.Add(clear);bar.Controls.Add(open);bar.Controls.Add(close);
   Controls.Add(view);Controls.Add(bar);Controls.Add(info);CancelButton=close;Theme.Apply(this);
   Shown+=delegate{LoadInitial();pump=new System.Windows.Forms.Timer(){Interval=500};pump.Tick+=delegate{Pump();};pump.Start();};
   FormClosed+=delegate{if(pump!=null){pump.Stop();pump.Dispose();pump=null;}};
   ResumeLayout(true);
  }
  static SoftButton ToolButton(string text,int width,Action action){var b=new SoftButton(){Text=text,Width=width};b.Click+=delegate{action();};return b;}
  void LoadInitial(){loading=true;try{
    if(!File.Exists(file)){AppendBatch(new List<string>{"[等待内核写入日志…]"});Tick(0);return;}
    using(var fs=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
     AppendBatch(ReadAll(fs));
     long tail=Math.Max(0,fs.Length-64*1024);fs.Seek(tail,SeekOrigin.Begin);offset=fs.Position;
     if(tail>0){shown=-1;dirty=true;}
    }}finally{loading=false;}ScrollEnd();
  }
  void Pump(){if(loading||!Visible)return;try{
    if(!File.Exists(file)){Tick(0);return;}
    using(var fs=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){long length=fs.Length;
     if(length<offset){offset=0;shown=-1;view.Clear();dirty=true;}
     if(!dirty&&length==offset){Tick(length);return;}
     fs.Seek(offset,SeekOrigin.Begin);List<string> lines=ReadAll(fs);offset=fs.Position;
     if(shown<0){AppendBatch(lines);}else foreach(string line in lines)AppendLine(line);
     dirty=false;Tick(length);
    }}catch{}
  }
  void Tick(long length){info.Text="文件："+file+(length>0?"  ·  "+Engine.Bytes(length)+"  ·  已显示最近 "+Math.Max(0,shown)+" 行（上限 4000）":"  ·  尚未生成");}
  List<string> ReadAll(StreamReader reader){var outLines=new List<string>();string line;int bytes=0;
   while((line=reader.ReadLine())!=null){bytes+=line.Length+Environment.NewLine.Length;if(bytes>256*1024){TrimOldest(outLines,bytes);bytes=0;}outLines.Add(line);}
   return outLines;}
  List<string> ReadAll(FileStream fs){fs.Seek(Math.Max(0,fs.Length-64*1024),SeekOrigin.Begin);using(var reader=new StreamReader(fs,new UTF8Encoding(false,true),false,8192))return ReadAll(reader);}
  void TrimOldest(List<string> lines,int bytes){int keep=0,i=0;while(i<lines.Count&&keep+lines[i].Length+2<bytes/2){keep+=lines[i].Length+2;i++;}lines.RemoveRange(0,i);}
  void AppendLine(string line){AppendBatch(new List<string>{line});}
  void AppendBatch(List<string> lines){
   if(lines.Count==0){if(follow.Checked)ScrollEnd();return;}
   bool batch=shown<0;foreach(string line in lines){Colorize(line);shown++;TrimBuffer();}
   if(batch||follow.Checked)ScrollEnd();
  }
  void TrimBuffer(){while(shown>4000&&view.TextLength>0){int cut=view.GetFirstCharIndexFromLine(0);int next=view.GetFirstCharIndexFromLine(1);if(next<=cut)break;view.Select(cut,next-cut);view.SelectedText="";shown--;}}
  void Colorize(string line){int start=view.TextLength;view.AppendText(line+Environment.NewLine);
   Color c=Theme.Text;if(line.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0)c=Theme.Amber;
   else if(Level.IsMatch(line)){string m=Level.Match(line).Value.ToLowerInvariant();c=m.Contains("debug")?Theme.Muted:(m.Contains("warning")||m.Contains("warn"))?Theme.Amber:Theme.Accent;}
   if(c!=Theme.Text){view.Select(start,line.Length);view.SelectionColor=c;view.Select(view.TextLength,0);}}
  void ScrollEnd(){view.SelectionStart=view.TextLength;view.ScrollToCaret();}
 }
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
