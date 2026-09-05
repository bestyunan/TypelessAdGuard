using System;
using System.Collections.Generic;
using TypelessAdGuard;
class PolicyTests {
    static int failures, total;
    static void Check(string name, int actual, int expected) {
        total++; if(actual != expected) {failures++; Console.WriteLine("FAIL " + name + " expected=" + expected + " actual=" + actual);}
        else Console.WriteLine("PASS " + name);
    }
    static List<string> Texts() {return new List<string>{AdPolicy.Title,AdPolicy.Body,"Upgrade"};}
    static List<ButtonInfo> Buttons() {return new List<ButtonInfo>{new ButtonInfo("",true,true,true),new ButtonInfo("Upgrade",true,true,true)};}
    static int Main() {
        var demand=new List<string>{"High demand","Typeless is busier than usual right now. Upgrade to Typeless Pro to get priority access.","Upgrade"};
        Check("high demand promotion",AdPolicy.SelectClose(true,"Status",true,true,demand,Buttons()),0);
        var mixed=new List<string>{"High demand",AdPolicy.Body,"Upgrade"};
        Check("titles and bodies cannot be mixed",AdPolicy.SelectClose(true,"Status",true,true,mixed,Buttons()),-1);
        var generic=new List<string>{"High demand","Please wait for your transcription.","Upgrade"};
        Check("high demand status alone is not promotion",AdPolicy.SelectClose(true,"Status",true,true,generic,Buttons()),-1);
        Check("observed promotion closes only unnamed button",AdPolicy.SelectClose(true,"Status",true,true,Texts(),Buttons()),0);
        var swapped=Buttons(); swapped.Reverse();
        Check("button order is not hardcoded",AdPolicy.SelectClose(true,"Status",true,true,Texts(),swapped),1);
        Check("other application",AdPolicy.SelectClose(false,"Status",true,true,Texts(),Buttons()),-1);
        Check("main window",AdPolicy.SelectClose(true,"Typeless",true,true,Texts(),Buttons()),-1);
        Check("voice pane is not tooltip",AdPolicy.SelectClose(true,"Status",false,true,Texts(),Buttons()),-1);
        Check("hidden promotion",AdPolicy.SelectClose(true,"Status",true,false,Texts(),Buttons()),-1);
        Check("voice state without promotion",AdPolicy.SelectClose(true,"Status",true,true,new List<string>(),Buttons()),-1);
        var changed=Texts(); changed[1]="Different promotion";
        Check("changed body",AdPolicy.SelectClose(true,"Status",true,true,changed,Buttons()),-1);
        var extra=Buttons(); extra.Add(new ButtonInfo("",true,true,true));
        Check("ambiguous extra button",AdPolicy.SelectClose(true,"Status",true,true,Texts(),extra),-1);
        var disabled=Buttons(); disabled[0].Enabled=false;
        Check("disabled close",AdPolicy.SelectClose(true,"Status",true,true,Texts(),disabled),-1);
        var hidden=Buttons(); hidden[0].Visible=false;
        Check("hidden close",AdPolicy.SelectClose(true,"Status",true,true,Texts(),hidden),-1);
        var unsupported=Buttons(); unsupported[0].CanInvoke=false;
        Check("no Invoke support",AdPolicy.SelectClose(true,"Status",true,true,Texts(),unsupported),-1);
        var wrong=Buttons(); wrong[0].Name="Delete";
        Check("unexpected button label",AdPolicy.SelectClose(true,"Status",true,true,Texts(),wrong),-1);
        Console.WriteLine(total + " cases; " + failures + " failures"); return failures==0?0:1;
    }
}
