$ErrorActionPreference='Stop'
$testData=Join-Path $PSScriptRoot ('.lifecycle-'+[guid]::NewGuid().ToString('N'))
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))
$source='using System.Speech.Synthesis;using System.Linq;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Threading.Tasks;'+$source.Replace('private static void Main()','private static void LaunchApplication()')
# Substitute only the external model/device boundary. Exercise the actual form
# lifecycle, callbacks, control states, restart button and session validation.
$source=$source.Replace('PopulateAudioDevices();','/* fake device inventory */').Replace('PopulateRecognizers();','/* no Windows recognizer */')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{loadingWhisper.Start(model,AddLog);});','await LifecycleFixture.Load();')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{loadingStream.Start("nemotron",generation,AddLog);});','await LifecycleFixture.Load();')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{loadingStream.Start(selected,generation,AddLog);});','await LifecycleFixture.Load(selected);')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{parakeet.Start("parakeet",generation,AddLog);});','await LifecycleFixture.Load("parakeet");')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{whisperFallback.Start(fallbackModel,AddLog);});','await LifecycleFixture.Load("whisper");')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{next.Start("parakeet",generation,AddLog);});','await LifecycleFixture.Load("parakeet");')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{nextWhisper.Start(model,AddLog);});','await LifecycleFixture.Load("whisper");')
$source=$source.Replace('audioRecorder.Start();','LifecycleFixture.Capture();').Replace('if(useNativeStreaming)engine.Finish();','if(useNativeStreaming)LifecycleFixture.Flush();').Replace('if(useNativeStreaming && engine!=null)engine.Finish();','if(useNativeStreaming && engine!=null)LifecycleFixture.Flush();')
foreach($file in @('PresentationAdapters.cs','GazelyInterface.cs','ChapterPronunciationRecovery.cs','BookPronunciationRecovery.cs','ScripturePlan.cs','GazelyCloud.cs')){
 $source+=[Environment]::NewLine+([IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)) -replace '(?m)^using [^;]+;\r?\n','')
}
$source+=@'
namespace EasyWorshipControllerMvp {
 internal static class LifecycleFixture {
  public static bool FailLoad,FailCapture,FailFlush;
  public static string FailLoadEngine;
  public static int Starts,Flushes;
  public static System.Threading.Tasks.Task Load(){return Load("whisper");}
  public static async System.Threading.Tasks.Task Load(string engine){Starts++;await System.Threading.Tasks.Task.Delay(350);if(FailLoad || FailLoadEngine==engine)throw new IOException("fixture startup failure: "+engine);}
  public static void Capture(){if(FailCapture)throw new IOException("fixture disconnected device");}
  public static void Flush(){Flushes++;Thread.Sleep(120);if(FailFlush)throw new IOException("fixture flush failure");}
 }
 internal static class LifecycleTest {
  static System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
  static int waits;
  static object Get(MainForm f,string n){return typeof(MainForm).GetField(n,flags).GetValue(f);}
  static void Call(MainForm f,string n,params object[] a){typeof(MainForm).GetMethod(n,flags).Invoke(f,a);}
  static void Until(Func<bool> condition){int current=++waits;var t=Stopwatch.StartNew();while(!condition()){Application.DoEvents();Thread.Sleep(10);if(t.ElapsedMilliseconds>5000)throw new Exception("Lifecycle timeout #"+current);}Application.DoEvents();}
  static void Require(bool value,string message){if(!value)throw new Exception(message);}
  [STAThread]public static int Main(){Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);int result=1;EventHandler run=null;run=delegate{Application.Idle-=run;result=Run();Application.ExitThread();};Application.Idle+=run;Application.Run(new ApplicationContext());return result;}
  static int Run(){try {
  string testData=Environment.GetEnvironmentVariable("GAZELY_LIFECYCLE_TEST_DATA");Directory.CreateDirectory(testData);
  WorkspacePreferences.PathOverride=Path.Combine(testData,"preferences.txt");
  GazelyCloudAccess.PathOverride=Path.Combine(testData,"account");
   WorkspacePreferences.Save(new Dictionary<string,string>{{"onboarding.shown","True"}});
   using(var f=new MainForm()) {
    try {
    f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();
    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    var onboardingPrefs=WorkspacePreferences.Load();onboardingPrefs.Remove("onboarding.shown");WorkspacePreferences.Save(onboardingPrefs);
    var startupMap=(Dictionary<string,int>)Get(f,"versionMap");startupMap.Clear();startupMap["KJV"]=0;Call(f,"SelectPage","speech");Call(f,"ShowFirstRunGuide");
    var pages=(TabControl)Get(f,"workspacePages");Require(pages.SelectedTab.Name=="workspace" && WorkspacePreferences.Load()["onboarding.shown"]=="True","An existing Bible map did not bypass first-run setup and land on Live workspace");
    Call(f,"LoadVersionMap");
    var input=(ComboBox)Get(f,"audioDeviceCombo");input.Items.Add(new AudioDeviceInfo{Id=0,Name="Fixture microphone"});input.SelectedIndex=0;
    Call(f,"SelectPage","speech");Application.DoEvents();
    var engine=(ComboBox)Get(f,"speechEngineCombo");engine.SelectedIndex=1;
    var clock=Stopwatch.StartNew();Call(f,"StartListening");
    Require(clock.ElapsedMilliseconds<200,"Model startup blocked the UI");
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Starting,"Starting state missing");
    Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    Require(engine.Enabled && input.Enabled,"Settings cannot be edited while listening: engine="+engine.Enabled+" input="+input.Enabled+" starting="+Get(f,"startingListening")+" draining="+Get(f,"drainingStreaming")+" state="+Get(f,"listeningState")+" parent="+engine.Parent.Enabled+" form="+f.Enabled);
    var originalStream=Get(f,"streamingEngine");var errorField=originalStream.GetType().GetField("Error",flags);((Action<string>)errorField.GetValue(originalStream))("fixture connection reset");
    Until(delegate{return (int)Get(f,"speechEngineEpoch")==1 && (int)Get(f,"failoverInProgress")==0;});
    Require((bool)Get(f,"useNativeStreaming") && !object.ReferenceEquals(originalStream,Get(f,"streamingEngine")) && LifecycleFixture.Starts==2,"Runtime Nemotron failure did not hand off to Parakeet");
    int oldSession=(int)Get(f,"listeningSession");
    engine.SelectedIndex=0; // Pending changes must not mutate the active engine.
    Require((bool)Get(f,"useNativeStreaming"),"Pending engine change modified current stream");
    ((Button)Get(f,"applySpeechSettings")).PerformClick();
    Require((bool)Get(f,"startingListening"),"Apply did not show transition state");
    Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening") && (int)Get(f,"listeningSession")!=oldSession;});
    Require(!(bool)Get(f,"useNativeStreaming") && LifecycleFixture.Starts==3 && LifecycleFixture.Flushes==1,"Apply did not drain once and start exactly one new engine after failover: native="+Get(f,"useNativeStreaming")+" starts="+LifecycleFixture.Starts+" flushes="+LifecycleFixture.Flushes+" state="+Get(f,"listeningState"));
    Call(f,"OnFinalTranscript","John chapter 3 verse 16",1.0,"stale fixture",0.0,oldSession,null);Application.DoEvents();
    Require(!((TextBox)Get(f,"transcriptTextBox")).Text.Contains("John chapter 3 verse 16"),"Old session callback survived restart");
    Call(f,"StopListening");Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    var working=(string[])Get(f,"workingSpeechSettings");
    LifecycleFixture.FailLoad=true;engine.SelectedIndex=1;Call(f,"StartListening");
    Until(delegate{return !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed,"Startup failure has no Failed state");
    Require(object.ReferenceEquals(working,Get(f,"workingSpeechSettings")),"Failed startup overwrote last working settings");
    LifecycleFixture.FailLoad=false;LifecycleFixture.FailLoadEngine=null;Call(f,"StartListening");Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    LifecycleFixture.FailFlush=true;int starts=LifecycleFixture.Starts;((Button)Get(f,"applySpeechSettings")).PerformClick();
    Until(delegate{return !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed && LifecycleFixture.Starts==starts,"Failed flush silently restarted");
    LifecycleFixture.FailFlush=false;LifecycleFixture.FailCapture=true;Call(f,"StartListening");
    Until(delegate{return !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed,"Device failure was not surfaced");
    if((bool)Get(f,"audioListening")){Call(f,"StopListening");Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});}
    LifecycleFixture.FailCapture=false;LifecycleFixture.FailLoadEngine="nemotron";Call(f,"StartListening");try{Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});}catch{throw new Exception("Startup fallback state: listening="+Get(f,"audioListening")+" starting="+Get(f,"startingListening")+" epoch="+Get(f,"speechEngineEpoch")+" state="+Get(f,"listeningState")+" engine="+((string)typeof(MainForm).GetProperty("SelectedSpeechEngineId",flags).GetValue(f))+" starts="+LifecycleFixture.Starts+" native="+Get(f,"useNativeStreaming")+" localAi="+Get(f,"useLocalAi")+" streamNull="+(Get(f,"streamingEngine")==null)+" failoverInProgress="+Get(f,"failoverInProgress")+" draining="+Get(f,"drainingStreaming")+" closing="+Get(f,"closingApplication")+" session="+Get(f,"listeningSession"));}
    Require((bool)Get(f,"useNativeStreaming") && (int)Get(f,"speechEngineEpoch")==1,"Nemotron startup failure did not select Parakeet");
    Call(f,"StopListening");Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    LifecycleFixture.FailLoadEngine=null;Call(f,"StartListening");Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    var nemo=(StreamingSpeechEngine)Get(f,"streamingEngine");((Action<string>)nemo.GetType().GetField("Error",flags).GetValue(nemo))("fixture reset one");Until(delegate{return (int)Get(f,"speechEngineEpoch")==1 && (int)Get(f,"failoverInProgress")==0;});
    var parakeet=(StreamingSpeechEngine)Get(f,"streamingEngine");((Action<string>)parakeet.GetType().GetField("Error",flags).GetValue(parakeet))("fixture reset two");Until(delegate{return (int)Get(f,"speechEngineEpoch")==2 && (int)Get(f,"failoverInProgress")==0 && (bool)Get(f,"useLocalAi");});
    Require(!(bool)Get(f,"useNativeStreaming") && Get(f,"streamingEngine")==null,"Parakeet failure did not fall through to Whisper");
    Call(f,"StopListening");Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    f.Close();
    if(Directory.Exists(GazelyCloudAccess.PathOverride))Directory.Delete(GazelyCloudAccess.PathOverride,true);
    }catch(Exception ex){Console.Error.WriteLine("Lifecycle body: "+ex);throw;}
   }
   Console.WriteLine("PASS: returning Bible-map startup opens Live workspace; responsive startup, Nemotron→Parakeet→Whisper failover, drain/restart, stale sessions and failure handling.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 }
}
'@
$refs=@();foreach($name in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Speech','System.Security','System.Web.Extensions','WindowsBase','UIAutomationClient','UIAutomationTypes')){
 Add-Type -AssemblyName $name
 $refs+=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq $name}|Select-Object -First 1).Location
}
$exe=Join-Path $PSScriptRoot ('LifecycleTest-'+[guid]::NewGuid().ToString('N')+'.exe')
$previousTestData=$env:GAZELY_LIFECYCLE_TEST_DATA
try{$env:GAZELY_LIFECYCLE_TEST_DATA=$testData
 Add-Type -TypeDefinition $source -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Speech lifecycle regression failed.'}
}finally{$env:GAZELY_LIFECYCLE_TEST_DATA=$previousTestData
 if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe}
 if(Test-Path -LiteralPath $testData){Remove-Item -LiteralPath $testData -Recurse -Force}}
