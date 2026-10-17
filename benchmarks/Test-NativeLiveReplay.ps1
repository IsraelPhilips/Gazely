param([Parameter(Mandatory=$true)][string]$Audio,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$outputPath=[IO.Path]::GetFullPath($Output)
if(Test-Path -LiteralPath $outputPath){throw 'Choose a new evidence directory.'}
New-Item -ItemType Directory -Path $outputPath | Out-Null
$app=Join-Path (Split-Path $PSScriptRoot -Parent) 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
$source=[IO.File]::ReadAllText((Join-Path $app 'EasyWorshipController.cs')).Replace('private static void Main()','private static void LaunchApplication()')
$source='using System.Speech.Synthesis;'+$source
foreach($file in @('PresentationAdapters.cs','GazelyInterface.cs','ChapterPronunciationRecovery.cs')){
 $source+=[Environment]::NewLine+([IO.File]::ReadAllText((Join-Path $app $file)) -replace '(?m)^using [^;]+;\r?\n','')
}
$replay=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ReplayHarness.cs'))
$start=$replay.IndexOf('    internal static byte[] ReadPcm')
$end=$replay.IndexOf('    private void SaveChunk',$start)
$reader=$replay.Substring($start,$end-$start)
$source+=@'
namespace EasyWorshipControllerMvp {
internal sealed class NoOutputAdapter : IPresentationAdapter {
 public string Id {get{return "replay";}} public string Name {get{return "Replay only";}}
 public PresentationCapabilities Capabilities {get{return PresentationCapabilities.Scripture|PresentationCapabilities.Present|PresentationCapabilities.Prepare;}}
 public int Calls;public bool Probe(){return true;}public void ResetSession(){}
 public PresentationResult Execute(PresentationIntent intent,Func<bool> canceled){Calls++;throw new Exception("Replay must never present");}
}
internal static class NativeLiveReplay {
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]
 private static extern uint SetThreadExecutionState(uint flags);
 private static System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 private static object Get(object o,string name){return o.GetType().GetField(name,flags).GetValue(o);}
 private static void Set(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
 private static void Call(object o,string name,params object[] args){o.GetType().GetMethod(name,flags).Invoke(o,args);}
 private static string Cell(string text){return (text??"").Replace("\t"," ").Replace("\r"," ").Replace("\n"," ");}
 [STAThread]public static int Main(string[] args){
  // Keep this paced replay awake; the previous run was interrupted by battery sleep.
  SetThreadExecutionState(0x80000001);
  try{return Run(args);}catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally{SetThreadExecutionState(0x80000000);}
 }
 private static int Run(string[] args){
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
  WorkspacePreferences.PathOverride=Path.Combine(args[1],"preferences.txt");
  byte[] pcm=ReadPcm(args[0]);var clock=new Stopwatch();
  var errors=new List<string>();var finalText=new StringBuilder();var finalIds=new HashSet<long>();
  int partials=0,finals=0;long maxEnd=0,peak=0;double backlog=0,flush=0;
  using(var form=new MainForm())using(var engine=new StreamingSpeechEngine())using(var log=new StreamWriter(Path.Combine(args[1],"events.tsv"))){
   var adapter=new NoOutputAdapter();var destination=new PresentationCoordinator(adapter);destination.SetEnabled(true);Set(form,"destination",destination);
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   foreach(SpeechEngineChoice choice in ((ComboBox)Get(form,"speechEngineCombo")).Items)if(choice.Id=="nemotron")((ComboBox)Get(form,"speechEngineCombo")).SelectedItem=choice;
   Set(form,"useNativeStreaming",true);Set(form,"streamingEngine",engine);Set(form,"listeningSession",777);Set(form,"audioListening",true);
   log.WriteLine("wall_ms\tkind\tid\tstart_sample\tend_sample\ttext");
   engine.Progress=delegate(long received,long acknowledged,int queued,double turnaround){lock(log){log.WriteLine(clock.Elapsed.TotalMilliseconds.ToString("F1")+"\ttransport\t0\t"+received+"\t"+acknowledged+"\tqueue_bytes="+queued+";ack_roundtrip_ms="+turnaround);log.Flush();}};
   engine.Error=delegate(string error){lock(errors)errors.Add(error);};
   engine.Transcript=delegate(string text,SpeechChunk chunk,bool final,double delay){
    lock(log){
     if(chunk.Session!=777)throw new Exception("Stale session result");
     if(final){if(!finalIds.Add(chunk.UtteranceId))throw new Exception("Duplicate final");finals++;if(!string.IsNullOrWhiteSpace(text))finalText.AppendLine(text);}else partials++;
     maxEnd=Math.Max(maxEnd,chunk.AudioEndSample);
     log.WriteLine(clock.Elapsed.TotalMilliseconds.ToString("F1")+"\t"+(final?"final":"partial")+"\t"+chunk.UtteranceId+"\t"+chunk.AudioStartSample+"\t"+chunk.AudioEndSample+"\t"+Cell(text));log.Flush();
    }
    if(final)Call(form,"OnFinalTranscript",text,1.0,"Nemotron",delay,777,chunk);else Call(form,"OnPartialTranscript",text,chunk,delay);
   };
   engine.Start("nemotron",777,delegate(string message){Console.Error.WriteLine(message);});clock.Start();
   string previous="";long lastResourceSample=-1000,lastPumpMs=-1;
   Action pump=delegate{
    long nowMs=clock.ElapsedMilliseconds;
    if(lastPumpMs>=0 && nowMs-lastPumpMs>15000)
     throw new Exception("Paced replay interrupted by a wall-clock gap of "+(nowMs-lastPumpMs)+" ms");
    lastPumpMs=nowMs;
    Application.DoEvents();backlog=Math.Max(backlog,engine.BacklogSeconds);
    if(clock.ElapsedMilliseconds-lastResourceSample>=1000){lastResourceSample=clock.ElapsedMilliseconds;long memory=Process.GetCurrentProcess().WorkingSet64;
    try{var speechWorker=(Process)Get(engine,"worker");speechWorker.Refresh();memory+=speechWorker.WorkingSet64;if(engine.WorkerProcessId>0 && engine.WorkerProcessId!=speechWorker.Id)using(var python=Process.GetProcessById(engine.WorkerProcessId))memory+=python.WorkingSet64;using(var native=Process.GetProcessById(engine.NativeProcessId))memory+=native.WorkingSet64;}catch(InvalidOperationException){}catch(ArgumentException){}
    var search=(OfflineSemanticSearch)Get(form,"semanticSearch");
    Process searchWorker=(Process)Get(search,"worker");if(searchWorker!=null)try{searchWorker.Refresh();memory+=searchWorker.WorkingSet64;}catch(InvalidOperationException){}
    peak=Math.Max(peak,memory);lock(log){log.WriteLine(clock.ElapsedMilliseconds+"\tresources\t0\t0\t0\trss_bytes="+memory+";quote_jobs="+((Queue<Action>)Get(form,"quotationJobs")).Count);log.Flush();}}
    string selected=((Label)Get(form,"detectedReferenceLabel")).Text;
    if(selected!=previous){lock(log){log.WriteLine(clock.Elapsed.TotalMilliseconds.ToString("F1")+"\tSP\t0\t0\t0\t"+Cell(selected));log.Flush();}previous=selected;}
    lock(errors)if(errors.Count>0)throw new Exception(string.Join("; ",errors.ToArray()));
   };
   for(int offset=0;offset<pcm.Length;offset+=3200){
    int count=Math.Min(3200,pcm.Length-offset);byte[] packet=new byte[count];Buffer.BlockCopy(pcm,offset,packet,0,count);
    Call(form,"OnAudioData",packet);
    while(clock.Elapsed.TotalMilliseconds<(offset+count)/32.0){pump();Thread.Sleep(10);}pump();
   }
   var tail=Stopwatch.StartNew();Exception failure=null;bool finished=false;
   var finisher=new Thread(delegate(){try{engine.Finish();}catch(Exception e){failure=e;}finally{finished=true;}});finisher.Start();
   while(!finished){pump();Thread.Sleep(10);}finisher.Join();pump();flush=tail.Elapsed.TotalMilliseconds;
   if(failure!=null)throw failure;
   var settle=Stopwatch.StartNew();while(settle.ElapsedMilliseconds<10000){pump();if(!(bool)Get(form,"quotationWorker"))break;Thread.Sleep(20);}
   var history=(ListView)Get(form,"suggestionHistory");
   using(var detections=new StreamWriter(Path.Combine(args[1],"detections.tsv")))foreach(ListViewItem row in history.Items){foreach(ListViewItem.ListViewSubItem cell in row.SubItems)detections.Write(Cell(cell.Text)+"\t");detections.WriteLine();}
   File.WriteAllText(Path.Combine(args[1],"transcript.txt"),finalText.ToString());
   File.WriteAllText(Path.Combine(args[1],"ui-transcript.txt"),((TextBox)Get(form,"transcriptTextBox")).Text);
   File.WriteAllText(Path.Combine(args[1],"application-log.txt"),((TextBox)Get(form,"logTextBox")).Text);
   File.WriteAllText(Path.Combine(args[1],"summary.txt"),"audio_seconds="+pcm.Length/32000.0+"\npartials="+partials+"\nfinals="+finals+"\nmax_backlog_seconds="+backlog+"\npeak_combined_rss_bytes="+peak+"\nflush_ms="+flush+"\nmax_audio_end_sample="+maxEnd+"\ncandidates="+history.Items.Count+"\nadapter_calls="+adapter.Calls);
   if(adapter.Calls!=0 || finals==0 || partials==0 || engine.BacklogSeconds>.2)throw new Exception("Replay did not finish safely");
   Set(form,"audioListening",false);form.Close();
  }
  Console.WriteLine("PASS: production live callbacks, parser, quotation search and inline transcript replay; no microphone or output actions.");return 0;
 }
 // WAV_READER
}
}
'@
$source=$source.Replace(' // WAV_READER',$reader)
$refs=@();foreach($name in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Speech','WindowsBase','UIAutomationClient','UIAutomationTypes')){
 Add-Type -AssemblyName $name
 $refs+=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq $name}|Select-Object -First 1).Location
}
$exe=Join-Path $app ('NativeReplay-'+[guid]::NewGuid().ToString('N')+'.exe')
try{
 Add-Type -TypeDefinition $source -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
 & $exe ([IO.Path]::GetFullPath($Audio)) $outputPath
 if($LASTEXITCODE -ne 0){throw 'Native live replay failed; evidence retained.'}
}finally{if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe}}
