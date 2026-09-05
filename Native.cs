using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TypelessAdGuard {
    internal static class Native {
        internal delegate bool EnumProc(IntPtr hwnd, IntPtr data);
        internal delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint pid,uint thread,uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder text,ref int count);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        internal static string Title(IntPtr hwnd) { var b=new StringBuilder(512); GetWindowText(hwnd,b,b.Capacity); return b.ToString(); }
        internal static string PathFor(uint pid) {
            IntPtr h=OpenProcess(0x1000,false,pid); if(h==IntPtr.Zero) return null;
            try {var b=new StringBuilder(32768); int n=b.Capacity; return QueryFullProcessImageName(h,0,b,ref n)?b.ToString():null;}
            finally {CloseHandle(h);}
        }
        internal static bool Trusted(IntPtr hwnd,string path) {
            if(string.IsNullOrWhiteSpace(path))return false;
            uint pid; GetWindowThreadProcessId(hwnd,out pid);
            return IsWindow(hwnd) && string.Equals(PathFor(pid),path,StringComparison.OrdinalIgnoreCase);
        }
        internal static HashSet<uint> Processes(string path) {
            var ids=new HashSet<uint>();
            if(string.IsNullOrWhiteSpace(path))return ids;
            foreach(var p in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(path))) {
                using(p) { try { if(string.Equals(PathFor((uint)p.Id),path,StringComparison.OrdinalIgnoreCase)) ids.Add((uint)p.Id); } catch(InvalidOperationException) {} }
            }
            return ids;
        }
        internal static List<string> RunningInstallations() {
            var paths=new List<string>();
            foreach(var p in Process.GetProcessesByName("Typeless"))using(p){
                try {string path=PathFor((uint)p.Id);if(path!=null)paths.Add(path);}catch(InvalidOperationException){}
            }
            return paths;
        }
        internal static List<IntPtr> StatusWindows(string path) {
            var ids=Processes(path); var result=new List<IntPtr>();
            EnumWindows(delegate(IntPtr hwnd,IntPtr unused) {uint pid; GetWindowThreadProcessId(hwnd,out pid);
                if(ids.Contains(pid) && Title(hwnd)=="Status") result.Add(hwnd); return true;
            },IntPtr.Zero);
            return result;
        }
    }
}
