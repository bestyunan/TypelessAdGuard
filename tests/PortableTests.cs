using System;
using System.Collections.Generic;
using System.IO;
using TypelessAdGuard;
class PortableTests {
    static int failed,total;
    static void Equal(string label,string expected,string actual){total++;if(expected!=actual){failed++;Console.WriteLine("FAIL "+label+" expected="+expected+" actual="+actual);}else Console.WriteLine("PASS "+label);}
    static int Main(){
        Equal("restart preserves observe and pause","--data-dir \"D:\\test data\" --observe --paused",LaunchArguments.Restart("D:\\test data",true,true));
        Equal("normal restart does not invent flags","--data-dir \"D:\\test data\"",LaunchArguments.Restart("D:\\test data",false,false));
        var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-test-"+Guid.NewGuid().ToString("N"));
        var store=new SettingsStore(folder);
        Equal("no configuration",null,store.LoadTarget());
        string path="D:\\中文 软件\\Typeless\\Typeless.exe";
        store.SaveTarget(path);Equal("unicode custom path persists",path,new SettingsStore(folder).LoadTarget());
        store.SaveTarget("E:\\Apps\\Typeless.exe");Equal("replace settings","E:\\Apps\\Typeless.exe",store.LoadTarget());
        var none=new string[0];Func<string,bool> exists=p=>p!=null&&p!="missing";
        Equal("explicit target never silently replaced","missing",TargetResolver.Resolve("missing","saved",none,new[]{"default"},exists));
        Equal("saved path wins","saved",TargetResolver.Resolve(null,"saved",new[]{"running"},new[]{"default"},exists));
        Equal("running install discovered","running",TargetResolver.Resolve(null,"missing",new[]{"running","running"},new[]{"default"},exists));
        Equal("ambiguous running installs need selection",null,TargetResolver.Resolve(null,null,new[]{"one","two"},new[]{"default"},exists));
        Equal("standard installation fallback","default",TargetResolver.Resolve(null,null,none,new[]{"missing","default"},exists));
        Equal("no installation",null,TargetResolver.Resolve(null,null,none,new[]{"missing"},exists));
        Equal("missing path status","未找到 Typeless",ConnectionText.Get(false,false,false,false,false,false));
        Equal("paused status","已暂停",ConnectionText.Get(true,true,false,true,true,false));
        Equal("worker error status","连接异常",ConnectionText.Get(true,false,true,true,true,false));
        Equal("waiting for target","等待 Typeless 启动",ConnectionText.Get(true,false,false,false,false,false));
        Equal("process is not confirmed connection","等待语音窗口 / 检查权限",ConnectionText.Get(true,false,false,true,false,false));
        Equal("connected","已连接",ConnectionText.Get(true,false,false,true,true,false));
        Equal("observe mode","已连接 · 只观察",ConnectionText.Get(true,false,false,true,true,true));
        Console.WriteLine(total+" cases; "+failed+" failures");return failed==0?0:1;
    }
}
