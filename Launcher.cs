using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TypelessAdGuard {
    internal static class Program {
        internal static string Quote(string value){
            return LaunchArguments.Quote(value);
        }
        [STAThread] static int Main(string[] args){
            bool observe=false,paused=false,probe=false,once=false,stop=false,background=false,selfCheck=false;
            string explicitTarget=null,dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TypelessAdGuard");
            try {
                for(int i=0;i<args.Length;i++) {
                    if(args[i]=="--target"&&i+1<args.Length)explicitTarget=Path.GetFullPath(args[++i]);
                    else if(args[i]=="--data-dir"&&i+1<args.Length)dataDir=Path.GetFullPath(args[++i]);
                    else if(args[i]=="--observe")observe=true;
                    else if(args[i]=="--paused")paused=true;
                    else if(args[i]=="--probe")probe=true;
                    else if(args[i]=="--once")once=true;
                    else if(args[i]=="--stop")stop=true;
                    else if(args[i]=="--background")background=true;
                    else if(args[i]=="--self-check")selfCheck=true;
                    else throw new ArgumentException("无法识别参数："+args[i]);
                }
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Directory.CreateDirectory(dataDir);
                string writable=Path.Combine(dataDir,".write-check-"+Guid.NewGuid().ToString("N"));
                File.WriteAllText(writable,"");File.Delete(writable);
                int release=0;using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))if(key!=null)release=Convert.ToInt32(key.GetValue("Release",0));
                bool compatible=Environment.Is64BitProcess&&Environment.OSVersion.Version.Major>=10&&release>=528040;
                if(selfCheck){File.WriteAllText(Path.Combine(dataDir,"environment.txt"),"Version=0.1.0-preview.1\r\nWindows="+Environment.OSVersion.Version+"\r\nProcess64Bit="+Environment.Is64BitProcess+"\r\nFrameworkRelease="+release+"\r\nDataWritable=True\r\nCompatible="+compatible+"\r\n");return compatible?0:4;}
                if(!compatible)throw new NotSupportedException("本版本需要 Windows 10/11 64 位和 .NET Framework 4.8 或更高版本。");
                var settings=new SettingsStore(dataDir);
                string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var defaults=new[]{Path.Combine(local,"Programs","Typeless","Typeless.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Typeless","Typeless.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Typeless","Typeless.exe")};
                string target=TargetResolver.Resolve(explicitTarget,settings.LoadTarget(),Native.RunningInstallations(),defaults,File.Exists);
                string tag;using(var hash=SHA256.Create())tag=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes((target??"UNCONFIGURED").ToUpperInvariant()))).Replace("-","").Substring(0,16);
                string stopName="Local\\TypelessAdGuard.Stop."+tag,showName="Local\\TypelessAdGuard.Show."+tag;
                if(stop){try{using(var signal=EventWaitHandle.OpenExisting(stopName))signal.Set();}catch(WaitHandleCannotBeOpenedException){}return 0;}
                var log=new LogFile(Path.Combine(dataDir,"logs",(probe||once?"probe-":"guard-")+tag+".log"));
                if(probe||once){
                    int result=1;var thread=new Thread(delegate(){
                        try{using(var engine=new GuardEngine(target,log.Write,delegate{},delegate{return once&&!observe&&!paused;})){
                            engine.Scan(false,true);log.Write("PROBE_DONE connected="+engine.Connected+" matches="+engine.Matches+" confirmed="+engine.Confirmed);result=engine.LastError==null?0:1;
                        }}catch(Exception e){log.Write("PROBE_FAILED "+e.GetType().Name);}
                    }){IsBackground=true};thread.SetApartmentState(ApartmentState.MTA);thread.Start();
                    if(!thread.Join(12000)){log.Write("PROBE_TIMEOUT");return 3;}return result;
                }
                bool restart=false,created;
                using(var mutex=new Mutex(true,"Local\\TypelessAdGuard.Instance."+tag,out created)){
                    if(!created){try{using(var signal=EventWaitHandle.OpenExisting(showName))signal.Set();}catch(WaitHandleCannotBeOpenedException){}return 0;}
                    try {
                        using(var stopEvent=new EventWaitHandle(false,EventResetMode.ManualReset,stopName))
                        using(var showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,showName)){
                            stopEvent.Reset();
                            var context=new GuardContext(target,observe,paused,log,stopEvent,showEvent,settings,!background);
                            Application.Run(context);restart=context.RestartRequested;observe=context.ObserveOnly;paused=context.IsPaused;
                        }
                    } finally {mutex.ReleaseMutex();}
                }
                if(restart)Process.Start(new ProcessStartInfo(Application.ExecutablePath,LaunchArguments.Restart(dataDir,observe,paused)){UseShellExecute=false,CreateNoWindow=true});
                return 0;
            } catch(Exception e){
                if(!background&&!probe&&!once&&!selfCheck&&!stop)MessageBox.Show("无法启动助手："+e.Message,"Typeless 提示助手",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
