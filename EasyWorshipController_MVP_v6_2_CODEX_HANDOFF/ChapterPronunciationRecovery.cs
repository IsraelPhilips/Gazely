using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;

namespace EasyWorshipControllerMvp
{
    internal sealed class ChapterRecoverySuggestion
    {
        public string Book;
        public int Chapter;
        public double Score;
        public string HeardChapter;
        public ScriptureDetection Reference;
    }

    // Uses the installed voice's pronunciation engine only; audio is discarded.
    // This is a bounded hypothesis generator, never a transcript replacement.
    internal sealed class ChapterPronunciationRecovery : IDisposable
    {
        private readonly SpeechSynthesizer voice=new SpeechSynthesizer();
        private readonly object gate=new object();
        private readonly List<string> current=new List<string>();
        private readonly Dictionary<int,string[]> numbers=new Dictionary<int,string[]>();
        public string VoiceName { get { return voice.Voice.Name; } }
        public ChapterPronunciationRecovery()
        {
            if(voice.Voice.Culture.TwoLetterISOLanguageName!="en")
            {
                bool found=false;
                foreach(InstalledVoice installed in voice.GetInstalledVoices())
                    if(installed.Enabled && installed.VoiceInfo.Culture.TwoLetterISOLanguageName=="en")
                    {voice.SelectVoice(installed.VoiceInfo.Name);found=true;break;}
                if(!found) { voice.Dispose();throw new InvalidOperationException("An installed English voice is needed for pronunciation candidates."); }
            }
            voice.SetOutputToNull();
            voice.PhonemeReached+=delegate(object sender,PhonemeReachedEventArgs args)
            { if(!string.IsNullOrWhiteSpace(args.Phoneme)) current.Add(args.Phoneme); };
            for(int i=1;i<=150;i++) numbers[i]=Pronounce(Words(i));
            if(numbers[49].Length==0) throw new InvalidOperationException("The installed voice does not expose pronunciation data.");
        }
        private string[] Pronounce(string text) { current.Clear();voice.Speak(text);return current.ToArray(); }
        private static string Words(int n)
        {
            string[] small={"zero","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen"};
            string[] tens={"","","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"};
            if(n<20)return small[n];
            if(n<100)return tens[n/10]+(n%10==0?"":" "+small[n%10]);
            return small[n/100]+" hundred"+(n%100==0?"":" "+Words(n%100));
        }
        private static double Distance(string[] a,string[] b)
        {
            double[] prior=new double[b.Length+1];for(int j=0;j<=b.Length;j++)prior[j]=j;
            for(int i=1;i<=a.Length;i++)
            {
                double[] next=new double[b.Length+1];next[0]=i;
                for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(prior[j]+1,next[j-1]+1),prior[j-1]+(a[i-1]==b[j-1]?0:1));
                prior=next;
            }
            return prior[b.Length]/Math.Max(a.Length,b.Length);
        }
        public ChapterRecoverySuggestion Find(string book,string transcript)
        {
            int maximum=BibleCanon.ChapterCount(book);
            if(maximum<1 || maximum>150 || string.IsNullOrWhiteSpace(transcript))return null;
            Match slot=Regex.Match(transcript,@"\b"+Regex.Escape(book)+@"\s+chapter\s+([a-z]+(?:\s+[a-z]+){0,4})\s*[,;:]?\s+(verses?\b.*)$",RegexOptions.IgnoreCase);
            if(!slot.Success)return null;
            string phrase=slot.Groups[1].Value;
            lock(gate)
            {
                string[] heard=Pronounce(phrase);
                if(heard.Length<6)return null;
                int best=0;double first=double.MaxValue,second=double.MaxValue;
                for(int i=1;i<=maximum;i++)
                {
                    double distance=Distance(heard,numbers[i]);
                    if(distance<first){second=first;first=distance;best=i;}else if(distance<second)second=distance;
                }
                if(first>0.25 || second-first<0.08)return null;
                ScriptureDetection reference=null;
                string verses=slot.Groups[2].Value;
                // Multiple unconnected numeric tokens are conflicting evidence, not a verse to choose silently.
                if(!Regex.IsMatch(verses,@"^verses?\s+\d+\s*[, ]\s*\d+\b",RegexOptions.IgnoreCase))
                    reference=new ScriptureDetector().Feed(book+" chapter "+best+" "+verses);
                if(reference!=null)
                {
                    reference.RequiresReview=true;reference.EvidenceKind="pronunciation";reference.MatchScore=1-first;
                    reference.Confidence=0.6;reference.SourceText=transcript;
                    reference.Reason="Possible chapter recovery: heard '"+phrase+"'; pronunciation is closest to "+Words(best)+". Confirm against the speaker.";
                }
                return new ChapterRecoverySuggestion{Book=book,Chapter=best,Score=1-first,HeardChapter=phrase,Reference=reference};
            }
        }
        public void Dispose() { lock(gate) voice.Dispose(); }
    }
}
