$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$speech=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location
$source=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start=$source.IndexOf('    internal sealed class ScriptureDetection');$end=$source.IndexOf('    internal sealed class SpeechChunk')
$core=$source.Substring($start,$end-$start)
$recovery=Get-Content (Join-Path $PSScriptRoot 'ChapterPronunciationRecovery.cs') -Raw -Encoding UTF8
$code='using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; using System.Speech.Synthesis; namespace EasyWorshipControllerMvp {'+$core+'}'+($recovery -replace '(?m)^using [^;]+;\r?\n','')
$code+=@'
namespace EasyWorshipControllerMvp {
 public static class ChapterRecoveryTests {
  public static string Run() {
   var timer=System.Diagnostics.Stopwatch.StartNew();
   using(ChapterPronunciationRecovery recovery=new ChapterPronunciationRecovery()) {
    long setup=timer.ElapsedMilliseconds;
    string original="In the Genesis chapter for tonight, verse 8, 3, and verse 4.";
    ChapterRecoverySuggestion uncertain=recovery.Find("Genesis",original);
    if(uncertain==null || uncertain.Chapter!=49 || uncertain.Reference!=null)throw new Exception("Conflicting verses were fabricated or chapter recovery failed");
    ChapterRecoverySuggestion range=recovery.Find("Genesis","Genesis chapter for tonight verse 3 and verse 4");
    if(range==null || range.Reference==null || range.Reference.ReferenceText!="Genesis 49:3-4" || !range.Reference.RequiresReview || range.Reference.Confidence>=.9)throw new Exception("Recovered reference lost review-only policy");
    ChapterRecoverySuggestion another=recovery.Find("Genesis","Genesis chapter twenty tree verse one");
    if(another==null || another.Chapter!=23)throw new Exception("Second pronunciation example failed");
    string[] negatives={"Genesis chapter for today verse 3","Genesis chapter the subject verse 3","We are getting ready for tonight at eight thirty four","Genesis chapter next week verse 3"};
    foreach(string text in negatives)if(recovery.Find("Genesis",text)!=null)throw new Exception("False chapter candidate: "+text);
    if(recovery.Find("Titus","Titus chapter for tonight verse 3")!=null)throw new Exception("Book's chapter limit was ignored");
    ChapterRecoverySuggestion invalid=recovery.Find("Genesis","Genesis chapter for tonight verse 99");
    if(invalid==null || invalid.Reference!=null)throw new Exception("Invalid verse was accepted");
    return "PASS: 9 pronunciation-recovery cases; voice="+recovery.VoiceName+"; initialization="+setup+"ms; checks="+(timer.ElapsedMilliseconds-setup)+"ms. Original conflicting verse sequence yields chapter 49 only; no presentation.";
   }
  }
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies $speech -IgnoreWarnings
[EasyWorshipControllerMvp.ChapterRecoveryTests]::Run()
