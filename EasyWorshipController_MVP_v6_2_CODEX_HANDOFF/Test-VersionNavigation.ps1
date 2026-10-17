param([switch]$VerifyLocalCatalogue)
$ErrorActionPreference='Stop'
$main=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start=$main.IndexOf('    internal sealed class BibleVersionPosition')
$state=$main.Substring($start,$main.IndexOf('    internal sealed class EasyWorshipAutomation')-$start)
$tests=@'
public static class VersionNavigationTests
{
    static int checks,focus,settle;
    static System.Collections.Generic.List<int> keys=new System.Collections.Generic.List<int>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static bool Move(BibleVersionPosition p,string code,int index){focus=settle=0;keys.Clear();return p.Navigate(code,index,delegate{focus++;},delegate(int key){keys.Add(key);},delegate{settle++;});}
    public static string Run()
    {
        BibleVersionPosition p=new BibleVersionPosition();p.Configure(9,"map1");p.Bind("process1");
        Check(p.Matches("KJV",9) && p.Summary.Contains("assumed"),"Startup must use mapped KJV index, not zero");
        Check(!Move(p,"kjv",9) && focus==0 && keys.Count==0 && settle==0,"Same version touched list");
        Move(p,"MSG",11);Check(focus==1 && keys.Count==2 && keys.TrueForAll(k=>k==1),"KJV to MSG must move down twice");
        Check(p.Matches("MSG",11),"Successful move not tracked");
        Check(p.Tracked,"Completed navigation should be reusable for manual selection when readback is unavailable");
        p.Bind("process1");Check(!Move(p,"MSG",11) && focus==0,"Same session lost selection");
        Move(p,"KJV",9);Check(keys.Count==2 && keys.TrueForAll(k=>k==-1),"Return to KJV must move up twice");
        p.Observe("MSG",11);Move(p,"KJV",9);Check(keys.Count==2 && keys.TrueForAll(k=>k==-1),"Fresh MSG readback must move directly up two rows");
        p.Observe("KJV",9);Check(!Move(p,"KJV",9) && keys.Count==0,"Fresh matching readback must leave list untouched");
        p.Invalidate();p.Bind("process1");Check(!p.Matches("KJV",9),"Explicit reset silently assumed KJV again");
        Check(!p.Tracked,"Explicit reset must discard the manual navigation cache");
        Move(p,"KJV",9);Check(keys.Count==69 && keys.GetRange(0,60).TrueForAll(k=>k==-1) && keys.GetRange(60,9).TrueForAll(k=>k==1),"Unknown position recovery failed");
        try{p.Navigate("MSG",11,delegate{},delegate(int k){throw new OperationCanceledException();},delegate{});}catch(OperationCanceledException){}
        Check(p.Summary=="unknown","Interrupted movement retained stale selection");
        Move(p,"KJV",9);Check(keys.Count==69,"Interrupted movement did not re-anchor");
        try{p.Navigate("MSG",11,delegate{},delegate(int k){},delegate{throw new OperationCanceledException();});}catch(OperationCanceledException){}
        Check(p.Summary=="unknown","Interrupted settling retained stale selection");
        p.Bind("process2");Check(!Move(p,"KJV",9) && focus==0,"New process must seed KJV");
        Check(!p.Tracked,"An assumed startup selection must not count as tracked navigation");
        p.Configure(8,"map2");Check(p.Summary=="unknown","Map change retained old index");
        Move(p,"KJV",8);Check(keys.Count==68,"New map index not used");
        p.Navigate("MSG",10,delegate{},delegate(int k){},delegate{p.Invalidate();});
        Check(p.Summary=="unknown","Concurrent reset overwritten by completion");
        BibleVersionPosition missing=new BibleVersionPosition();missing.Configure(-1,"missing");missing.Bind("process3");
        Check(missing.Summary=="unknown","Missing KJV mapping assumed a position");
        Move(missing,"MSG",2);Check(keys.Count==62,"Missing default failed recovery");
        BibleVersionPosition reset=new BibleVersionPosition();reset.Configure(9,"map");reset.Invalidate();reset.Bind("process4");
        Check(reset.Summary=="unknown","Reset before first attachment ignored");
        bool rejected=false;try{Move(reset,"bad",-1);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected && focus==0 && keys.Count==0,"Invalid index touched list");
        return checks+" version navigation checks passed; no EasyWorship commands sent.";
    }
}
'@
Add-Type -TypeDefinition ('using System;'+$state+$tests)
[VersionNavigationTests]::Run()
if($VerifyLocalCatalogue){
    $expected=@('AMP','AMPC','ASV','BBE','BSB','ESV','GNT','HAU','HCSB','KJV','LBLA','MSG','NASB','NBLH','NIVA','NKJV','NLT','PIG','RST','RVA','TLV','TPT','WB','YOR')
    $map=Join-Path $PSScriptRoot 'version-map.txt'
    if(!(Test-Path -LiteralPath $map)){throw 'Local version map is missing'}
    $rows=@(Get-Content -LiteralPath $map | Where-Object {$_ -match '^[A-Z]+=[0-9]+$'})
    if($rows.Count -ne $expected.Count){throw "Expected $($expected.Count) screenshot-confirmed local rows, found $($rows.Count)"}
    for($i=0;$i -lt $expected.Count;$i++){
        if($rows[$i] -cne ($expected[$i]+'='+$i)){throw "Local catalogue differs from screenshot at index $i"}
    }
    'PASS: 24 LIGHT NATION_1 screenshot-confirmed Bible rows in abbreviation order at indexes 0-23.'
}
