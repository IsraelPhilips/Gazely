using System;
using System.Collections.Generic;
using System.IO;

namespace EasyWorshipControllerMvp
{
    [Flags]
    internal enum PresentationCapabilities { None=0, Scripture=1, VerseRanges=2, Prepare=4, Present=8, ObserveLive=16 }
    internal enum IntegrationState { Disabled, Connecting, Ready, Disconnected, Error }
    internal enum PresentationOutcome { Prepared, SentUnverified, LiveVerified, Canceled, Failed }

    internal sealed class PresentationIntent
    {
        public string Id { get; private set; }
        public ScriptureRequest Scripture { get; private set; }
        public bool Present { get; private set; }
        public bool ChangeVersion { get; private set; }
        public string Version { get; private set; }
        public int VersionIndex { get; private set; }
        public PresentationIntent(string id, ScriptureRequest scripture, bool present, bool changeVersion, string version, int index)
        {
            if(string.IsNullOrWhiteSpace(id) || scripture==null) throw new ArgumentException("An intent needs an ID and content.");
            Id=id;
            Scripture=new ScriptureRequest { Book=scripture.Book,Chapter=scripture.Chapter,Verse=scripture.Verse,VerseEnd=scripture.VerseEnd };
            Present=present;ChangeVersion=changeVersion;Version=version??string.Empty;VersionIndex=index;
        }
        public string Signature { get { return Scripture.ReferenceText+"|"+Present+"|"+ChangeVersion+"|"+Version+"|"+VersionIndex; } }
    }

    internal sealed class PresentationResult
    {
        public PresentationOutcome Outcome { get; private set; }
        public string Detail { get; private set; }
        public PresentationResult(PresentationOutcome outcome,string detail) { Outcome=outcome;Detail=detail; }
    }

    internal interface IPresentationAdapter
    {
        string Id { get; }
        string Name { get; }
        PresentationCapabilities Capabilities { get; }
        bool Probe();
        PresentationResult Execute(PresentationIntent intent,Func<bool> canceled);
        void ResetSession();
    }

