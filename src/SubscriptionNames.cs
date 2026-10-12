using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AuroraProxy {
 public static class SubscriptionNames {
  static readonly Encoding Utf8=new UTF8Encoding(false,true);
  public static string FromHeaders(string title,string disposition){
   string name=Title(title);if(name!=null)return name;
   if(String.IsNullOrEmpty(disposition)||disposition.Length>4096)return null;
   string filename=null,extended=null;
   foreach(Match m in Regex.Matches(disposition,@"(?:^|;)\s*(filename\*?)\s*=\s*(?:""((?:\\.|[^""\\])*)""|([^;]*))",RegexOptions.IgnoreCase)){
    string value=m.Groups[2].Success?Regex.Replace(m.Groups[2].Value,@"\\(.)","$1"):m.Groups[3].Value.Trim();
    if(m.Groups[1].Value.EndsWith("*"))extended=Extended(value);else filename=RepairUtf8(value);
   }
   return FileTitle(extended)??FileTitle(filename);
  }
  static string Title(string value){
   if(String.IsNullOrWhiteSpace(value)||value.Length>4096)return null;value=value.Trim();
   if(value.StartsWith("base64:",StringComparison.OrdinalIgnoreCase)){
    try{string data=value.Substring(7).Trim().Replace('-','+').Replace('_','/');data=data.PadRight(data.Length+(4-data.Length%4)%4,'=');value=Utf8.GetString(Convert.FromBase64String(data));}catch(FormatException){return null;}catch(DecoderFallbackException){return null;}
   }else value=RepairUtf8(value);
   return Clean(value);
  }
  static string RepairUtf8(string value){
   if(value==null)return null;bool high=false;foreach(char c in value){if(c>255)return value;if(c>=128)high=true;}if(!high)return value;
   try{return Utf8.GetString(Encoding.GetEncoding(28591).GetBytes(value));}catch(DecoderFallbackException){return value;}
  }
  static string Extended(string value){
   int first=value.IndexOf('\''),second=first<0?-1:value.IndexOf('\'',first+1);if(first<=0||second<0)return null;
   string charset=value.Substring(0,first);Encoding encoding;
   if(charset.Equals("UTF-8",StringComparison.OrdinalIgnoreCase))encoding=Utf8;
   else if(charset.Equals("ISO-8859-1",StringComparison.OrdinalIgnoreCase))encoding=Encoding.GetEncoding(28591);
   else return null;
   var bytes=new List<byte>();for(int i=second+1;i<value.Length;i++){
    if(value[i]=='%'){byte b;if(i+2>=value.Length||!Byte.TryParse(value.Substring(i+1,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out b))return null;bytes.Add(b);i+=2;}
    else{if(value[i]>127)return null;bytes.Add((byte)value[i]);}
   }
   try{return encoding.GetString(bytes.ToArray());}catch(DecoderFallbackException){return null;}
  }
  static string FileTitle(string value){
   if(value==null)return null;value=value.Trim().Replace('\\','/');value=value.Substring(value.LastIndexOf('/')+1);
   if(value.EndsWith(".yaml",StringComparison.OrdinalIgnoreCase))value=value.Substring(0,value.Length-5);
   else if(value.EndsWith(".yml",StringComparison.OrdinalIgnoreCase))value=value.Substring(0,value.Length-4);
   return Clean(value);
  }
  public static string Clean(string value){
   if(value==null)return null;var text=new StringBuilder();foreach(char c in value){if(Char.IsControl(c)||Char.IsWhiteSpace(c)){if(text.Length>0&&text[text.Length-1]!=' ')text.Append(' ');}else text.Append(c);}
   string name=text.ToString().Trim();if(name.Length==0)return null;
   if(name.Length>80){int length=80;if(Char.IsHighSurrogate(name[length-1])&&Char.IsLowSurrogate(name[length]))length--;name=name.Substring(0,length).TrimEnd();}
   return name;
  }
  public static string Resolve(string manual,string suggested,string url,string previous=null){
   if(!String.IsNullOrWhiteSpace(manual))return manual.Trim();
   string name=Clean(suggested)??Clean(previous);if(name!=null)return name;
   Uri uri;if(Uri.TryCreate(url,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))return Clean("订阅 · "+uri.Host);
   return "我的订阅";
  }
  public static void ApplyDownloaded(Subscription profile,DownloadResult result){if(profile.AutoName)profile.Name=Resolve(null,result.Name,profile.Url,profile.Name);}
 }
}
