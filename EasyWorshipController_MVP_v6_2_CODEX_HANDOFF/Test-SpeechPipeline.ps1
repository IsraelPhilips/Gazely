param([ValidateSet('tiny.en','base.en','small.en')][string]$Model = 'base.en')
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$source='using System.Speech.Synthesis;'+$source
$source = $source.Replace('private static void Main()', 'private static void UnusedMain()')
foreach($part in @('PresentationAdapters.cs','GazelyInterface.cs')) {
    $source += [Environment]::NewLine+((Get-Content (Join-Path $PSScriptRoot $part) -Raw -Encoding UTF8) -replace '(?m)^using [^;]+;\r?\n','')
}
$recovery=Get-Content (Join-Path $PSScriptRoot 'ChapterPronunciationRecovery.cs') -Raw -Encoding UTF8
$recovery=$recovery.Replace('SpeechSynthesizer','System.Speech.Synthesis.SpeechSynthesizer').Replace('PhonemeReachedEventArgs','System.Speech.Synthesis.PhonemeReachedEventArgs')
$source += [Environment]::NewLine+($recovery -replace '(?m)^using [^;]+;\r?\n','')
$harness = @"
namespace EasyWorshipControllerMvp {
    internal static class SpeechSmokeTest {
        [System.STAThread]
        private static void Main() {
            string[] phrases = { "Genesis one one", "Genesis chapter one verse one", "Luke one five", "Titus two eleven", "First John three sixteen", "Romans eight twenty eight", "Good morning everyone", "Please look at the screen", "The title of this message is hope" };
            string[] expected = { "Genesis 1:1", "Genesis 1:1", "Luke 1:5", "Titus 2:11", "1 John 3:16", "Romans 8:28", "", "", "" };
            int passed = 0;
            using (WhisperServerEngine engine = new WhisperServerEngine())
            using (System.Speech.Synthesis.SpeechSynthesizer voice = new System.Speech.Synthesis.SpeechSynthesizer()) {
                engine.Start("$Model", null);
                System.Text.StringBuilder report = new System.Text.StringBuilder("phrase\ttranscript\tdetected\texpected\tinference_ms\n");
                for (int i = 0; i < phrases.Length; i++) {
                    byte[] pcm;
                    string wavPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "speech-test.wav");
                    voice.SetOutputToWaveFile(wavPath, new System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, System.Speech.AudioFormat.AudioBitsPerSample.Sixteen, System.Speech.AudioFormat.AudioChannel.Mono));
                    voice.Speak(phrases[i]); voice.SetOutputToNull();
                    using (System.IO.BinaryReader reader = new System.IO.BinaryReader(System.IO.File.OpenRead(wavPath))) {
                        reader.ReadBytes(12); pcm = null;
                        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
                            string tag = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                            int length = reader.ReadInt32();
                            if (tag == "data") { pcm = reader.ReadBytes(length); break; }
                            reader.BaseStream.Seek(length + (length % 2), System.IO.SeekOrigin.Current);
                        }
                    }
                    if (pcm == null) throw new System.Exception("No PCM data in synthesized WAV.");
                    System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
                    string text = engine.Transcribe(pcm, SpeechVocabulary.Prompt);
                    timer.Stop();
                    ScriptureDetection result = new ScriptureDetector().Feed(text);
                    string actual = result == null ? "" : result.ReferenceText;
                    if (actual == expected[i]) passed++;
                    string row = phrases[i] + "\t" + text.Replace("\n", " ") + "\t" + actual + "\t" + expected[i] + "\t" + timer.ElapsedMilliseconds;
                    System.Console.WriteLine(row); report.AppendLine(row);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "speech-smoke-$Model.tsv"), report.ToString());
            }
            System.Console.WriteLine("Synthetic smoke results: " + passed + "/" + phrases.Length + ". This is NOT a real-voice accuracy benchmark.");
        }
    }
}
"@
$refs = @()
foreach ($name in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Speech','WindowsBase','UIAutomationClient','UIAutomationTypes')) {
    Add-Type -AssemblyName $name
    $refs += ([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $name } | Select-Object -First 1).Location
}
$exe = Join-Path $PSScriptRoot 'SpeechPipelineSmokeTest.exe'
if (Test-Path $exe) { Remove-Item -LiteralPath $exe }
Add-Type -TypeDefinition ($source + $harness) -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
& $exe
if ($LASTEXITCODE -ne 0) { throw "Speech smoke test failed: $LASTEXITCODE" }
