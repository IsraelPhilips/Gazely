param([switch]$VerifyLocalCatalogue)
$ErrorActionPreference='Stop'
$main=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$request=$main.Substring($main.IndexOf('    internal sealed class ScriptureRequest'),$main.IndexOf('    internal sealed class VersionEntry')-$main.IndexOf('    internal sealed class ScriptureRequest'))
$adapters=Get-Content (Join-Path $PSScriptRoot 'PresentationAdapters.cs') -Raw -Encoding UTF8
$adapters=$adapters.Substring(0,$adapters.IndexOf('    internal sealed class EasyWorship7Adapter'))
$tests=@'
    internal sealed class FakeAdapter : IPresentationAdapter
    {
        public string Id { get { return "fake"; } }
        public string Name { get { return "Fake presentation destination"; } }
        public PresentationCapabilities Supported=PresentationCapabilities.Scripture|PresentationCapabilities.Prepare|PresentationCapabilities.Present|PresentationCapabilities.VerseRanges;
        public PresentationCapabilities Capabilities { get { return Supported; } }
        public bool Connected=true,Fail=false,Wait=false;
        public int Attempts=0,Sent=0,Resets=0;
        public readonly System.Threading.ManualResetEvent Entered=new System.Threading.ManualResetEvent(false);
        public string LastReference="";
        public bool Probe() { return Connected; }
        public void ResetSession() { Resets++; }
        public PresentationResult Execute(PresentationIntent intent,Func<bool> canceled)
        {
            Attempts++;Entered.Set();
            while(Wait && !canceled()) System.Threading.Thread.Sleep(1);
            if(canceled()) throw new OperationCanceledException();
            if(Fail) throw new IOException("Simulated failure after attempting command");
            Sent++;LastReference=intent.Scripture.ReferenceText;
            return new PresentationResult(intent.Present?PresentationOutcome.SentUnverified:PresentationOutcome.Prepared,"Fake result");
        }
    }
    public static class AdapterTests
    {
        private static int count;
        private static void Check(bool condition,string message) { if(!condition) throw new Exception(message);count++; }
        private static PresentationIntent Intent(string id,bool live)
        { return new PresentationIntent(id,new ScriptureRequest{Book="Genesis",Chapter=49,Verse=3,VerseEnd=4},live,true,"KJV",9); }
        public static string Run(string directory,string localBibleFolder)
        {
            string[] unavailable;string unavailableReport;
            Check(!LocalBibleCatalogue.TryDiscover(Path.Combine(directory,"missing-bibles"),out unavailable,out unavailableReport),"Missing Bible folder was accepted");
            if(localBibleFolder.Length>0){
                string[] discovered;string report;
                Check(LocalBibleCatalogue.TryDiscover(localBibleFolder,out discovered,out report),"Local Bible headers were not read: "+report);
                string expected="AMP,AMPC,ASV,BBE,BSB,ESV,GNT,HAU,HCSB,KJV,LBLA,MSG,NASB,NBLH,NIVA,NKJV,NLT,PIG,RST,RVA,TLV,TPT,WB,YOR";
                Check(string.Join(",",discovered)==expected,"Header abbreviations differ from the screenshot-confirmed 24-row library");
            }
            var observation=new VersionSelectionObservation{Code="MSG",DisplayName="The Message",EvidenceSource="test",ProcessIdentity="p1",ProfileIdentity="profile",CatalogueRevision="r1",Position=2,ObservedUtc=DateTime.UtcNow,Verified=true};
            Check(observation.Matches("msg","p1","r1"),"Verified version did not match");
            Check(!observation.Matches("KJV","p1","r1") && !observation.Matches("MSG","p2","r1") && !observation.Matches("MSG","p1","r2"),"Stale or wrong version observation accepted");
            var entry=new VersionCatalogueEntry{Code="MSG",DisplayName="The Message",EvidenceSource="calibration",Position=2};
            Check(entry.Position==observation.Position,"Catalogue position mismatch");
            string local=VersionCatalogueSetup.BuildMap("KJV\r\nMSG\r\nNLT");
            var parsed=VersionCatalogueSetup.ParseMap(local);
            Check(parsed.Count==3 && parsed["KJV"]==0 && parsed["MSG"]==1 && parsed["NLT"]==2,"Local displayed order was not preserved");
            Check(VersionCatalogueSetup.ParseMap("# New installations have no assumed Bible order.\r\n").Count==0,"Fresh installation seeded a version index");
            foreach(string invalid in new[]{"MSG\nMSG","KJV\nMore Available...",""}){
                bool refused=false;try{VersionCatalogueSetup.BuildMap(invalid);}catch(FormatException){refused=true;}
                Check(refused,"Invalid displayed catalogue accepted");
            }
            foreach(string invalid in new[]{"KJV=0\nMSG=0","KJV=0\nMSG=2","KJV=0\nMSG=x"}){
                bool refused=false;try{VersionCatalogueSetup.ParseMap(invalid);}catch(FormatException){refused=true;}
                Check(refused,"Invalid version map accepted");
            }
            VersionOutputPolicy.RejectObservedMismatch("MSG","msg");
            VersionOutputPolicy.RejectObservedMismatch("MSG",null);
            bool blocked=false;try{VersionOutputPolicy.RejectObservedMismatch("MSG","KJV");}catch(InvalidOperationException){blocked=true;}
            Check(blocked,"Known wrong translation allowed output");
            FakeAdapter fake=new FakeAdapter();PresentationCoordinator coordinator=new PresentationCoordinator(fake);
            Check(coordinator.Execute(Intent("disabled",true)).Outcome==PresentationOutcome.Canceled && fake.Attempts==0,"Disabled integration executed");
            coordinator.SetEnabled(true);
            Check(coordinator.Refresh()==IntegrationState.Ready,"Failed to connect");
            PresentationIntent intent=Intent("one",false);
            Check(coordinator.Execute(intent).Outcome==PresentationOutcome.Prepared && fake.LastReference=="Genesis 49:3-4","Range preparation failed");
            coordinator.Execute(intent);Check(fake.Attempts==1,"Duplicate action executed");
            bool rejected=false;try { coordinator.Execute(Intent("one",true)); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected,"Reused intent ID accepted different content");
            Check(coordinator.Execute(Intent("live",true)).Outcome==PresentationOutcome.SentUnverified,"Unverified live command reported verified");
            fake.Connected=false;
            Check(coordinator.Refresh()==IntegrationState.Disconnected,"Disconnected app appeared ready");
            int before=fake.Attempts;
            Check(coordinator.Execute(Intent("offline",true)).Outcome==PresentationOutcome.Failed && fake.Attempts==before,"Unavailable app executed");
            fake.Connected=true;fake.Fail=true;
            coordinator.Execute(Intent("failure",true));before=fake.Attempts;
            coordinator.Execute(Intent("failure",true));Check(fake.Attempts==before,"Ambiguous failure retried automatically");
            fake.Fail=false;fake.Supported=PresentationCapabilities.Scripture|PresentationCapabilities.Present;
            Check(coordinator.Execute(Intent("range",true)).Outcome==PresentationOutcome.Failed,"Unsupported range silently reduced");
            fake.Supported|=PresentationCapabilities.VerseRanges|PresentationCapabilities.Prepare;
            fake.Wait=true;fake.Entered.Reset();
            PresentationResult canceled=null;
            System.Threading.Thread worker=new System.Threading.Thread(delegate(){canceled=coordinator.Execute(Intent("pending",true));});
            worker.IsBackground=true;worker.Start();
            Check(fake.Entered.WaitOne(2000),"Worker did not start");
            Check(coordinator.Execute(Intent("concurrent",true)).Outcome==PresentationOutcome.Canceled,"Concurrent action accepted");
            before=fake.Sent;
            coordinator.SetEnabled(false);coordinator.SetEnabled(true);
            Check(worker.Join(2000),"Cancellation failed to finish");
            Check(canceled.Outcome==PresentationOutcome.Canceled && fake.Sent==before,"Re-enable revived a canceled command");
            Check(!coordinator.Busy,"Busy state leaked after cancellation");
            fake.Wait=false;
            Check(coordinator.Execute(Intent("new",false)).Outcome==PresentationOutcome.Prepared,"Could not recover after cancellation");
            string path=Path.Combine(directory,"preferences.txt");
            Check(IntegrationPreferences.Load(path),"Missing settings broke existing installation");
            IntegrationPreferences.Save(path,false);Check(!IntegrationPreferences.Load(path),"Disable preference not persisted");
            IntegrationPreferences.Save(path,true);Check(IntegrationPreferences.Load(path),"Re-enable preference not persisted");
            File.WriteAllText(path,"corrupt");Check(!IntegrationPreferences.Load(path),"Corrupt settings enabled integration");
            return "PASS: "+count+" adapter lifecycle checks. No real application or presentation actions.";
        }
    }
}
'@
Add-Type -TypeDefinition ($adapters+$request+$tests)
$temporary=Join-Path $PSScriptRoot ('adapter-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
 $folder=if($VerifyLocalCatalogue){Join-Path $env:ProgramData 'Softouch\EasyWorship.v7\Plugins\Bibles'}else{''}
 [EasyWorshipControllerMvp.AdapterTests]::Run($temporary,$folder)
}
finally {
 $settings=Join-Path $temporary 'preferences.txt'
 if(Test-Path -LiteralPath $settings){Remove-Item -LiteralPath $settings}
 Remove-Item -LiteralPath $temporary
}
