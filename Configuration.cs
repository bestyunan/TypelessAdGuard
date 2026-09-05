using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TypelessAdGuard {
    internal sealed class SettingsStore {
        readonly string directory;
        internal SettingsStore(string folder){directory=folder;}
        internal void SaveTarget(string path){
            Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"target-path.txt"),temp=file+"."+Guid.NewGuid().ToString("N");
            File.WriteAllText(temp,path,Encoding.UTF8);
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
        internal string LoadTarget(){
            try {string p=File.ReadAllText(Path.Combine(directory,"target-path.txt"),Encoding.UTF8).Trim();return p.Length==0?null:p;}
            catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
        }
    }
    internal static class TargetResolver {
        internal static string Resolve(string explicitPath,string saved,IEnumerable<string> running,IEnumerable<string> defaults,Func<string,bool> exists){
            if(explicitPath!=null)return explicitPath;
            if(saved!=null&&exists(saved))return saved;
            var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string p in running)if(p!=null&&exists(p))unique.Add(p);
            if(unique.Count>1)return null;
            foreach(string p in unique)return p;
            foreach(string p in defaults)if(p!=null&&exists(p))return p;
            return null;
        }
    }
    internal static class ConnectionText {
        internal static string Get(bool targetExists,bool paused,bool failed,bool running,bool connected,bool observe){
            if(!targetExists)return "未找到 Typeless";
            if(paused)return "已暂停";
            if(failed)return "连接异常";
            if(!running)return "等待 Typeless 启动";
            if(!connected)return "等待语音窗口 / 检查权限";
            return observe?"已连接 · 只观察":"已连接";
        }
    }
    internal static class LaunchArguments {
        internal static string Quote(string value){
            var b=new StringBuilder("\"");int slashes=0;
            foreach(char c in value){if(c=='\\'){slashes++;continue;}b.Append('\\',c=='\"'?slashes*2+1:slashes);b.Append(c);slashes=0;}
            b.Append('\\',slashes*2);b.Append('"');return b.ToString();
        }
        internal static string Restart(string dataDir,bool observe,bool paused){return "--data-dir "+Quote(dataDir)+(observe?" --observe":"")+(paused?" --paused":"");}
    }
}
