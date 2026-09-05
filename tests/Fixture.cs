using System;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

class PromotionPanel : StackPanel {
    protected override AutomationPeer OnCreateAutomationPeer() {return new PromotionPeer(this);}
}
class DocumentPanel : StackPanel {
    protected override AutomationPeer OnCreateAutomationPeer(){return new DocumentPeer(this);}
}
class DocumentPeer : FrameworkElementAutomationPeer {
    internal DocumentPeer(DocumentPanel owner):base(owner){}
    protected override AutomationControlType GetAutomationControlTypeCore(){return AutomationControlType.Document;}
    protected override bool IsControlElementCore(){return true;}
}
class WrapperPanel : StackPanel {
    protected override AutomationPeer OnCreateAutomationPeer(){return new WrapperPeer(this);}
}
class WrapperPeer : FrameworkElementAutomationPeer {
    internal WrapperPeer(WrapperPanel owner):base(owner){}
    protected override AutomationControlType GetAutomationControlTypeCore(){return AutomationControlType.Custom;}
    protected override bool IsControlElementCore(){return true;}
}
class PromotionPeer : FrameworkElementAutomationPeer {
    internal PromotionPeer(PromotionPanel owner):base(owner) {}
    protected override AutomationControlType GetAutomationControlTypeCore(){return AutomationControlType.ToolTip;}
    protected override bool IsControlElementCore(){return true;}
    protected override string GetNameCore(){return "Upgrade for enhanced accuracy";}
}
class Fixture {
    [STAThread] static void Main(string[] args) {
        string output=args[0];bool changed=args[1]=="changed",blocked=args[1]=="blocked",demand=args[1]=="high-demand";
        var app=new Application();
        var root=new DocumentPanel();
        var voice=new TextBlock{Text="Synthetic voice pane - must remain",Margin=new Thickness(8)};
        root.Children.Add(voice);
        var win=new Window{Title="Status",Width=450,Height=260,ShowActivated=false,ShowInTaskbar=false,Left=30,Top=30,Content=root};
        bool closed=false,upgrade=false;
        var show=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};
        show.Tick+=delegate {
            show.Stop();
            StackPanel holder=root;
            if(args[1]=="deep") for(int i=0;i<6;i++){var wrapper=new WrapperPanel();holder.Children.Add(wrapper);holder=wrapper;}
            var tip=new PromotionPanel();
            var close=new Button{Width=25,Height=25,HorizontalAlignment=HorizontalAlignment.Right};
            close.Click+=delegate {
                if(blocked){close.IsEnabled=false;File.AppendAllText(output,"REQUEST_IGNORED\n");return;}
                holder.Children.Remove(tip);closed=true;File.AppendAllText(output,"CLOSE\n");
            };
            tip.Children.Add(close);
            tip.Children.Add(new TextBlock{Text=demand?"High demand":"Upgrade for enhanced accuracy"});
            tip.Children.Add(new TextBlock{Text=changed?"An unrelated notice":demand?"Typeless is busier than usual right now. Upgrade to Typeless Pro to get priority access.":"Upgrade to Typeless Pro for unlimited words, enhanced accuracy, and priority access during high demand.",TextWrapping=TextWrapping.Wrap});
            var buy=new Button{Content="Upgrade"};
            buy.Click+=delegate{upgrade=true;File.AppendAllText(output,"WRONG_UPGRADE\n");};
            tip.Children.Add(buy);holder.Children.Add(tip);
            File.AppendAllText(output,"SHOWN\n");
        };
        var done=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};
        done.Tick+=delegate {
            done.Stop();
            bool preserved=root.Children.Contains(voice) && win.IsVisible;
            File.AppendAllText(output,(preserved && !upgrade && closed==(!changed&&!blocked)?"PASS":"FAIL")+" closed="+closed+" voice="+preserved+" upgrade="+upgrade+"\n");
            app.Shutdown();
        };
        win.ContentRendered+=delegate{File.AppendAllText(output,"READY\n");show.Start();done.Start();};
        app.Run(win);
    }
}
