using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EasyWorshipControllerMvp
{
    [Flags]
    internal enum PresentationCapabilities { None=0, Scripture=1, VerseRanges=2, Prepare=4, Present=8, ObserveLive=16 }
    internal enum IntegrationState { Disabled, Connecting, Ready, Disconnected, Error }
    internal enum PresentationOutcome { Prepared, SentUnverified, LiveVerified, Canceled, Failed }

    internal sealed class VersionSelectionObservation
    {
        public string Code,DisplayName,EvidenceSource,ProcessIdentity,ProfileIdentity,CatalogueRevision;
        public int Position=-1;
        public DateTime ObservedUtc;
        public bool Verified;
        public bool Matches(string requested,string process,string revision)
        {return Verified && ProcessIdentity==process && CatalogueRevision==revision && string.Equals(Code,requested,StringComparison.OrdinalIgnoreCase);}
    }
    internal sealed class VersionCatalogueEntry
    {
        public string Code,DisplayName,EvidenceSource;
        public int Position;
    }
    internal static class VersionCatalogueSetup
    {
        private static string Code(string input)
        {
            string code=(input??string.Empty).Trim().ToUpperInvariant();
            if(code.Length<2 || code.Length>12)throw new FormatException("Use displayed Bible abbreviations, one per line.");
            foreach(char c in code)if(!char.IsLetterOrDigit(c) || c>127)
                throw new FormatException("Invalid Bible abbreviation: "+code);
            return code;
        }
        public static string BuildMap(string displayedCodes)
        {
            string[] lines=(displayedCodes??string.Empty).Replace("\r",string.Empty).Split(new[]{'\n',',',';'},StringSplitOptions.RemoveEmptyEntries);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mapped=new List<string>();
            foreach(string line in lines){
                string code=Code(line);
                if(!seen.Add(code))throw new FormatException("Duplicate Bible abbreviation: "+code);
                if(mapped.Count>=250)throw new FormatException("Too many Bible rows.");
                mapped.Add(code+"="+mapped.Count);
            }
            if(mapped.Count==0)throw new FormatException("Enter the displayed Bible abbreviations from top to bottom.");
            return "# Local EasyWorship Bible order. Recheck after profile or catalogue changes.\r\n"+
                string.Join("\r\n",mapped.ToArray())+"\r\n";
        }
        public static Dictionary<string,int> ParseMap(string text)
        {
            var map=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var used=new HashSet<int>();
            foreach(string raw in (text??string.Empty).Replace("\r",string.Empty).Split('\n')){
                string line=raw.Trim();if(line.Length==0 || line.StartsWith("#"))continue;
                string[] parts=line.Split('=');int index;
                if(parts.Length!=2 || !int.TryParse(parts[1].Trim(),out index) || index<0 || index>=250)
                    throw new FormatException("Use CODE=INDEX for each Bible row, beginning at zero.");
                string code=Code(parts[0]);
                if(map.ContainsKey(code) || !used.Add(index))throw new FormatException("Bible codes and indexes must be unique.");
                map.Add(code,index);
            }
            for(int i=0;i<map.Count;i++)if(!used.Contains(i))throw new FormatException("Bible indexes must be continuous from zero.");
            return map;
        }
    }
    // Read only the abbreviation in each modern EasyWorship Bible header. Older
    // .ewb files may also be present, so this is a proposed list for review,
    // never proof of the visible library order or the selected translation.
    internal static class LocalBibleCatalogue
    {
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename,out IntPtr database,int flags,IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr database,string sql,int bytes,out IntPtr statement,IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr database);

        public static bool TryDiscover(string bibleDirectory,out string[] codes,out string report)
        {
            codes=new string[0];report="";
            if(!Directory.Exists(bibleDirectory)){report="EasyWorship Bible folder was not found on this PC.";return false;}
            var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int older=0,unreadable=0;
            string[] files;
            try{files=Directory.GetFiles(bibleDirectory,"*.ewb");}
            catch(Exception ex){report="EasyWorship Bible folder could not be read: "+ex.Message;return false;}
            foreach(string path in files)
            {
                string abbreviation=null;
                try
                {
                    using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                    {
                        byte[] header=new byte[16];
                        if(file.Read(header,0,header.Length)!=header.Length ||
                            Encoding.ASCII.GetString(header)!="SQLite format 3\0"){older++;continue;}
                    }
                    IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;
                    try
                    {
                        byte[] name=Encoding.UTF8.GetBytes(path+"\0");
                        if(sqlite3_open_v2(name,out database,1,IntPtr.Zero)!=0)throw new InvalidDataException("Bible header could not be opened read-only.");
                        if(sqlite3_prepare_v2(database,"SELECT abbrev_name FROM header LIMIT 2",-1,out statement,IntPtr.Zero)!=0)
                            throw new InvalidDataException("Bible header table could not be read.");
                        if(sqlite3_step(statement)!=100)throw new InvalidDataException("Bible header is empty.");
                        abbreviation=Marshal.PtrToStringAnsi(sqlite3_column_text(statement,0));
                        if(sqlite3_step(statement)!=101)throw new InvalidDataException("Bible header has multiple rows.");
                    }
                    finally
                    {
                        if(statement!=IntPtr.Zero)sqlite3_finalize(statement);
                        if(database!=IntPtr.Zero)sqlite3_close(database);
                    }
                    // Reuse the map parser's code rules; no Bible text is read.
                    if(VersionCatalogueSetup.ParseMap(VersionCatalogueSetup.BuildMap(abbreviation)).Count!=1)
                        throw new InvalidDataException("Bible header has more than one abbreviation.");
                    if(!found.Add(abbreviation.Trim().ToUpperInvariant()))
                        throw new InvalidDataException("Duplicate displayed Bible abbreviation.");
                }
                catch(Exception ex)
                {
                    if(ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException){report="Windows SQLite is unavailable; use guided setup.";return false;}
                    unreadable++;
                }
            }
            if(found.Count==0){report="No readable modern Bible headers were found. Use guided setup.";return false;}
            if(unreadable>0){report="Some Bible headers could not be read. Use guided setup and check the full EasyWorship library.";return false;}
            codes=new List<string>(found).ToArray();Array.Sort(codes,StringComparer.Ordinal);
            report=found.Count+" Bible abbreviations found in read-only local headers"+
                (older>0?"; "+older+" older-format files were excluded":"")+
                ". Compare the full visible EasyWorship list before saving.";
            return true;
        }
    }
    internal static class VersionOutputPolicy
    {
        public static void RejectObservedMismatch(string requested,string observed)
        {
            // The mapped/tracked position is used when EasyWorship does not
            // expose a selected row. A readable contradictory row still stops.
            if(string.IsNullOrWhiteSpace(observed))return;
            if(!string.Equals(requested,observed,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requested "+requested+", but EasyWorship's library shows "+observed+". Recheck the version map; nothing was sent live.");
        }
    }

    internal sealed class PresentationIntent
    {
        public string Id { get; private set; }
        public ScriptureRequest Scripture { get; private set; }
        public bool Present { get; private set; }
        public bool ChangeVersion { get; private set; }
        public string Version { get; private set; }
        public int VersionIndex { get; private set; }
        public bool Automatic { get; private set; }
        public PresentationIntent(string id, ScriptureRequest scripture, bool present, bool changeVersion, string version, int index,bool automatic=false)
        {
            if(string.IsNullOrWhiteSpace(id) || scripture==null) throw new ArgumentException("An intent needs an ID and content.");
            Id=id;
            Scripture=new ScriptureRequest { Book=scripture.Book,Chapter=scripture.Chapter,Verse=scripture.Verse,VerseEnd=scripture.VerseEnd };
            Present=present;ChangeVersion=changeVersion;Version=version??string.Empty;VersionIndex=index;Automatic=automatic;
        }
        public string Signature { get { return Scripture.ReferenceText+"|"+Present+"|"+ChangeVersion+"|"+Version+"|"+VersionIndex+"|"+Automatic; } }
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
