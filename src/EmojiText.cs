using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace AuroraProxy {
 // Names stay untouched. Only owner-drawn presentation substitutes artwork.
 public static class EmojiText {
  static readonly Dictionary<string,string> data=new Dictionary<string,string>();
  static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
  static readonly Queue<string> order=new Queue<string>();
  static EmojiText(){foreach(string line in Load().Split('\n')){int split=line.IndexOf('=');if(split>0)data[line.Substring(0,split).Replace("-fe0f","")]=line.Substring(split+1);}}
  static string Load(){using(var stream=typeof(EmojiText).Assembly.GetManifestResourceStream("AuroraProxy.Emoji"))using(var zip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress))using(var reader=new StreamReader(zip))return reader.ReadToEnd();}
  public static string Match(string text,int start,out int length){length=0;if(text==null||start<0||start>=text.Length)return null;int first=text[start];if((first>0x3299&&!Char.IsHighSurrogate(text[start]))||(first<0x2000&&first!=0xa9&&first!=0xae&&first!=0x23&&first!=0x2a&&!(first>=0x30&&first<=0x39)))return null;string best=null;var key=new StringBuilder();for(int i=start,n=0;i<text.Length&&n<20;n++){int cp;int count=Char.IsHighSurrogate(text[i])&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])?2:1;cp=count==2?Char.ConvertToUtf32(text,i):text[i];i+=count;if(cp==0xfe0f){if(best!=null&&key.ToString()==best)length=i-start;continue;}if(key.Length>0)key.Append('-');key.Append(cp.ToString("x"));if(data.ContainsKey(key.ToString())){best=key.ToString();length=i-start;}if(cp<0x2000&&cp!=0xa9&&cp!=0xae&&cp!=0x23&&cp!=0x2a&&!(cp>=0x30&&cp<=0x39))break;}return best;}
  static Image Artwork(string key){Image image;if(images.TryGetValue(key,out image))return image;using(var stream=new MemoryStream(Convert.FromBase64String(data[key])))using(var original=Image.FromStream(stream))image=new Bitmap(original);if(images.Count>=128){string old=order.Dequeue();images[old].Dispose();images.Remove(old);}images[key]=image;order.Enqueue(key);return image;}
  public static void Draw(Graphics g,string text,Font font,Rectangle bounds,Color color,TextFormatFlags flags){if(String.IsNullOrEmpty(text)||bounds.Width<=0||bounds.Height<=0)return;int length;bool found=false;for(int i=0;i<text.Length;i++){if(Match(text,i,out length)!=null){found=true;break;}}if(!found){Theme.DrawText(g,text,font,bounds,color,flags|TextFormatFlags.NoPrefix);return;}var state=g.Save();try{g.SetClip(bounds,System.Drawing.Drawing2D.CombineMode.Intersect);int x=bounds.Left;int size=Math.Max(1,Math.Min(bounds.Height-4,Math.Max(16,(int)Math.Ceiling(font.GetHeight(g)*1.15))));int pos=0;while(pos<text.Length&&x<bounds.Right){string key=Match(text,pos,out length);if(key!=null){if(x+size>bounds.Right){Theme.DrawText(g,"…",font,new Rectangle(x,bounds.Top,bounds.Right-x,bounds.Height),color,flags);break;}g.DrawImage(Artwork(key),new Rectangle(x,bounds.Top+(bounds.Height-size)/2,size,size));x+=size+2;pos+=length;}else{int end=pos+1;while(end<text.Length&&Match(text,end,out length)==null)end++;string run=text.Substring(pos,end-pos);int width=TextRenderer.MeasureText(g,run,font,new Size(Int32.MaxValue,bounds.Height),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;Theme.DrawText(g,run,font,new Rectangle(x,bounds.Top,Math.Max(0,bounds.Right-x),bounds.Height),color,flags|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);x+=width;pos=end;}}}finally{g.Restore(state);}}
 }
}
