$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start = $source.IndexOf('    internal sealed class SpeechChunk')
$end = $source.IndexOf('    internal sealed class WhisperServerEngine')
$core = 'using System; using System.Collections.Generic; using System.Diagnostics; using System.IO; using System.Text; ' + $source.Substring($start, $end-$start)
$tests = @"
public static class AudioPipelineChecks {
    private static int count;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
    private static byte[] Frame(int amplitude) {
        byte[] data = new byte[1280];
        for(int i = 0; i < 640; i++) { short v = (short)(amplitude * Math.Sin(2 * Math.PI * 440 * i / 16000)); data[2*i]=(byte)(v & 255); data[2*i+1]=(byte)((v >> 8)&255); }
        return data;
    }
    public static string Run() {
        var chunks = new List<SpeechChunk>();
        var segmenter = new UtteranceSegmenter();
        segmenter.ChunkReady += delegate(SpeechChunk chunk) { chunk.Session = 1; chunks.Add(chunk); };
        for(int i=0;i<100;i++) segmenter.Process(Frame(0));
        Check(chunks.Count == 0, "Silence must not create recognition work.");
        for(int i=0;i<15;i++) segmenter.Process(Frame(200));
        for(int i=0;i<10;i++) segmenter.Process(Frame(0));
        Check(chunks.Count == 1 && chunks[0].IsFinal, "Quiet normal speech should finalize once.");
        Check(chunks[0].Pcm.Length == (9+15+10)*1280, "400ms pre-roll includes trigger exactly once.");
        chunks.Clear(); segmenter.Reset();
        for(int i=0;i<10;i++) segmenter.Process(Frame(0));
        segmenter.Process(Frame(1000));
        for(int i=0;i<12;i++) segmenter.Process(Frame(0));
        Check(chunks.Count == 0, "A single click must not generate speech.");
        chunks.Clear(); segmenter.Reset();
        for(int i=0;i<40;i++) segmenter.Process(Frame(1000));
        Check(chunks.Count >= 1 && !chunks[0].IsFinal, "Sustained speech must expose a partial before silence.");
        long id = chunks[0].UtteranceId;
        for(int i=0;i<10;i++) segmenter.Process(Frame(0));
        Check(chunks[chunks.Count-1].IsFinal && chunks[chunks.Count-1].UtteranceId == id, "Final must identify the same utterance.");
        chunks.Clear(); segmenter.Reset(); segmenter.EnablePartials = false;
        for(int i=0;i<40;i++) segmenter.Process(Frame(1000));
        Check(chunks.Count == 0, "Disabling early results must skip speculative work.");
        for(int i=0;i<10;i++) segmenter.Process(Frame(0));
        Check(chunks.Count == 1 && chunks[0].IsFinal, "Final recognition must work with partials disabled.");
        chunks.Clear(); segmenter.Reset(); segmenter.MaxUtteranceMs = 1000;
        for(int i=0;i<30;i++) segmenter.Process(Frame(1000));
        Check(chunks.Count == 1 && chunks[0].IsFinal, "Continuous speech must be bounded.");
        byte[] low = Frame(200), original = (byte[])low.Clone();
        byte[] boosted = SpeechVocabulary.NormalizeAudio(low);
        Check(BitConverter.ToInt16(boosted, 20) > BitConverter.ToInt16(low, 20), "Quiet speech must receive bounded gain.");
        Check(Convert.ToBase64String(low) == Convert.ToBase64String(original), "Gain must not mutate captured PCM.");
        byte[] hot = Frame(32000);
        Check(Convert.ToBase64String(hot) == Convert.ToBase64String(SpeechVocabulary.NormalizeAudio(hot)), "Hot audio must not be amplified.");
        byte[] silent = Frame(0);
        Check(Convert.ToBase64String(silent) == Convert.ToBase64String(SpeechVocabulary.NormalizeAudio(silent)), "Silence must stay silent.");
        Check(!SpeechVocabulary.IsRunawayRepetition("Holy, holy, holy. Amen, amen."), "Legitimate short emphasis must be retained.");
        Check(!SpeechVocabulary.IsRunawayRepetition("Genesis chapter forty nine verses three and four"), "References must not be repetition-filtered.");
        string runaway = "KJV, ";
        for(int i=0;i<60;i++) runaway += "Nephi, ";
        Check(SpeechVocabulary.IsRunawayRepetition(runaway), "Reported Nephi decoding loop must be rejected.");
        string loop = "";
        for(int i=0;i<25;i++) loop += "thank you ";
        Check(SpeechVocabulary.IsRunawayRepetition(loop), "Long repeated phrase loop must be rejected.");
        string normal = "";
        for(int i=0;i<60;i++) normal += "word" + i + " ";
        Check(!SpeechVocabulary.IsRunawayRepetition(normal), "Long distinct speech must survive unchanged.");
        Check(!SpeechVocabulary.IsRunawayRepetition(null), "Absent speech is not a repetition loop.");
        var recent=new RecentSpeechAudio();
        for(int i=0;i<750;i++)recent.Append(Frame(i));
        var span=recent.Snapshot(16);
        Check(span.Pcm.Length==16*32000 && span.AudioStartSample==14*16000 && span.AudioEndSample==30*16000,"Rolling audio must retain exact bounded sample span");
        var refine=new ReferenceRefinement();refine.UpdateIntent("Leviticus",1);
        Check(refine.Request(recent,0),"Incomplete intent can queue recovery");
        Check(refine.Take(0)==null,"Unknown decoder cost must not admit speculative work");
        refine.Observe(16000,500);
        var job=refine.Take(0);Check(job!=null && job.IsRefinement && refine.IsCurrent(job),"Admissible recovery retains intent identity");
        Check(!refine.Request(recent,2000),"Recovery must be spaced by three seconds");
        Check(refine.Request(recent,3000) && refine.Take(3000)!=null,"Second attempt allowed after cooldown");
        Check(!refine.Request(recent,7000),"Maximum two attempts per intent");
        refine.UpdateIntent("John 3:16",1);Check(!refine.IsCurrent(job),"New reference cancels stale result");
        refine.Request(recent,8000);refine.Observe(1000,1000);Check(refine.Take(8000)==null,"Overload suspends recovery");
        Check(refine.Take(13000)==null,"Expired recovery cannot run late");
        refine.Reset();Check(!refine.IsCurrent(job),"Release/restart invalidates old recovery");
        recent.Reset();Check(recent.EndSample==0 && recent.Snapshot(16).Pcm.Length==0,"Restart clears retained audio");
        return "PASS: " + count + " audio pipeline checks. No microphone or EasyWorship access.";
    }
}
"@
Add-Type -TypeDefinition ($core + $tests)
[AudioPipelineChecks]::Run()
