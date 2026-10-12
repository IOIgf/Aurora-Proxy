using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace AuroraProxy {
 public partial class MainForm {
  readonly ToolTip hints=new ToolTip(){AutoPopDelay=12000,InitialDelay=500,ReshowDelay=100};
  readonly Dictionary<SoftButton,Func<bool>> availability=new Dictionary<SoftButton,Func<bool>>();
  bool noticeError,pendingGlobal,pendingConnect,restoreNodeView,focusNodeSearch;string pendingProfile,rememberedProfile,rememberedGroup,rememberedSearch;
  ViewBookmark nodeBookmark,profileBookmark;Label nodeHint;
  class ViewBookmark {
   public string Selected,Focused,Top;public int TopIndex,SelectedIndex;
   public static string Key(ListViewItem item){var profile=item.Tag as Subscription;return profile==null?item.Tag as string:profile.Id;}
   public static ViewBookmark Capture(ListView list){if(list==null||list.IsDisposed)return null;var top=list.Items.Count==0?null:list.TopItem;return new ViewBookmark(){SelectedIndex=list.SelectedItems.Count==0?0:list.SelectedItems[0].Index,Selected=list.SelectedItems.Count==0?null:Key(list.SelectedItems[0]),Focused=list.FocusedItem==null?null:Key(list.FocusedItem),Top=top==null?null:Key(top),TopIndex=top==null?0:top.Index};}
   public void Restore(ListView list,string selected=null){foreach(ListViewItem item in list.Items){item.Selected=Key(item)==(selected??Selected);if(Key(item)==Focused)item.Focused=true;}if(list.Items.Count>0&&Top!=null){var top=list.Items.Cast<ListViewItem>().FirstOrDefault(x=>Key(x)==Top);list.TopItem=top??list.Items[Math.Min(TopIndex,list.Items.Count-1)];}}
  }
  SoftButton AvailableButton(string text,Action action,Func<bool> enabled,bool primary=false,int width=116){var button=Button(text,action,primary,width);availability[button]=enabled;return button;}
  void RefreshActions(){foreach(var pair in availability)if(!pair.Key.IsDisposed)pair.Key.Enabled=!busy&&pair.Value();if(focusNodeSearch&&!busy&&page==2&&search!=null&&!search.IsDisposed&&search.Enabled){search.Focus();search.SelectAll();focusNodeSearch=false;}if(nodeHint!=null&&!nodeHint.IsDisposed){nodeHint.Text=pendingGlobal?"在 GLOBAL 选择代理，或进入所选策略组设置出口；选好后自动完成刚才的操作。":!engine.Running?"内核尚未启动。请在概览连接代理，或通过托盘仅启动内核。":groups==null||groups.Items.Count==0?(busy?"正在获取策略组…":"没有可用策略组，请刷新或检查订阅。"):nodes.Items.Count==0?"没有匹配的节点。按 Esc 清空搜索，或更换策略组。":"显示 "+nodes.Items.Count+" 个节点 · Ctrl+F 搜索，Esc 清空 · 全局模式使用 GLOBAL 的出口";}}
  bool HasProfile(){return subscriptions!=null&&!subscriptions.IsDisposed&&subscriptions.SelectedItems.Count>0;}
  bool HasNode(){return engine.Running&&nodes!=null&&!nodes.IsDisposed&&nodes.SelectedItems.Count>0;}
  bool CanSelectNode(){if(!HasNode()||groups==null||groups.SelectedItem==null)return false;object raw;if(!proxyData.TryGetValue(groups.SelectedItem.ToString(),out raw))return false;var group=Dict(raw);string type=Str(group,"type");return (type=="Selector"||type=="URLTest"||type=="Fallback")&&(pendingGlobal||type!="Selector"||Str(group,"now")!=SelectedNode());}
  void Feedback(string message,bool error=false){noticeError=error;activity.ErrorState=error;activity.Invalidate();activity.Text=message;activity.Cursor=error?Cursors.Hand:Cursors.Default;hints.SetToolTip(activity,message+(error?"\n点击查看日志中的完整说明。":""));}
  void RememberView(){if(page==1&&subscriptions!=null)profileBookmark=ViewBookmark.Capture(subscriptions);if(page==2&&groups!=null&&!groups.IsDisposed){rememberedProfile=engine.ActiveId??store.State.Active;rememberedGroup=groups.SelectedItem as string;rememberedSearch=search.Text;nodeBookmark=ViewBookmark.Capture(nodes);}}
  void RevealWindow(){if(!Visible||WindowState==FormWindowState.Minimized){Show();WindowState=FormWindowState.Normal;Activate();}}
  bool PrepareStart(){if(store.Active==null){ShowPage(1);RevealWindow();Feedback("请先添加订阅：支持粘贴链接或拖入 YAML 文件。");return false;}if(!File.Exists(engine.CoreFile)){ShowPage(4);RevealWindow();Feedback("请先选择 mihomo 内核并保存设置。");return false;}return true;}
  void ToggleConnection(){if(preview||busy)return;if(engine.Proxy.Owned){Run(engine.Proxy.Restore,delegate{pendingConnect=false;Feedback("系统代理已关闭，内核继续运行，可供局域网设备使用。");},"正在关闭系统代理…");return;}if(!engine.Running&&!PrepareStart())return;pendingConnect=true;Run(delegate{if(!engine.Running)engine.Start(store.Active);if(!engine.Proxy.Owned)engine.ToggleSystemProxy();},delegate{pendingConnect=false;Feedback("系统代理已开启，可供遵循系统代理的应用使用。");},"正在连接代理…");}
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(!busy&&keyData==(Keys.Control|Keys.F)){if(page!=2)ShowPage(2);focusNodeSearch=true;RefreshActions();return true;}if(!busy&&page==2&&keyData==Keys.Escape&&search!=null&&search.Text.Length>0){search.Clear();search.Focus();return true;}if(keyData==(Keys.Control|Keys.L)){ShowPage(3);return true;}if(!busy&&keyData==(Keys.Control|Keys.R)){if(page==2)RefreshNodes();else if(page==6)RefreshConnections();else return base.ProcessCmdKey(ref msg,keyData);return true;}return base.ProcessCmdKey(ref msg,keyData);}
  void ImportDroppedFile(string path){if(busy){Feedback("正在处理当前操作，请完成后再导入。");return;}int originPage=page;Subscription created=null;Run(delegate{if(new FileInfo(path).Length>8*1024*1024)throw new IOException("文件超过 8 MiB。");string yaml=File.ReadAllText(path);engine.Validate(engine.Build(yaml));created=new Subscription(){Name=Path.GetFileNameWithoutExtension(path),Updated=DateTime.UtcNow.ToString("o"),Usage="本地配置"};Store.Atomic(store.ProfilePath(created),yaml);AddProfile(created);},delegate{if(page==originPage)ShowPage(1);FillProfiles(created.Id);Feedback("已导入 "+created.Name+"，可前往概览连接代理。");},"正在校验并导入 YAML…");}
  void InitExperience(){activity.Click+=delegate{if(noticeError&&!busy)ShowPage(3);};hints.SetToolTip(activity,"操作状态；错误提示可点击查看日志。");AllowDrop=true;DragEnter+=delegate(object sender,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];e.Effect=!busy&&files!=null&&files.Length==1&&(files[0].EndsWith(".yaml",StringComparison.OrdinalIgnoreCase)||files[0].EndsWith(".yml",StringComparison.OrdinalIgnoreCase))?DragDropEffects.Copy:DragDropEffects.None;};DragDrop+=delegate(object sender,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length==1)ImportDroppedFile(files[0]);};}
 }
}
