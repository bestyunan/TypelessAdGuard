using System;
using System.Drawing;
using System.Windows.Forms;

namespace TypelessAdGuard {
    internal sealed class StatusWindow : Form {
        readonly Label state,details;
        readonly TextBox target;
        internal bool AllowClose;
        internal StatusWindow(Action choose,Action refresh,Action pause,Action openLog,Action quit) {
            Text="Typeless 提示助手 · 0.1.0 预览版";
            ClientSize=new Size(540,330);MinimumSize=new Size(550,365);StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Microsoft YaHei UI",9F);AutoScaleMode=AutoScaleMode.Dpi;
            state=new Label{Left=20,Top=18,Width=500,Height=38,Font=new Font(Font.FontFamily,16F,FontStyle.Bold)};
            details=new Label{Left=20,Top=60,Width=500,Height=76};
            var caption=new Label{Left=20,Top=139,Width=490,Height=24,Text="Typeless 程序位置"};
            target=new TextBox{Left=20,Top=165,Width=500,Height=48,ReadOnly=true,Multiline=true,WordWrap=true};
            Controls.AddRange(new Control[]{state,details,caption,target});
            AddButton("选择 Typeless…",20,224,140,choose);AddButton("重新检测",170,224,105,refresh);
            AddButton("暂停 / 恢复",285,224,115,pause);AddButton("打开日志",410,224,110,openLog);
            AddButton("后台运行",285,274,115,Hide);AddButton("退出助手",410,274,110,quit);
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!AllowClose&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
        }
        void AddButton(string text,int x,int y,int width,Action action){
            var b=new Button{Text=text,Left=x,Top=y,Width=width,Height=34};b.Click+=delegate{action();};Controls.Add(b);
        }
        internal void SetState(string text,string explanation,string path){state.Text=text;details.Text=explanation;target.Text=path??"未选择";}
        internal void Present(){Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();}
    }
}
