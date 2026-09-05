using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TypelessAdGuard {
    internal sealed class LogFile {
        readonly string path; readonly object gate=new object();
        internal string FilePath {get{return path;}}
        internal LogFile(string file) {path=file; Directory.CreateDirectory(Path.GetDirectoryName(path));}
        internal void Write(string message) {
            lock(gate) {
                try {
                    if(File.Exists(path) && new FileInfo(path).Length>512*1024) {
                        File.Copy(path,path+".previous",true); File.WriteAllText(path,"");
                    }
                    File.AppendAllText(path,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" "+message+Environment.NewLine,Encoding.UTF8);
                } catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
        }
    }
    internal sealed class GuardContext : ApplicationContext {
        readonly string target;
        readonly LogFile log;
        readonly EventWaitHandle stopSignal;
        readonly EventWaitHandle showSignal;
        readonly SettingsStore settings;
        readonly StatusWindow window;
        readonly AutoResetEvent work=new AutoResetEvent(false);
        readonly Dictionary<uint,List<IntPtr>> hooks=new Dictionary<uint,List<IntPtr>>();
        readonly Native.WinEventProc callback;
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer discovery;
        readonly System.Windows.Forms.Timer stopTimer;
        readonly ToolStripMenuItem pauseItem, observeItem;
        readonly Thread worker;
        readonly Stopwatch clock=Stopwatch.StartNew();
        volatile bool stopping,paused,observe;
        volatile bool running,connected,failed;
        volatile string failure;
        internal bool RestartRequested;
        internal bool ObserveOnly {get{return observe;}}
        internal bool IsPaused {get{return paused;}}
        long activeScan,lastHealth;
        long lastWork;
        long winEvents,uiaEvents,completedScans,lastScanMs;
        internal GuardContext(string path,bool observeOnly,bool initiallyPaused,LogFile logger,EventWaitHandle stopEvent,EventWaitHandle showEvent,SettingsStore store,bool showWindow) {
            target=path; observe=observeOnly;paused=initiallyPaused; log=logger; stopSignal=stopEvent;showSignal=showEvent;settings=store;
            callback=delegate(IntPtr hook,uint ev,IntPtr hwnd,int obj,int child,uint tid,uint time) {Interlocked.Increment(ref winEvents);if(!stopping && !paused) work.Set();};
            var menu=new ContextMenuStrip();
            pauseItem=new ToolStripMenuItem("暂停拦截");
            pauseItem.Checked=paused;
            pauseItem.Click+=delegate {TogglePause();};
            observeItem=new ToolStripMenuItem("只观察，不关闭"); observeItem.Checked=observe;
            observeItem.Click+=delegate {observe=!observe;observeItem.Checked=observe;log.Write(observe?"MODE observe":"MODE close");UpdateTray();work.Set();};
            menu.Items.Add(pauseItem);menu.Items.Add(observeItem);
            menu.Items.Add("打开状态窗口",null,delegate {window.Present();});
            menu.Items.Add("打开运行日志",null,delegate {OpenLog();});
            menu.Items.Add("退出",null,delegate {ExitThread();});
            window=new StatusWindow(ChooseTarget,delegate {Discover();work.Set();},TogglePause,OpenLog,delegate {ExitThread();});
            tray=new NotifyIcon {Icon=SystemIcons.Shield,ContextMenuStrip=menu,Visible=true};
            tray.DoubleClick+=delegate {window.Present();};UpdateTray();
            worker=new Thread(WorkLoop) {IsBackground=true,Name="Typeless UIA worker"};
            worker.SetApartmentState(ApartmentState.MTA); worker.Start();
            discovery=new System.Windows.Forms.Timer {Interval=5000}; discovery.Tick+=delegate {Discover();if(clock.ElapsedMilliseconds-lastHealth>=30000){lastHealth=clock.ElapsedMilliseconds;log.Write("HEALTH winevents="+Interlocked.Read(ref winEvents)+" uiaevents="+Interlocked.Read(ref uiaEvents)+" scans="+Interlocked.Read(ref completedScans)+" last_scan_ms="+Interlocked.Read(ref lastScanMs)+" paused="+paused+" observe="+observe);}};
            stopTimer=new System.Windows.Forms.Timer {Interval=500};stopTimer.Tick+=delegate {if(stopSignal.WaitOne(0)){ExitThread();return;}if(showSignal.WaitOne(0))window.Present();UpdateTray();};
            log.Write("START mode="+(observe?"observe":"close")+" pid="+Process.GetCurrentProcess().Id);
            Discover(); work.Set(); discovery.Start();stopTimer.Start();
            if(showWindow)window.Show();
        }
        void TogglePause(){paused=!paused;pauseItem.Checked=paused;log.Write(paused?"PAUSED":"RESUMED");UpdateTray();if(!paused)work.Set();}
        void OpenLog(){try{Process.Start("notepad.exe",Program.Quote(log.FilePath));}catch(Exception e){MessageBox.Show(window,"无法打开日志："+e.Message);}}
        void ChooseTarget(){
            using(var picker=new OpenFileDialog{Title="选择已安装的 Typeless.exe",Filter="Typeless 程序 (Typeless.exe)|Typeless.exe",CheckFileExists=true}){
                if(picker.ShowDialog(window)!=DialogResult.OK)return;
                if(!string.Equals(Path.GetFileName(picker.FileName),"Typeless.exe",StringComparison.OrdinalIgnoreCase)){MessageBox.Show(window,"请选择 Typeless.exe。");return;}
                try{settings.SaveTarget(picker.FileName);RestartRequested=true;ExitThread();}
                catch(Exception e){MessageBox.Show(window,"无法保存设置："+e.Message);}
            }
        }
        void UpdateTray() {
            long started=Interlocked.Read(ref activeScan);bool slow=started>0&&clock.ElapsedMilliseconds-started>10000;
            string state=ConnectionText.Get(File.Exists(target),paused,failed||slow,running,connected,observe);
            tray.Text="Typeless 提示助手："+state;
            string detail=!File.Exists(target)?"没有找到安装文件。点击下方按钮选择 Typeless.exe。":paused?"已停止自动关闭提示。点击暂停 / 恢复继续。":slow?"读取窗口超过 10 秒，可退出后重启助手。":failure??(!running?"请先启动 Typeless。如果它已运行，请核对程序位置及双方权限。":!connected?"按 Ctrl+Alt 显示语音窗口。若一直未连接，请核对路径；Typeless 以管理员运行时，助手也需要相同权限。":"正在监听窗口事件，匹配已知升级提示。关闭此窗口后会继续在托盘运行。");
            window.SetState(state,detail,target);
        }
        void Discover() {
            try {
                var ids=Native.Processes(target);bool changed=false;
                running=ids.Count>0;if(!running)connected=false;
                foreach(uint old in new List<uint>(hooks.Keys)) if(!ids.Contains(old)) {foreach(var h in hooks[old]) Native.UnhookWinEvent(h);hooks.Remove(old);changed=true;}
                foreach(uint pid in ids) if(!hooks.ContainsKey(pid)) {
                    var list=new List<IntPtr>();
                    // Only create/show/hide/reorder/name changes. No global keyboard hook or pixel sampling.
                    foreach(uint ev in new uint[]{0x8000,0x8002,0x8003,0x8004,0x800C}) {
                        IntPtr h=Native.SetWinEventHook(ev,ev,IntPtr.Zero,callback,pid,0,0);
                        if(h!=IntPtr.Zero) list.Add(h);
                    }
                    if(list.Count!=5) {foreach(var h in list) Native.UnhookWinEvent(h);log.Write("HOOK_FAILED pid="+pid);continue;}
                    hooks[pid]=list; changed=true;
                }
                if(changed)log.Write("TARGETS processes="+hooks.Count);
                if(changed||(!connected&&running&&!paused))work.Set();
            } catch(Exception e) {failure="进程检测失败："+e.GetType().Name;failed=true;log.Write("DISCOVERY_FAILED "+e.GetType().Name);}
        }
        void WorkLoop() {
            using(var engine=new GuardEngine(target,log.Write,delegate {Interlocked.Increment(ref uiaEvents);if(!stopping && !paused)work.Set();},delegate {return !stopping && !paused && !observe;})) {
                try {
                    while(!stopping) {
                        work.WaitOne(); if(stopping)break; if(paused)continue;
                        long wait=Math.Max(200,750-(clock.ElapsedMilliseconds-lastWork));
                        Thread.Sleep((int)wait); if(stopping)break;if(paused)continue;
                        work.Reset(); lastWork=clock.ElapsedMilliseconds;
                        var scanClock=Stopwatch.StartNew();
                        Interlocked.Exchange(ref activeScan,clock.ElapsedMilliseconds+1);
                        engine.Scan(true,false);connected=engine.Connected;failure=engine.LastError;failed=failure!=null;
                        Interlocked.Exchange(ref activeScan,0);
                        Interlocked.Exchange(ref lastScanMs,scanClock.ElapsedMilliseconds);Interlocked.Increment(ref completedScans);
                        if(scanClock.ElapsedMilliseconds>2000)log.Write("SLOW_SCAN ms="+scanClock.ElapsedMilliseconds);
                    }
                } catch(Exception e) {failure="窗口监听失败，请重启助手："+e.GetType().Name;failed=true;connected=false;log.Write("WORKER_FAILED "+e.GetType().Name);}
                finally {log.Write("STATS scans="+engine.Scans+" matches="+engine.Matches+" confirmed="+engine.Confirmed);}
            }
        }
        protected override void ExitThreadCore() {
            if(stopping)return;stopping=true; discovery.Stop();stopTimer.Stop();
            foreach(var list in hooks.Values)foreach(var h in list)Native.UnhookWinEvent(h);
            hooks.Clear();work.Set();worker.Join(1500);
            log.Write("STOP");tray.Visible=false;tray.Dispose();discovery.Dispose();stopTimer.Dispose();
            window.AllowClose=true;window.Close();window.Dispose();
            base.ExitThreadCore();
        }
    }
}