    // The coordinator owns destination lifecycle and at-most-once command attempts.
    // Detection has no dependency on the target application's UI or API.
    internal sealed class PresentationCoordinator
    {
        private readonly IPresentationAdapter adapter;
        private readonly object gate=new object();
        private readonly Dictionary<string,PresentationResult> results=new Dictionary<string,PresentationResult>();
        private readonly Dictionary<string,string> signatures=new Dictionary<string,string>();
        private readonly Queue<string> history=new Queue<string>();
        private bool enabled=false,busy=false;
        private int generation=0;
        private IntegrationState state=IntegrationState.Disabled;
        public PresentationCoordinator(IPresentationAdapter adapter) { this.adapter=adapter; }
        public bool Enabled { get { lock(gate) return enabled; } }
        public bool Busy { get { lock(gate) return busy; } }
        public IntegrationState State { get { lock(gate) return state; } }
        public string Name { get { return adapter.Name; } }
        public void SetEnabled(bool value)
        {
            lock(gate)
            {
                if(enabled==value) return;
                enabled=value;generation++;state=value?IntegrationState.Connecting:IntegrationState.Disabled;
                // Reset on the next worker/probe when idle; never race an active adapter.
                if(!busy) adapter.ResetSession();
            }
        }
        public void Cancel()
        {
            lock(gate) { generation++; if(!busy) adapter.ResetSession(); }
        }
        public IntegrationState Refresh()
        {
            int revision;
            lock(gate) { if(!enabled || busy) return state; revision=generation; }
            IntegrationState next;
            try { next=adapter.Probe()?IntegrationState.Ready:IntegrationState.Disconnected; }
            catch { next=IntegrationState.Error; }
            lock(gate)
            {
                if(generation==revision && enabled) state=next;
                return state;
            }
        }
        public PresentationResult Execute(PresentationIntent intent)
        {
            int revision;
            lock(gate)
            {
                PresentationResult previous;
                if(results.TryGetValue(intent.Id,out previous))
                {
                    if(signatures[intent.Id]!=intent.Signature) throw new InvalidOperationException("An intent ID cannot be reused for different content.");
                    return previous;
                }
                if(!enabled) return new PresentationResult(PresentationOutcome.Canceled,"Enable a presentation integration in Settings first.");
                if(busy) return new PresentationResult(PresentationOutcome.Canceled,"Another presentation command is in progress.");
                PresentationCapabilities required=PresentationCapabilities.Scripture|(intent.Present?PresentationCapabilities.Present:PresentationCapabilities.Prepare);
                if(intent.Scripture.VerseEnd>intent.Scripture.Verse) required|=PresentationCapabilities.VerseRanges;
                if((adapter.Capabilities&required)!=required) return new PresentationResult(PresentationOutcome.Failed,"This integration does not support the requested operation.");
                busy=true;revision=generation;
            }
            PresentationResult result;
            try
            {
                Func<bool> canceled=delegate { lock(gate) return !enabled || revision!=generation; };
                if(canceled()) throw new OperationCanceledException();
                if(!adapter.Probe()) throw new InvalidOperationException("The presentation application is not available.");
                if(canceled()) throw new OperationCanceledException();
                result=adapter.Execute(intent,canceled);
            }
            catch(OperationCanceledException) { result=new PresentationResult(PresentationOutcome.Canceled,"Command canceled. Check the destination before retrying."); }
            catch(Exception ex) { result=new PresentationResult(PresentationOutcome.Failed,ex.Message); }
            lock(gate)
            {
                results[intent.Id]=result;signatures[intent.Id]=intent.Signature;history.Enqueue(intent.Id);
                while(history.Count>256) { string oldest=history.Dequeue();results.Remove(oldest);signatures.Remove(oldest); }
                busy=false;
                if(!enabled) state=IntegrationState.Disabled;
                else if(result.Outcome==PresentationOutcome.Failed) state=IntegrationState.Error;
                if(revision!=generation) adapter.ResetSession();
            }
            return result;
        }
    }

    internal static class IntegrationPreferences
    {
        public static bool Load(string path)
        {
            if(!File.Exists(path)) return true; // Preserve the existing EasyWorship installation.
            return string.Equals(File.ReadAllText(path).Trim(),"easyworship7=enabled",StringComparison.Ordinal);
        }
        public static void Save(string path,bool enabled)
        {
            string folder=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(folder);
            string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary,enabled?"easyworship7=enabled":"easyworship7=disabled");
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    internal sealed class EasyWorship7Adapter : IPresentationAdapter
    {
        private readonly EasyWorshipAutomation automation;
        public EasyWorship7Adapter(EasyWorshipAutomation automation) { this.automation=automation; }
        public string Id { get { return "easyworship7"; } }
        public string Name { get { return "EasyWorship 7"; } }
        public PresentationCapabilities Capabilities { get { return PresentationCapabilities.Scripture|PresentationCapabilities.VerseRanges|PresentationCapabilities.Prepare|PresentationCapabilities.Present; } }
        public bool Probe() { return automation.IsConnected(); }
        public void ResetSession() { /* Version navigation invalidates itself before moving; listening/cancellation alone does not change the Bible selection. */ }
        public PresentationResult Execute(PresentationIntent intent,Func<bool> canceled)
        {
            automation.IsCanceled=canceled;
            try
            {
                string observed=automation.ShowScripture(intent.Scripture,intent.ChangeVersion,intent.Version,intent.VersionIndex,intent.Present);
                return new PresentationResult(intent.Present?PresentationOutcome.SentUnverified:PresentationOutcome.Prepared,
                    intent.Present?"Presentation command sent for "+observed+". Live screen not verified.":"Prepared: "+observed+". Nothing sent live.");
            }
            finally { automation.IsCanceled=null; }
        }
    }
}
