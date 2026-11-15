using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;

namespace EasyWorshipControllerMvp
{
    internal sealed class BookRecoverySuggestion
    {
        public string Book;
        public string HeardBook;
        public double Score;
        public string Reason;
    }

    // Bounded phonetic recovery of a misheard BOOK name, mirroring
    // ChapterPronunciationRecovery. A correction cue ("no, Micah") that does not
    // match any alias because the recognizer produced "mica" or "my lagai" would
    // otherwise be discarded, even though the preacher's meaning is unambiguous
    // and the chapter and verse were already called.
    //
    // This is a hypothesis generator, not a rule that turns arbitrary words into
    // book names: it requires a correction cue, a close phonetic match, and a
    // clear margin over the runner-up. Every result is review-required and can
    // never inherit automatic permission.
    internal sealed class BookPronunciationRecovery : IDisposable
    {
        private readonly SpeechSynthesizer voice = new SpeechSynthesizer();
        private readonly object gate = new object();
        private readonly List<string> current = new List<string>();
        private readonly List<KeyValuePair<string, string[]>> books = new List<KeyValuePair<string, string[]>>();

        public string VoiceName { get { return voice.Voice.Name; } }

        public BookPronunciationRecovery()
        {
            if (voice.Voice.Culture.TwoLetterISOLanguageName != "en")
            {
                bool found = false;
                foreach (InstalledVoice installed in voice.GetInstalledVoices())
                    if (installed.Enabled && installed.VoiceInfo.Culture.TwoLetterISOLanguageName == "en")
                    { voice.SelectVoice(installed.VoiceInfo.Name); found = true; break; }
                if (!found) { voice.Dispose(); throw new InvalidOperationException("An installed English voice is needed for book pronunciation candidates."); }
            }
            voice.SetOutputToNull();
            voice.PhonemeReached += delegate(object sender, PhonemeReachedEventArgs args)
            { if (!string.IsNullOrWhiteSpace(args.Phoneme)) current.Add(args.Phoneme); };

            foreach (string name in BibleCanon.BookNames)
                books.Add(new KeyValuePair<string, string[]>(name, Pronounce(name)));
            if (books.Count == 0) throw new InvalidOperationException("The Bible canon did not supply book names.");
        }

        private string[] Pronounce(string text) { current.Clear(); voice.Speak(text); return current.ToArray(); }

        internal static double Distance(string[] a, string[] b)
        {
            double[] prior = new double[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prior[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                double[] next = new double[b.Length + 1]; next[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    next[j] = Math.Min(Math.Min(prior[j] + 1, next[j - 1] + 1), prior[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                prior = next;
            }
            return prior[b.Length] / Math.Max(a.Length, b.Length);
        }

        private const double MaximumDistance = 0.30;
        private const double MinimumMargin = 0.06;

        // The words after a correction cue, with spoken filler removed. Real
        // speech inserts "no, I mean, give me Micah", which a bare-alias matcher
        // cannot match.
        internal static string Suspect(string transcript)
        {
            // Punctuation between the cue and the words it introduces ("no, Micah")
            // is normal speech, so the cue may be followed by any punctuation.
            Match cue = Regex.Match(transcript ?? string.Empty, @"\b(?:no|sorry|correction|i mean|rather)\b[^A-Za-z]+(.+)$", RegexOptions.IgnoreCase);
            if (!cue.Success) return string.Empty;
            string phrase = cue.Groups[1].Value;
            // A second cue often follows the first ("no, I mean, Micah"), so the
            // filler set includes the cue words themselves.
            phrase = Regex.Replace(phrase, @"\b(?:please|now|okay|ok|well|just|give me|let me have|i want|it is|its|it's|i mean|sorry|correction|rather|no)\b", " ", RegexOptions.IgnoreCase);
            phrase = Regex.Replace(phrase, @"[^A-Za-z\s]", " ");
            return Regex.Replace(phrase, @"\s+", " ").Trim();
        }

        public BookRecoverySuggestion Find(string transcript)
        {
            string phrase = Suspect(transcript);
            if (phrase.Length < 3) return null;
            lock (gate)
            {
                string[] heard = Pronounce(phrase);
                if (heard.Length < 3) return null;
                string best = null;
                double first = double.MaxValue, second = double.MaxValue;
                foreach (KeyValuePair<string, string[]> entry in books)
                {
                    double distance = Distance(heard, entry.Value);
                    if (distance < first) { second = first; first = distance; best = entry.Key; }
                    else if (distance < second) second = distance;
                }
                // A near-tie is genuinely ambiguous: abstain rather than guess.
                if (best == null || first > MaximumDistance || second - first < MinimumMargin) return null;
                BookRecoverySuggestion suggestion = new BookRecoverySuggestion();
                suggestion.Book = best;
                suggestion.HeardBook = phrase;
                suggestion.Score = 1 - first;
                suggestion.Reason = "Possible book recovery: heard '" + phrase + "'; pronunciation is closest to " + best
                    + ". Confirm before presenting.";
                return suggestion;
            }
        }

        public void Dispose() { lock (gate) voice.Dispose(); }
    }
}
