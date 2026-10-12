param([Parameter(Mandatory=$true)][string]$Audio,[Parameter(Mandatory=$true)][string]$Output,[ValidateSet('medium','small','nemotron')][string]$Model='medium')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Choose a new evidence file.'}
$app=Join-Path (Split-Path $PSScriptRoot -Parent) 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
$source=Get-Content (Join-Path $app 'EasyWorshipController.cs') -Raw -Encoding UTF8
function Slice($first,$last){$a=$source.IndexOf($first);$b=$source.IndexOf($last,$a);if($a-lt 0 -or $b-le $a){throw 'Source boundary changed'};$source.Substring($a,$b-$a)}
$code="#pragma warning disable 0649`n"+'using System;using System.IO;using System.Text;using System.Threading;using System.Diagnostics;using System.Collections.Generic;'+(Slice '    internal sealed class SpeechChunk' '    // PCM16 mono')+(Slice '    internal sealed class StreamingSpeechEngine' '    internal sealed class AudioLevelMeter')
$code+=@'
internal static class BridgeTest {
 public static int Main(string[] args){try{return Run(args);}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 private static int Run(string[] args){
  byte[] pcm;using(var input=new BinaryReader(File.OpenRead(args[0]))){
   if(new string(input.ReadChars(4))!="RIFF")throw new Exception("Expected WAV");input.ReadInt32();input.ReadChars(4);pcm=null;
   while(input.BaseStream.Position<input.BaseStream.Length){string name=new string(input.ReadChars(4));int count=input.ReadInt32();if(name=="data"){pcm=input.ReadBytes(count);break;}input.BaseStream.Position+=count+(count%2);}
  }
  int finals=0,partials=0;var errors=new List<string>();var seen=new HashSet<string>();
  using(var log=new StreamWriter(args[1])){
   log.WriteLine("session\tevent\tid\ttext");
   for(int session=1;session<=2;session++){
    using(var engine=new StreamingSpeechEngine()){
     int generation=session;
     engine.Error=delegate(string message){lock(errors)errors.Add(message);};
     engine.Transcript=delegate(string text,SpeechChunk c,bool final,double delay){lock(log){
      if(c.Session!=generation)throw new Exception("Wrong session");
      if(final){finals++;if(!seen.Add(generation+":"+c.UtteranceId))throw new Exception("Duplicate final");}else partials++;
      log.WriteLine(generation+"\t"+(final?"final":"partial")+"\t"+c.UtteranceId+"\t"+text);log.Flush();
     }};
     engine.Start(args[2],generation,delegate(string message){Console.Error.WriteLine(message);});
     var clock=Stopwatch.StartNew();
     for(int offset=0;offset<pcm.Length;offset+=3200){int count=Math.Min(3200,pcm.Length-offset);byte[] data=new byte[count];Buffer.BlockCopy(pcm,offset,data,0,count);engine.AddAudio(data);int wait=(int)((offset+count)/32.0-clock.Elapsed.TotalMilliseconds);if(wait>0)Thread.Sleep(wait);}
     engine.Finish();
    }
   }
  }
  if(errors.Count>0 || finals<2 || partials<2)throw new Exception("Bridge failed: "+string.Join(";",errors.ToArray()));
  Console.WriteLine("PASS: two fresh sessions; "+finals+" finals, "+partials+" partials; EOF flushed, no duplicate finals or errors.");return 0;
 }
}
'@
$exe=Join-Path $app ('MoonshineTest-'+[Guid]::NewGuid().ToString('N')+'.exe')
try{
 Add-Type -TypeDefinition $code -ReferencedAssemblies System.dll,System.Core.dll -OutputAssembly $exe -OutputType ConsoleApplication
 & $exe ([IO.Path]::GetFullPath($Audio)) ([IO.Path]::GetFullPath($Output)) $Model
 if($LASTEXITCODE -ne 0){throw 'Bridge test failed; evidence retained.'}
}finally{if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe}}
