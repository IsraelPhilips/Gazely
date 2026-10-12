// Compiled with production recognition classes and queue/worker methods by Run-LiveReplay.ps1.
internal sealed class ReplayHarness
{
    private WhisperServerEngine whisperEngine = new WhisperServerEngine();
    private readonly UtteranceSegmenter utteranceSegmenter = new UtteranceSegmenter();
    private readonly ScriptureDetector scriptureDetector = new ScriptureDetector();
    private readonly ActiveReferenceState activeReference=new ActiveReferenceState();
    private readonly ReferenceRefinement referenceRefinement=new ReferenceRefinement();
    private readonly OfflineSemanticSearch semanticSearch=new OfflineSemanticSearch();
    private ScriptureQuoteIndex quoteIndex;
    private bool refinement, retrieval;
    private readonly object retrievalLock=new object();
    private string retrievalText;
    private bool retrievalRunning;
    private int maxQueue;
    private readonly List<SpeechChunk> whisperQueue = new List<SpeechChunk>();
    private bool whisperWorkerRunning;
    private volatile int listeningSession = 1;
    private volatile bool audioListening = true;
    private readonly object evidenceLock = new object();
    private readonly Dictionary<SpeechChunk, double[]> spans = new Dictionary<SpeechChunk, double[]>();
    private readonly Stopwatch clock = new Stopwatch();
    private StreamWriter events, log;
    private double inputMs;
    private bool saveChunks;
    private string output;
    private int sequence, failures;
    private long serverPeak, harnessPeak;
    private long retrievalPeak, combinedPeak;
    private double retrievalCpu;
    private Process server;
    private void SampleMemory()
    {
        harnessPeak=Math.Max(harnessPeak,Process.GetCurrentProcess().PeakWorkingSet64);
        if(server!=null && !server.HasExited) { server.Refresh(); serverPeak=Math.Max(serverPeak,server.PeakWorkingSet64); }
        try{
            Process worker=(Process)typeof(OfflineSemanticSearch).GetField("worker",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(semanticSearch);
            long working=Process.GetCurrentProcess().WorkingSet64+(server==null || server.HasExited?0:server.WorkingSet64);
            if(worker!=null && !worker.HasExited){worker.Refresh();retrievalPeak=Math.Max(retrievalPeak,worker.PeakWorkingSet64);retrievalCpu=worker.TotalProcessorTime.TotalMilliseconds;working+=worker.WorkingSet64;}
            combinedPeak=Math.Max(combinedPeak,working);
        }catch(InvalidOperationException){} // Worker may finish between samples.
    }

    // PRODUCTION_WORKER_METHODS

    private static string Cell(string value) { return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }
    private void Event(string kind, SpeechChunk chunk, string text, string reference, double inferenceMs)
    {
        lock (evidenceLock)
        {
            if(chunk!=null && !spans.ContainsKey(chunk))spans.Add(chunk,new double[]{chunk.AudioStartSample/16.0,chunk.AudioEndSample/16.0,0});
            double[] span = chunk == null ? new double[] {0, 0, 0} : spans[chunk];
            double ready = chunk == null ? 0 : (Stopwatch.GetTimestamp()-chunk.LastVoiceTicks)*1000.0/Stopwatch.Frequency;
            double queue = chunk == null || span[2] == 0 ? 0 : (span[2]-chunk.QueuedTicks)*1000.0/Stopwatch.Frequency;
            events.WriteLine(string.Join("\t", new string[] { (++sequence).ToString(), kind,
                clock.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),
                chunk == null ? "" : chunk.UtteranceId.ToString(), chunk == null ? "" : chunk.IsFinal.ToString(),
                span[0].ToString("F2",System.Globalization.CultureInfo.InvariantCulture), span[1].ToString("F2",System.Globalization.CultureInfo.InvariantCulture),
                queue.ToString("F2",System.Globalization.CultureInfo.InvariantCulture), inferenceMs.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),
                ready.ToString("F2",System.Globalization.CultureInfo.InvariantCulture), Cell(text), Cell(reference) }));
            events.Flush();
            if(chunk!=null && (kind=="partial" || kind=="final" || kind=="refined" || kind=="empty_final" || kind=="dropped" || kind=="superseded"))spans.Remove(chunk);
        }
    }
    private void TraceStarted(SpeechChunk chunk, long ticks) { Event(chunk.IsRefinement?"refinement_started":"started",chunk,"","",0);lock(evidenceLock) spans[chunk][2]=ticks; }
    private void OnRefinementStarted(SpeechChunk chunk) {}
    private void OnRefinedTranscript(string text,SpeechChunk chunk,double inferenceMs)
    {
        ScriptureDetection active=activeReference.Current;
        var parser=new ScriptureDetector();if(chunk.CorrectionRequested && active!=null && active.Chapter>0 && active.Verse>0)parser.SeedActiveReference(active);
        ScriptureDetection d=parser.Feed(text);
        bool corrected=d!=null && chunk.CorrectionRequested && d.EvidenceKind=="correction";
        if(!referenceRefinement.IsCurrent(chunk) || (Stopwatch.GetTimestamp()-chunk.LastVoiceTicks)*1000.0/Stopwatch.Frequency>4000)d=null;
        if(d!=null && active!=null && (d.Book==active.Book || corrected) && (active.Verse==0 || active.RequiresReview || corrected)){d.RequiresReview=true;d.EvidenceKind="audio-refinement";activeReference.Confirm(d);}
        else d=null;
        Event("refined",chunk,text,d==null?"":d.ReferenceText,inferenceMs);
    }
    private void RequestRefinement(SpeechChunk source,string text)
    {
        ScriptureDetection active=activeReference.Current;
        referenceRefinement.UpdateIntent(active==null?null:active.ReferenceText,listeningSession);
        bool correction=Regex.IsMatch(text??string.Empty,@"\b(?:no|sorry|correction|i mean)\b",RegexOptions.IgnoreCase);
        if(refinement && active!=null && (active.Verse==0 || active.RequiresReview || correction))referenceRefinement.Request(utteranceSegmenter.RecentAudio,Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency,source.LastVoiceTicks*1000.0/Stopwatch.Frequency,correction);
    }
    private void TraceDrop(SpeechChunk chunk) { Event("dropped",chunk,"","",0); }
    private bool TraceSuperseded(SpeechChunk chunk, bool remove) { if(remove) Event("superseded",chunk,"","",0); return remove; }
    private void AddLog(string value)
    {
        lock(evidenceLock) { log.WriteLine(value); log.Flush(); }
    }
    private void SetWhisperStatusSafe(string status, Color color) { Interlocked.Increment(ref failures); Event("error",null,status,"",0); }
    private void OnPartialTranscript(string text, SpeechChunk chunk, double inferenceMs)
    {
        scriptureDetector.SeedActiveReference(activeReference.Context);
        ScriptureDetection candidate = scriptureDetector.Preview(text,true);
        activeReference.Preview(candidate);RequestRefinement(chunk,text);
        Event("partial",chunk,text,candidate == null ? "" : candidate.ReferenceText,inferenceMs);
    }
    private void OnFinalTranscript(string text, double confidence, string engine, double inferenceMs, int session, SpeechChunk chunk)
    {
        scriptureDetector.SeedActiveReference(activeReference.Context);
        ScriptureDetection result = scriptureDetector.FeedFinal(text,session+":"+chunk.UtteranceId);
        activeReference.Confirm(result);
        if(result==null && (scriptureDetector.UnresolvedSource??string.Empty).EndsWith(text) && !string.IsNullOrEmpty(scriptureDetector.UnresolvedBook))activeReference.Confirm(new ScriptureDetection{Book=scriptureDetector.UnresolvedBook,Chapter=scriptureDetector.UnresolvedChapter,SourceText=text,EvidenceKind="draft",RequiresReview=true});
        RequestRefinement(chunk,text);
        if(result == null) result = scriptureDetector.Suggest(text);
        Event("final",chunk,text,result == null ? "" : result.ReferenceText,inferenceMs);
        if(retrieval)lock(retrievalLock){retrievalText=text;if(!retrievalRunning){retrievalRunning=true;ThreadPool.QueueUserWorkItem(delegate{
            while(true){string query;lock(retrievalLock){query=retrievalText;retrievalText=null;if(query==null){retrievalRunning=false;return;}}
                try{var found=semanticSearch.Find(quoteIndex,query);Event("retrieval",null,semanticSearch.Status,found.Count==0?"":found[0].ReferenceText,0);}catch(Exception ex){AddLog("Retrieval error: "+ex.Message);}}
        });}}
    }
    private void WaitIdle()
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while(true) {
            lock(whisperQueue) if(!whisperWorkerRunning && whisperQueue.Count == 0) return;
            if(timeout.Elapsed.TotalMinutes > 5) throw new TimeoutException("Inference did not drain within five minutes.");
            SampleMemory();
            Thread.Sleep(10);
        }
    }
    internal static byte[] ReadPcm(string path)
    {
        using(BinaryReader r = new BinaryReader(File.OpenRead(path))) {
            if(Encoding.ASCII.GetString(r.ReadBytes(4))!="RIFF") throw new InvalidDataException("Expected RIFF WAV.");
            r.ReadUInt32(); if(Encoding.ASCII.GetString(r.ReadBytes(4))!="WAVE") throw new InvalidDataException("Expected WAVE.");
            bool format=false; byte[] pcm=null;
            while(r.BaseStream.Position+8 <= r.BaseStream.Length) {
                string tag=Encoding.ASCII.GetString(r.ReadBytes(4)); uint size=r.ReadUInt32(); long end=r.BaseStream.Position+size;
                if(end>r.BaseStream.Length) throw new InvalidDataException("Truncated WAV chunk.");
                if(tag=="fmt ") {
                    if(size<16 || r.ReadUInt16()!=1 || r.ReadUInt16()!=1 || r.ReadUInt32()!=16000) throw new InvalidDataException("Use PCM16, mono, 16000 Hz WAV.");
                    r.ReadUInt32(); if(r.ReadUInt16()!=2 || r.ReadUInt16()!=16) throw new InvalidDataException("Use PCM16 WAV."); format=true;
                } else if(tag=="data") pcm=r.ReadBytes(checked((int)size));
                r.BaseStream.Position=end+(size%2);
            }
            if(!format || pcm==null || pcm.Length%2!=0) throw new InvalidDataException("Missing or invalid PCM data.");
            return pcm;
        }
    }
    private void SaveChunk(SpeechChunk chunk)
    {
        string name=chunk.UtteranceId+"-"+(chunk.IsFinal?"final":"partial")+"-"+inputMs.ToString("F0",System.Globalization.CultureInfo.InvariantCulture)+".wav";
        using(BinaryWriter w=new BinaryWriter(File.Create(Path.Combine(output,"chunks",name)))) {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36+chunk.Pcm.Length); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(chunk.Pcm.Length); w.Write(chunk.Pcm);
        }
    }
    private int Run(string[] args)
    {
        byte[] pcm=ReadPcm(args[0]); output=args[1]; string mode=args[2], model=args[3];
        int limit=int.Parse(args[4]); saveChunks=bool.Parse(args[5]);
        refinement=args.Length>11 && bool.Parse(args[11]);retrieval=args.Length>12 && bool.Parse(args[12]);
        if(limit>0 && pcm.Length>limit*32000L) Array.Resize(ref pcm,limit*32000);
        Directory.CreateDirectory(output); if(saveChunks) Directory.CreateDirectory(Path.Combine(output,"chunks"));
        using(events=new StreamWriter(Path.Combine(output,"events.tsv"),false,Encoding.UTF8))
        using(log=new StreamWriter(Path.Combine(output,"server.log"),false,Encoding.UTF8))
        using(whisperEngine) {
            events.WriteLine("sequence\tevent\twall_ms\tutterance\tis_final\taudio_start_ms\taudio_end_ms\tqueue_ms\tinference_ms\tready_from_last_voice_ms\ttext\treference");
            utteranceSegmenter.Reset();
            utteranceSegmenter.MinSpeechMs=model=="tiny.en"?160:model=="base.en"?180:220;
            utteranceSegmenter.SilenceToFinalizeMs=model=="tiny.en"?360:model=="base.en"?400:440;
            if(args.Length>7 && int.Parse(args[7])>0) utteranceSegmenter.SilenceToFinalizeMs=int.Parse(args[7]);
            if(args.Length>8 && int.Parse(args[8])>0) utteranceSegmenter.MaxUtteranceMs=int.Parse(args[8]);
            utteranceSegmenter.ChunkReady += delegate(SpeechChunk chunk) {
                lock(evidenceLock) spans.Add(chunk,new double[]{inputMs-chunk.Pcm.Length/32.0,inputMs,0});
                Event("emitted",chunk,"","",0); if(saveChunks) SaveChunk(chunk); OnSpeechChunk(chunk);
            };
            if(args.Length>6) whisperEngine.EnableNeuralVad=bool.Parse(args[6]);
            if(args.Length>9)whisperEngine.BeamSizeOverride=int.Parse(args[9]);
            if(args.Length>10)whisperEngine.NormalizeInput=bool.Parse(args[10]);
            if(retrieval)quoteIndex=new ScriptureQuoteIndex(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corpus","kjv.tsv"));
            whisperEngine.Start(model,AddLog);
            server=(Process)typeof(WhisperServerEngine).GetField("process",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(whisperEngine);
            clock.Start();
            int eofMs=Math.Max(800,utteranceSegmenter.SilenceToFinalizeMs+400);
            for(int offset=0;offset<pcm.Length+eofMs*32;offset+=1280) {
                int length=offset<pcm.Length?Math.Min(1280,pcm.Length-offset):1280;
                if(mode=="realtime") while(clock.Elapsed.TotalMilliseconds<(offset+length)/32.0) Thread.Sleep(1);
                byte[] frame=new byte[length]; if(offset<pcm.Length) Buffer.BlockCopy(pcm,offset,frame,0,length);
                inputMs=(offset+length)/32.0; utteranceSegmenter.Process(frame);
                SampleMemory();
                lock(whisperQueue)maxQueue=Math.Max(maxQueue,whisperQueue.Count);
                if(mode=="accelerated") WaitIdle();
            }
            WaitIdle(); audioListening=false; Event("completed",null,"presentation disabled","",0);
            Stopwatch retrievalDrain=Stopwatch.StartNew();while(retrievalRunning && retrievalDrain.ElapsedMilliseconds<30000)Thread.Sleep(20);
            if(retrievalRunning)throw new TimeoutException("Retrieval did not drain");SampleMemory();semanticSearch.Dispose();
            SampleMemory();
            File.WriteAllText(Path.Combine(output,"resources.tsv"),"server_peak_working_set_bytes\tharness_peak_working_set_bytes\tserver_cpu_ms\treplay_wall_ms\n"+serverPeak+"\t"+harnessPeak+"\t"+server.TotalProcessorTime.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\t"+clock.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(output,"queue.json"),"{\"maximum_waiting_jobs\":"+maxQueue+"}");
            File.WriteAllText(Path.Combine(output,"refinement.json"),"{\"requested\":"+referenceRefinement.Requested+",\"admitted\":"+referenceRefinement.Admitted+",\"expired\":"+referenceRefinement.Expired+",\"deferred_checks\":"+referenceRefinement.Deferred+"}");
            File.WriteAllText(Path.Combine(output,"process-resources.tsv"),"sampled_combined_working_set_bytes\tretrieval_peak_working_set_bytes\tretrieval_cpu_ms\tharness_cpu_ms\n"+combinedPeak+"\t"+retrievalPeak+"\t"+retrievalCpu.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\t"+Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("Replay finished: "+mode+", "+pcm.Length/32000.0+" seconds of audio; "+failures+" inference errors. No presentation adapter included.");
        }
        return failures==0?0:1;
    }
    public static int Main(string[] args)
    {
        try {
            if(args.Length>0 && args[0]=="--noise-check") return NoiseCheck(args[1]);
            return new ReplayHarness().Run(args);
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static int NoiseCheck(string report)
    {
        int failed=0;
        using(WhisperServerEngine engine=new WhisperServerEngine())
        using(StreamWriter writer=new StreamWriter(report,false,Encoding.UTF8)) {
            engine.EnableNeuralVad=true;
            engine.Start("base.en",null);
            writer.WriteLine("fixture\ttext\tpassed");
            foreach(string name in new string[]{"silence","seeded_noise","steady_hum"}) {
                byte[] pcm=new byte[6*32000]; Random random=new Random(491);
                for(int i=0;i<pcm.Length/2;i++) {
                    short sample=name=="silence"?(short)0:name=="seeded_noise"?(short)random.Next(-1600,1601):(short)(1600*Math.Sin(2*Math.PI*100*i/16000));
                    pcm[2*i]=(byte)(sample&255);pcm[2*i+1]=(byte)((sample>>8)&255);
                }
                string text=engine.Transcribe(pcm,SpeechVocabulary.Prompt);
                bool passed=string.IsNullOrWhiteSpace(text);if(!passed) failed++;
                writer.WriteLine(name+"\t"+Cell(text)+"\t"+passed);
                Console.WriteLine(name+": "+(passed?"PASS":"FAIL "+text));
            }
        }
        return failed==0?0:1;
    }
}
