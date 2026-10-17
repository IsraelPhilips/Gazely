$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))
$source='using System.Speech.Synthesis;'+$source.Replace('private static void Main()','private static void LaunchApplication()')
# Substitute only the external model/device boundary. Exercise the actual form
# lifecycle, callbacks, control states, restart button and session validation.
$source=$source.Replace('PopulateAudioDevices();','/* fake device inventory */').Replace('PopulateRecognizers();','/* no Windows recognizer */')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{loadingWhisper.Start(model,AddLog);});','await LifecycleFixture.Load();')
$source=$source.Replace('await System.Threading.Tasks.Task.Run(delegate{loadingStream.Start("nemotron",generation,AddLog);});','await LifecycleFixture.Load();')
$source=$source.Replace('audioRecorder.Start();','LifecycleFixture.Capture();').Replace('if(useNativeStreaming)engine.Finish();','if(useNativeStreaming)LifecycleFixture.Flush();')
foreach($file in @('PresentationAdapters.cs','GazelyInterface.cs','ChapterPronunciationRecovery.cs')){
 $source+=[Environment]::NewLine+([IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)) -replace '(?m)^using [^;]+;\r?\n','')
}
$source+=@'
namespace EasyWorshipControllerMvp {
 internal static class LifecycleFixture {
  public static bool FailLoad,FailCapture,FailFlush;
  public static int Starts,Flushes;
  public static async System.Threading.Tasks.Task Load(){Starts++;await System.Threading.Tasks.Task.Delay(350);if(FailLoad)throw new IOException("fixture startup failure");}
  public static void Capture(){if(FailCapture)throw new IOException("fixture disconnected device");}
  public static void Flush(){Flushes++;Thread.Sleep(120);if(FailFlush)throw new IOException("fixture flush failure");}
 }
 internal static class LifecycleTest {
  static System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  static object Get(MainForm f,string n){return typeof(MainForm).GetField(n,flags).GetValue(f);}
  static void Call(MainForm f,string n,params object[] a){typeof(MainForm).GetMethod(n,flags).Invoke(f,a);}
  static void Until(Func<bool> condition){var t=Stopwatch.StartNew();while(!condition()){Application.DoEvents();Thread.Sleep(10);if(t.ElapsedMilliseconds>5000)throw new Exception("Lifecycle timeout");}Application.DoEvents();}
  static void Require(bool value,string message){if(!value)throw new Exception(message);}
  [STAThread]public static int Main(){Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);int result=1;EventHandler run=null;run=delegate{Application.Idle-=run;result=Run();Application.ExitThread();};Application.Idle+=run;Application.Run(new ApplicationContext());return result;}
  static int Run(){try {
   WorkspacePreferences.PathOverride=Path.Combine(Path.GetTempPath(),"gazely-lifecycle-"+Guid.NewGuid().ToString("N")+".txt");
   WorkspacePreferences.Save(new Dictionary<string,string>{{"onboarding.shown","True"}});
   using(var f=new MainForm()) {
    try {
    f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();
    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    var input=(ComboBox)Get(f,"audioDeviceCombo");input.Items.Add(new AudioDeviceInfo{Id=0,Name="Fixture microphone"});input.SelectedIndex=0;
    Call(f,"SelectPage","speech");Application.DoEvents();
    var engine=(ComboBox)Get(f,"speechEngineCombo");engine.SelectedIndex=1;
    var clock=Stopwatch.StartNew();Call(f,"StartListening");
    Require(clock.ElapsedMilliseconds<200,"Model startup blocked the UI");
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Starting,"Starting state missing");
    Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    Require(engine.Enabled && input.Enabled,"Settings cannot be edited while listening: engine="+engine.Enabled+" input="+input.Enabled+" starting="+Get(f,"startingListening")+" draining="+Get(f,"drainingStreaming")+" state="+Get(f,"listeningState")+" parent="+engine.Parent.Enabled+" form="+f.Enabled);
    int oldSession=(int)Get(f,"listeningSession");
    engine.SelectedIndex=0; // Pending changes must not mutate the active engine.
    Require((bool)Get(f,"useNativeStreaming"),"Pending engine change modified current stream");
    ((Button)Get(f,"applySpeechSettings")).PerformClick();
    Require((bool)Get(f,"startingListening"),"Apply did not show transition state");
    Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening") && (int)Get(f,"listeningSession")!=oldSession;});
    Require(!(bool)Get(f,"useNativeStreaming") && LifecycleFixture.Starts==2 && LifecycleFixture.Flushes==1,"Apply did not drain once and start exactly one new engine");
    Call(f,"OnFinalTranscript","John chapter 3 verse 16",1.0,"stale fixture",0.0,oldSession,null);Application.DoEvents();
    Require(!((TextBox)Get(f,"transcriptTextBox")).Text.Contains("John chapter 3 verse 16"),"Old session callback survived restart");
    Call(f,"StopListening");Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    var working=(string[])Get(f,"workingSpeechSettings");
    LifecycleFixture.FailLoad=true;engine.SelectedIndex=1;Call(f,"StartListening");
    Until(delegate{return !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed,"Startup failure has no Failed state");
    Require(object.ReferenceEquals(working,Get(f,"workingSpeechSettings")),"Failed startup overwrote last working settings");
    LifecycleFixture.FailLoad=false;Call(f,"StartListening");Until(delegate{return (bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    LifecycleFixture.FailFlush=true;int starts=LifecycleFixture.Starts;((Button)Get(f,"applySpeechSettings")).PerformClick();
    Until(delegate{return !(bool)Get(f,"audioListening") && !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed && LifecycleFixture.Starts==starts,"Failed flush silently restarted");
    LifecycleFixture.FailFlush=false;LifecycleFixture.FailCapture=true;Call(f,"StartListening");
    Until(delegate{return !(bool)Get(f,"startingListening");});
    Require((ListeningState)Get(f,"listeningState")==ListeningState.Failed,"Device failure was not surfaced");
    f.Close();
    }catch(Exception ex){Console.Error.WriteLine("Lifecycle body: "+ex);throw;}
   }
   Console.WriteLine("PASS: responsive startup, editable pending settings, drain/restart, stale sessions, startup/device/flush failure and working-config retention.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 }
}
'@
$refs=@();foreach($name in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Speech','WindowsBase','UIAutomationClient','UIAutomationTypes')){
 Add-Type -AssemblyName $name
 $refs+=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq $name}|Select-Object -First 1).Location
}
$exe=Join-Path $PSScriptRoot ('LifecycleTest-'+[guid]::NewGuid().ToString('N')+'.exe')
try{Add-Type -TypeDefinition $source -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Speech lifecycle regression failed.'}
}finally{if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe}}
