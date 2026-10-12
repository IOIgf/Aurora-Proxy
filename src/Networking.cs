using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace AuroraProxy {
 public class LanAddress {
  public string Address,Adapter;
  public override string ToString(){return Adapter+"  ·  "+Address;}
 }
 public static class NetworkInfo {
  public static bool PrivateIPv4(IPAddress ip){if(ip.AddressFamily!=AddressFamily.InterNetwork)return false;byte[] b=ip.GetAddressBytes();return b[0]==10||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==192&&b[1]==168||b[0]==169&&b[1]==254;}
  public static List<LanAddress> Addresses(){var items=new List<LanAddress>();foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()){if(nic.OperationalStatus!=OperationalStatus.Up||nic.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;try{foreach(var entry in nic.GetIPProperties().UnicastAddresses)if(PrivateIPv4(entry.Address)&&!items.Any(x=>x.Address==entry.Address.ToString()))items.Add(new LanAddress(){Address=entry.Address.ToString(),Adapter=nic.Name});}catch(NetworkInformationException){}}return items;}
  public static string Instructions(string ip,Settings settings,bool running){return "局域网代理连接\r\n\r\n共享状态："+(settings.AllowLan?(running?"已运行":"已开启设置，尚未启动内核"):"尚未开启")+"\r\n服务器："+ip+"\r\n端口："+settings.Port+"\r\n类型：HTTP / SOCKS5（同一个混合端口）\r\n验证："+(settings.LanAuthentication?"用户名 "+settings.LanUser+"；密码使用局域网页设置的共享密码":"无需用户名和密码")+"\r\n\r\n1. 在局域网页开启共享、保存设置，再启动内核。\r\n2. 其他设备连接同一路由器 / Wi-Fi，填写上面的服务器和端口；不要填写 127.0.0.1。\r\n3. 手机可在 Wi-Fi 设置中查找手动 HTTP 代理；如果没有用户名 / 密码输入入口，使用支持代理验证的客户端。\r\n4. Windows 设备可在 Internet 选项 → 连接 → 局域网设置中填写 HTTP 代理；应用也可单独配置 SOCKS5。\r\n5. 如果无法连接，在本机 Windows 防火墙允许 core\\mihomo.exe 通过家庭 / 工作网络，并检查路由器是否开启了设备隔离。\r\n\r\n本版仅共享 IPv4 私网代理入口。电脑和内核必须保持运行；远端设备使用完毕后关闭其代理设置。API 控制接口仍只在本机监听。\r\n共享设置改变后需停止内核再保存，然后重新启动。";}
 }
 public static class Startup {
  const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static string Command(string executable){if(String.IsNullOrWhiteSpace(executable)||executable.IndexOf('"')>=0)throw new ArgumentException("程序路径无效。");return "\""+executable+"\" --tray";}
  public static void SetEnabled(bool enabled,string executable){if(!SystemProxy.Windows){if(enabled)throw new PlatformNotSupportedException("开机启动设置仅用于 Windows。");return;}using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){if(enabled)key.SetValue("AuroraProxy",Command(executable),RegistryValueKind.String);else key.DeleteValue("AuroraProxy",false);}}
 }
 public class ConnectionInfo {
  public string Id,Target,Source,Protocol,Rule;public long Upload,Download;
  static string Text(Dictionary<string,object> data,string key){object o;return data.TryGetValue(key,out o)&&o!=null?o.ToString():"";}
  static long Number(Dictionary<string,object> data,string key){long n;return Int64.TryParse(Text(data,key),out n)?n:0;}
  public static List<ConnectionInfo> Parse(Dictionary<string,object> data){var items=new List<ConnectionInfo>();object all;if(!data.TryGetValue("connections",out all))return items;var list=all as IEnumerable;if(list==null||all is string)return items;foreach(var value in list){var d=value as Dictionary<string,object>;if(d==null)continue;object raw;var m=d.TryGetValue("metadata",out raw)?raw as Dictionary<string,object>:null;if(m==null)m=new Dictionary<string,object>();string host=Text(m,"host");if(host=="")host=Text(m,"destinationIP");items.Add(new ConnectionInfo(){Id=Text(d,"id"),Target=host+":"+Text(m,"destinationPort"),Source=Text(m,"sourceIP")+":"+Text(m,"sourcePort"),Protocol=Text(m,"network")+" · "+Text(m,"type"),Rule=Text(d,"rule")+(Text(d,"rulePayload")==""?"":" · "+Text(d,"rulePayload")),Upload=Number(d,"upload"),Download=Number(d,"download")});}return items;}
 }
}
