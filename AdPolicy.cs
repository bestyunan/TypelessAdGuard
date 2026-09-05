using System;
using System.Collections.Generic;

namespace TypelessAdGuard {
    public sealed class ButtonInfo {
        public string Name;
        public bool Enabled, Visible, CanInvoke;
        public ButtonInfo(string name, bool enabled, bool visible, bool canInvoke) {
            Name = name; Enabled = enabled; Visible = visible; CanInvoke = canInvoke;
        }
    }
    public static class AdPolicy {
        public const string Title = "Upgrade for enhanced accuracy";
        public const string Body = "Upgrade to Typeless Pro for unlimited words, enhanced accuracy, and priority access during high demand.";
        public const string DemandTitle = "High demand";
        public const string DemandBody = "Typeless is busier than usual right now. Upgrade to Typeless Pro to get priority access.";
        public static string PromotionId(IList<string> texts) {
            if(texts==null)return null;
            if(texts.Contains(Title) && texts.Contains(Body))return "accuracy";
            if(texts.Contains(DemandTitle) && texts.Contains(DemandBody))return "high-demand";
            return null;
        }
        public static int SelectClose(bool trusted, string windowTitle, bool isTooltip, bool visible,
            IList<string> texts, IList<ButtonInfo> buttons) {
            if (!trusted || windowTitle != "Status" || !isTooltip || !visible ||
                texts == null || buttons == null || buttons.Count != 2) return -1;
            if (PromotionId(texts)==null) return -1;
            int close = -1, upgrade = 0;
            for (int i = 0; i < buttons.Count; i++) {
                ButtonInfo b = buttons[i];
                if (b.Name == "Upgrade") { upgrade++; continue; }
                if (b.Name != "" || close != -1 || !b.Enabled || !b.Visible || !b.CanInvoke) return -1;
                close = i;
            }
            return upgrade == 1 ? close : -1;
        }
    }
}
