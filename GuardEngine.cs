using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;

namespace TypelessAdGuard {
    internal sealed class GuardEngine : IDisposable {
        readonly string target;
        readonly Action<string> log;
        readonly Action wake;
        readonly Func<bool> mayAct;
        readonly Dictionary<IntPtr,AutomationElement> subscriptions=new Dictionary<IntPtr,AutomationElement>();
        readonly StructureChangedEventHandler structure;
        readonly AutomationEventHandler tooltipOpened;
        readonly AutomationPropertyChangedEventHandler propertyChanged;
        readonly Dictionary<string,long> cooldown=new Dictionary<string,long>();
        readonly Stopwatch clock=Stopwatch.StartNew();
        long lastDiagnostic=-60000;
        internal int Scans, Matches, Confirmed;
        internal bool Connected;
        internal string LastError;
        internal GuardEngine(string path,Action<string> logger,Action notify,Func<bool> allowed) {
            target=path; log=logger; wake=notify; mayAct=allowed;
            structure=delegate(object sender,StructureChangedEventArgs e) { wake(); };
            tooltipOpened=delegate(object sender,AutomationEventArgs e) { wake(); };
            propertyChanged=delegate(object sender,AutomationPropertyChangedEventArgs e) { wake(); };
        }
        internal void Scan(bool subscribe,bool diagnostic) {
            Scans++;
            Connected=false;LastError=null;
            var windows=Native.StatusWindows(target);
            if(diagnostic) log("PROBE status_windows="+windows.Count);
            foreach(var old in new List<IntPtr>(subscriptions.Keys)) {
                if(!windows.Contains(old)) Unsubscribe(old);
            }
            foreach(IntPtr hwnd in windows) {
                try {
                    AutomationElement root=AutomationElement.FromHandle(hwnd);
                    if(subscribe && !subscriptions.ContainsKey(hwnd)) {
                        subscriptions.Add(hwnd,root);
                        try {
                            Automation.AddStructureChangedEventHandler(root,TreeScope.Subtree,structure);
                            Automation.AddAutomationEventHandler(AutomationElement.ToolTipOpenedEvent,root,TreeScope.Subtree,tooltipOpened);
                            Automation.AddAutomationPropertyChangedEventHandler(root,TreeScope.Subtree,propertyChanged,AutomationElement.NameProperty,AutomationElement.IsOffscreenProperty);
                            log("ATTACHED hwnd="+hwnd);
                        } catch(Exception e) { Unsubscribe(hwnd); Diagnostic("SUBSCRIBE_FAILED "+e.GetType().Name); }
                    }
                    if(!Native.IsWindowVisible(hwnd)) { if(diagnostic) log("PROBE status_hidden hwnd="+hwnd); continue; }
                    var nodes=ReadTooltips(root);
                    if(nodes==null) {LastError="界面结构尚未就绪或与支持的版本不同";Diagnostic("SKIP document_scope_or_result_limit");continue;}
                    Connected=!subscribe||subscriptions.ContainsKey(hwnd);
                    int count=0;
                    foreach(var node in nodes) {
                        if(node.Cached.ControlType!=ControlType.ToolTip) continue;
                        count++;
                        log("TOOLTIP detected hwnd="+hwnd+" offscreen="+node.Cached.IsOffscreen);
                        var candidate=Candidate(node,hwnd,true);
                        if(candidate==null) continue;
                        Matches++;
                        if(!mayAct()) { Diagnostic("MATCH observe_only"); continue; }
                        string key=hwnd+":"+string.Join(".",Array.ConvertAll(node.GetRuntimeId(),x=>x.ToString()));
                        long previous;
                        if(cooldown.TryGetValue(key,out previous) && clock.ElapsedMilliseconds-previous<10000) continue;
                        if(cooldown.Count>128) cooldown.Clear();
                        cooldown[key]=clock.ElapsedMilliseconds;
                        bool gone=false;
                        // A transient failure gets at most three local attempts, not an endless polling loop.
                        for(int attempt=0;attempt<3 && !gone;attempt++) {
                            if(attempt>0)Thread.Sleep(400);
                            candidate=Candidate(node,hwnd,false);
                            if(candidate==null || !mayAct()) break;
                            object pattern;
                            if(!candidate.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)) {log("SKIP invoke_unavailable");break;}
                            try {
                                ((InvokePattern)pattern).Invoke();
                                log("INVOKE requested hwnd="+hwnd+" attempt="+(attempt+1));
                            } catch(Exception e) {log("INVOKE_FAILED "+e.GetType().Name);continue;}
                            for(int retry=0;retry<4;retry++) {
                                Thread.Sleep(125);
                                gone=IsGone(node,root)==true;
                                if(gone)break;
                            }
                        }
                        if(gone && Native.Trusted(hwnd,target) && Native.Title(hwnd)=="Status") {
                            Confirmed++; log("CONFIRMED promotion_gone status_preserved hwnd="+hwnd);
                        } else log("UNCONFIRMED verify_on_next_real_ad");
                    }
                    if(diagnostic) log("PROBE tooltips="+count);
                } catch(Exception e) {LastError="无法读取窗口（"+e.GetType().Name+"）";Diagnostic("SCAN_FAILED "+e.GetType().Name);}
            }
        }
        static bool? IsGone(AutomationElement tooltip,AutomationElement root) {
            int[] id;
            try {
                if(tooltip.Current.IsOffscreen)return true;
                id=tooltip.GetRuntimeId();
            } catch(ElementNotAvailableException) {return true;}
            catch(Exception) {return null;}
            try {
                var nodes=ReadTooltips(root);
                if(nodes==null)return null;
                foreach(var node in nodes) if(Automation.Compare(node.GetRuntimeId(),id))return false;
                // A scoped search can miss a relocated control. Absence here is unknown, not disappearance.
                return null;
            } catch(Exception) {return null;}
        }
        static List<AutomationElement> ReadTooltips(AutomationElement root) {
            // Status exposes a direct Document plus a slow duplicate native-window branch.
            // Search the complete Document regardless of Custom wrapper depth, ignoring that duplicate branch.
            var cache=new CacheRequest();cache.TreeScope=TreeScope.Element;
            cache.Add(AutomationElement.ControlTypeProperty);cache.Add(AutomationElement.IsOffscreenProperty);
            var result=new List<AutomationElement>();
            using(cache.Activate()) {
                var documents=root.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
                if(documents.Count!=1)return null;
                var nodes=documents[0].FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ToolTip));
                if(nodes.Count>16)return null;
                foreach(AutomationElement node in nodes)result.Add(node);
            }
            return result;
        }
        AutomationElement Candidate(AutomationElement tooltip,IntPtr hwnd,bool diagnostic) {
            if(!Native.Trusted(hwnd,target) || Native.Title(hwnd)!="Status") return null;
            var state=tooltip.Current;
            if(state.ControlType!=ControlType.ToolTip || state.IsOffscreen) {if(diagnostic)log("REJECT tooltip_offscreen_or_type");return null;}
            var nodes=ReadTree(tooltip,48); if(nodes==null) return null;
            var texts=new List<string>(); var infos=new List<ButtonInfo>(); var buttons=new List<AutomationElement>();
            foreach(var node in nodes) {
                var p=node.Cached;
                if(p.ControlType==ControlType.Text) texts.Add(p.Name);
                if(p.ControlType==ControlType.Edit) return null;
                if(p.ControlType==ControlType.Button) {
                    bool invoke=(bool)node.GetCachedPropertyValue(AutomationElement.IsInvokePatternAvailableProperty);
                    infos.Add(new ButtonInfo(p.Name,p.IsEnabled,!p.IsOffscreen,invoke)); buttons.Add(node);
                }
            }
            int selected=AdPolicy.SelectClose(true,"Status",true,true,texts,infos);
            if(diagnostic) {
                log("PROBE tooltip buttons="+buttons.Count+" exact_match="+(selected>=0)+" promotion="+(AdPolicy.PromotionId(texts)??"unknown"));
                for(int i=0;i<infos.Count;i++) {
                    var info=infos[i];
                    log("BUTTON index="+i+" unnamed="+(info.Name=="")+" upgrade="+(info.Name=="Upgrade")+" enabled="+info.Enabled+" visible="+info.Visible+" invoke="+info.CanInvoke);
                }
            }
            return selected>=0?buttons[selected]:null;
        }
        static List<AutomationElement> ReadTree(AutomationElement root,int limit) {
            // Batch properties across the process boundary. No per-node screen capture or OCR.
            var cache=new CacheRequest();cache.TreeScope=TreeScope.Element;
            cache.Add(AutomationElement.ControlTypeProperty);cache.Add(AutomationElement.NameProperty);
            cache.Add(AutomationElement.IsOffscreenProperty);cache.Add(AutomationElement.IsEnabledProperty);
            cache.Add(AutomationElement.IsInvokePatternAvailableProperty);
            var result=new List<AutomationElement>();
            using(cache.Activate()) {
                var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
                if(nodes.Count>limit)return null;
                foreach(AutomationElement node in nodes)result.Add(node);
            }
            return result;
        }
        void Diagnostic(string value) {if(clock.ElapsedMilliseconds-lastDiagnostic<10000) return; lastDiagnostic=clock.ElapsedMilliseconds;log(value);}
        void Unsubscribe(IntPtr hwnd) {
            AutomationElement root=subscriptions[hwnd]; subscriptions.Remove(hwnd);
            try {Automation.RemoveStructureChangedEventHandler(root,structure);} catch(Exception) {}
            try {Automation.RemoveAutomationEventHandler(AutomationElement.ToolTipOpenedEvent,root,tooltipOpened);} catch(Exception) {}
            try {Automation.RemoveAutomationPropertyChangedEventHandler(root,propertyChanged);} catch(Exception) {}
        }
        public void Dispose() {foreach(var hwnd in new List<IntPtr>(subscriptions.Keys)) Unsubscribe(hwnd);}
    }
}
