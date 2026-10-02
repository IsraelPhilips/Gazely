param([string]$Phrase='for tonight',[int]$Maximum=50)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$speech=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location
$code=@'
using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
public static class NumberPronunciationProbe {
 private static string Words(int n) {
  string[] small={"zero","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen"};
  string[] tens={"","","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"};
  if(n<20)return small[n];if(n<100)return tens[n/10]+(n%10==0?"":" "+small[n%10]);
  return small[n/100]+" hundred"+(n%100==0?"":" "+Words(n%100));
 }
 private static double Distance(string[] a,string[] b) {
  double[] prior=new double[b.Length+1];for(int j=0;j<=b.Length;j++)prior[j]=j;
  for(int i=1;i<=a.Length;i++) {double[] next=new double[b.Length+1];next[0]=i;
   for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(prior[j]+1,next[j-1]+1),prior[j-1]+(a[i-1]==b[j-1]?0:1));prior=next;}
  return prior[b.Length]/Math.Max(a.Length,b.Length);
 }
 public static string Run(string phrase,int maximum) {
  using(SpeechSynthesizer voice=new SpeechSynthesizer()) {
   voice.SetOutputToNull();List<string> sounds=new List<string>();
   voice.PhonemeReached+=delegate(object sender,PhonemeReachedEventArgs e){if(!string.IsNullOrWhiteSpace(e.Phoneme))sounds.Add(e.Phoneme);};
   voice.Speak(phrase);string[] heard=sounds.ToArray();
   if(heard.Length==0)throw new Exception("Installed voice returned no phoneme events");
   List<KeyValuePair<int,double>> scores=new List<KeyValuePair<int,double>>();
   for(int i=1;i<=maximum;i++){sounds.Clear();voice.Speak(Words(i));scores.Add(new KeyValuePair<int,double>(i,Distance(heard,sounds.ToArray())));}
   scores.Sort(delegate(KeyValuePair<int,double>a,KeyValuePair<int,double>b){return a.Value.CompareTo(b.Value);});
   string result="Voice: "+voice.Voice.Name+"\nPhrase: "+phrase+"\nPhonemes: "+string.Join(" ",heard)+"\n";
   for(int i=0;i<Math.Min(5,scores.Count);i++)result+=scores[i].Key+" ("+Words(scores[i].Key)+"): distance "+scores[i].Value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\n";
   return result;
  }
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies $speech
[NumberPronunciationProbe]::Run($Phrase,$Maximum)
