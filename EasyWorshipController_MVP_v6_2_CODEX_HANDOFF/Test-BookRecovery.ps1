$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$speech=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location
$source=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start=$source.IndexOf('    internal static class BibleCanon')
$end=$source.IndexOf('    internal sealed class PassageSelectionPolicy')
$canon=$source.Substring($start,$end-$start)
$recovery=Get-Content (Join-Path $PSScriptRoot 'BookPronunciationRecovery.cs') -Raw -Encoding UTF8
$code='using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; using System.Speech.Synthesis; namespace EasyWorshipControllerMvp {'+$canon+'}'+($recovery -replace '(?m)^using [^;]+;\r?\n','')
$code+=@'
namespace EasyWorshipControllerMvp {
 public static class BookRecoveryTests {
  public static string Run() {
   // Filler stripping is pure string work, so it is checked without the voice.
   string[] fillers={
     "no Micah","no, Micah","no, I mean, Micah","sorry, give me Micah",
     "correction rather Micah","I mean it is Micah"
   };
   foreach(string text in fillers){
     string suspect=BookPronunciationRecovery.Suspect(text);
     if(suspect!="Micah" && suspect!="Micah Micah")
       throw new Exception("Filler was not stripped from '"+text+"' -> '"+suspect+"'");
   }
   string[] notCues={"Micah","give me Micah","and then Micah said"};
   foreach(string text in notCues)
     if(BookPronunciationRecovery.Suspect(text)!=string.Empty)
       throw new Exception("A phrase without a correction cue was treated as a correction: "+text);

   var timer=System.Diagnostics.Stopwatch.StartNew();
   using(BookPronunciationRecovery recovery=new BookPronunciationRecovery()) {
    long setup=timer.ElapsedMilliseconds;

    // The reported case: the recognizer heard "mica", not "Micah".
    BookRecoverySuggestion mica=recovery.Find("no mica");
    if(mica==null || mica.Book!="Micah")
      throw new Exception("Misheard 'mica' did not recover Micah; got "+(mica==null?"null":mica.Book));

    // The recognizer's worst rendering of the same correction in the recording.
    BookRecoverySuggestion lagai=recovery.Find("no my lagai");
    if(lagai!=null && lagai.Book!="Micah")
      throw new Exception("Recovered a different book from 'my lagai': "+lagai.Book);

    // Negatives: these must not become book names.
    string[] negatives={"no the subject","no next week","no we will see","rather the same thing"};
    foreach(string text in negatives){
      BookRecoverySuggestion bad=recovery.Find(text);
      if(bad!=null)throw new Exception("False book candidate from '"+text+"': "+bad.Book);
    }

    // The distance function itself.
    if(BookPronunciationRecovery.Distance(new[]{"a","b"},new[]{"a","b"})!=0)
      throw new Exception("Identical sequences must have zero distance");
    if(BookPronunciationRecovery.Distance(new[]{"a","b"},new[]{"c","d"})!=1)
      throw new Exception("Fully different equal-length sequences must have distance 1");

    return "PASS: book-recovery cases; voice="+recovery.VoiceName+"; initialization="+setup+"ms; checks="+(timer.ElapsedMilliseconds-setup)+"ms. 'mica' recovers Micah, filler is stripped, ambiguities abstain; results are never automatic.";
   }
  }
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies $speech -IgnoreWarnings
[EasyWorshipControllerMvp.BookRecoveryTests]::Run()
