using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AuroraProxy {
 public class ProxySnapshot { public int Flags{get;set;} public string Server{get;set;} public string Bypass{get;set;} public string Pac{get;set;} }
 public class ProxyJournal { public ProxySnapshot Before{get;set;} public ProxySnapshot Applied{get;set;} }
 public sealed class SystemProxy {
  [StructLayout(LayoutKind.Explicit,Size=8)] struct Value { [FieldOffset(0)] public int Number; [FieldOffset(0)] public IntPtr Text; }
  [StructLayout(LayoutKind.Sequential)] struct Option {public int Kind;public Value Data;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct OptionList {public int Size;public IntPtr Connection;public int Count;public int Error;public IntPtr Options;}
  [DllImport("wininet.dll",EntryPoint="InternetQueryOptionW",SetLastError=true)] static extern bool Query(IntPtr h,int option,ref OptionList list,ref int length);
  [DllImport("wininet.dll",EntryPoint="InternetSetOptionW",SetLastError=true)] static extern bool Set(IntPtr h,int option,ref OptionList list,int length);
  [DllImport("wininet.dll",EntryPoint="InternetSetOptionW",SetLastError=true)] static extern bool Notify(IntPtr h,int option,IntPtr data,int length);
  [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr p);
  readonly string path; readonly object gate=new object(); public SystemProxy(string root){path=Path.Combine(root,"proxy-recovery.json");}
  public bool Owned {get{return File.Exists(path);}}
  public static bool Windows {get{return Environment.OSVersion.Platform==PlatformID.Win32NT;}}
  static ProxySnapshot Read(){
   int stride=Marshal.SizeOf(typeof(Option));IntPtr buf=Marshal.AllocHGlobal(stride*4);
   try{for(int i=0;i<4;i++)Marshal.StructureToPtr(new Option(){Kind=i+1},IntPtr.Add(buf,stride*i),false);
    var list=new OptionList(){Size=Marshal.SizeOf(typeof(OptionList)),Count=4,Options=buf};int length=list.Size;
    if(!Query(IntPtr.Zero,75,ref list,ref length))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法读取 Windows 系统代理。");
    var values=new Option[4];for(int i=0;i<4;i++)values[i]=(Option)Marshal.PtrToStructure(IntPtr.Add(buf,stride*i),typeof(Option));
    var s=new ProxySnapshot(){Flags=values[0].Data.Number,Server=Marshal.PtrToStringUni(values[1].Data.Text)??"",Bypass=Marshal.PtrToStringUni(values[2].Data.Text)??"",Pac=Marshal.PtrToStringUni(values[3].Data.Text)??""};
    for(int i=1;i<4;i++)if(values[i].Data.Text!=IntPtr.Zero)GlobalFree(values[i].Data.Text);return s;
   }finally{Marshal.FreeHGlobal(buf);}
  }
  static void Write(ProxySnapshot s){
   int stride=Marshal.SizeOf(typeof(Option));IntPtr buf=Marshal.AllocHGlobal(stride*4);var strings=new IntPtr[]{Marshal.StringToHGlobalUni(s.Server??""),Marshal.StringToHGlobalUni(s.Bypass??""),Marshal.StringToHGlobalUni(s.Pac??"")};
   try{Marshal.StructureToPtr(new Option(){Kind=1,Data=new Value(){Number=s.Flags}},buf,false);
    for(int i=1;i<4;i++)Marshal.StructureToPtr(new Option(){Kind=i+1,Data=new Value(){Text=strings[i-1]}},IntPtr.Add(buf,stride*i),false);
    var list=new OptionList(){Size=Marshal.SizeOf(typeof(OptionList)),Count=4,Options=buf};
    if(!Set(IntPtr.Zero,75,ref list,list.Size))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法修改 Windows 系统代理。");
    Notify(IntPtr.Zero,39,IntPtr.Zero,0);Notify(IntPtr.Zero,37,IntPtr.Zero,0);
   }finally{foreach(var p in strings)Marshal.FreeHGlobal(p);Marshal.FreeHGlobal(buf);}
  }
  static bool Same(ProxySnapshot a,ProxySnapshot b){return a.Flags==b.Flags&&a.Server==b.Server&&a.Bypass==b.Bypass&&a.Pac==b.Pac;}
  public void Enable(int port){lock(gate){if(!Windows)throw new PlatformNotSupportedException("系统代理开关仅用于 Windows。");if(Owned)return;
   var j=new ProxyJournal(){Before=Read(),Applied=new ProxySnapshot(){Flags=3,Server="127.0.0.1:"+port,Bypass="<local>;localhost;127.*;[::1]",Pac=""}};
   Store.Atomic(path,Store.Json.Serialize(j));try{Write(j.Applied);}catch{Restore();throw;}
  }}
  public void Restore(){lock(gate){if(!Owned||!Windows)return;var j=Store.Json.Deserialize<ProxyJournal>(File.ReadAllText(path));
   // Never overwrite a proxy changed by the user or another application after us.
   if(Same(Read(),j.Applied))Write(j.Before);File.Delete(path);
  }}
 }
 // Closing the last job handle kills the child even when the GUI crashes.
 public sealed class ChildJob : IDisposable {
  [StructLayout(LayoutKind.Sequential)] struct Basic {public long P1,P2;public uint Flags;public UIntPtr Min,Max;public uint Processes;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)] struct IO {public ulong A,B,C,D,E,F;}
  [StructLayout(LayoutKind.Sequential)] struct Extended {public Basic Basic;public IO IO;public UIntPtr A,B,C,D;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a,string n);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr h,int kind,ref Extended info,uint size);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr h,IntPtr p);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  IntPtr handle;
  public bool Attach(System.Diagnostics.Process p){if(!SystemProxy.Windows)return false;handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)return false;
   var info=new Extended();info.Basic.Flags=0x2000;
   if(!SetInformationJobObject(handle,9,ref info,(uint)Marshal.SizeOf(typeof(Extended)))||!AssignProcessToJobObject(handle,p.Handle)){Dispose();return false;}return true;
  }
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}
