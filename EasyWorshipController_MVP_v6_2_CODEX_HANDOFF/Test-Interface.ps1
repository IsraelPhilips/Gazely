param([Parameter(Mandatory=$true)][string]$OutputDirectory,[int]$Width=1020,[int]$Height=780,[switch]$Light,[switch]$Pronunciation)
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new output directory to preserve screenshots.'}
New-Item -ItemType Directory -Path $output | Out-Null
$source=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$source='using System.Speech.Synthesis;using System.Linq;'+$source
$source=$source.Replace('private static void Main()','private static void LaunchApplication()')
$adapters=Get-Content (Join-Path $PSScriptRoot 'PresentationAdapters.cs') -Raw -Encoding UTF8
$source += [Environment]::NewLine+($adapters -replace '(?m)^using [^;]+;\r?\n','')
$interface=Get-Content (Join-Path $PSScriptRoot 'GazelyInterface.cs') -Raw -Encoding UTF8
$source += [Environment]::NewLine+($interface -replace '(?m)^using [^;]+;\r?\n','')
$recovery=Get-Content (Join-Path $PSScriptRoot 'ChapterPronunciationRecovery.cs') -Raw -Encoding UTF8
# The main source has no synthesis import; qualify it within this namespace block.
$recovery=$recovery.Replace('namespace EasyWorshipControllerMvp','namespace EasyWorshipControllerMvp').Replace('using System.Speech.Synthesis;','')
$recovery=$recovery.Replace('SpeechSynthesizer','System.Speech.Synthesis.SpeechSynthesizer').Replace('PhonemeReachedEventArgs','System.Speech.Synthesis.PhonemeReachedEventArgs')
$source += [Environment]::NewLine+($recovery -replace '(?m)^using [^;]+;\r?\n','')
$source+=@'
namespace EasyWorshipControllerMvp {
 internal sealed class CandidateTestAdapter : IPresentationAdapter {
  public string Id {get{return "test";}} public string Name {get{return "Test";}}
  public PresentationCapabilities Capabilities {get{return PresentationCapabilities.Scripture|PresentationCapabilities.VerseRanges|PresentationCapabilities.Present|PresentationCapabilities.Prepare;}}
  public PresentationIntent Last; public int Calls;
  public bool Probe(){return true;} public void ResetSession(){}
  public PresentationResult Execute(PresentationIntent intent,Func<bool> canceled){Last=intent;Calls++;return new PresentationResult(PresentationOutcome.SentUnverified,"Test adapter only");}
 }
 internal static class InterfaceTestEntry {
  private static TabControl FindTabs(Control root) {
   if(root is TabControl) return (TabControl)root;
   foreach(Control child in root.Controls) { TabControl found=FindTabs(child);if(found!=null) return found; }
   return null;
  }
  [STAThread] public static int Main(string[] args) { try{return Run(args);}catch(Exception ex){Console.Error.WriteLine(ex);return 1;} }
  private static int Run(string[] args) {
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   WorkspacePreferences.PathOverride=Path.Combine(args[0],"preferences-test.txt");
   using(MainForm form=new MainForm()) {
    form.Size=new Size(int.Parse(args[1]),int.Parse(args[2]));
    GazelyTheme.Dark=!bool.Parse(args[3]);GazelyTheme.Apply(form);
    form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;
    form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
    IntPtr handle=form.Handle;
    TabControl tabs=FindTabs(form);
    if(tabs==null || tabs.TabCount!=7) throw new Exception("Missing application pages");
    Size requestedSize=form.Size;
    foreach(Size size in new Size[]{new Size(1920,1080),new Size(1280,980),new Size(980,700),requestedSize})
    {
     form.Size=size;tabs.SelectedIndex=1;form.PerformLayout();Application.DoEvents();
     if(tabs.Region!=null)throw new Exception("Tab clipping region must not survive resizing");
     Control panes=form.Controls.Find("WorkspacePanes",true)[0];
     Control transcript=form.Controls.Find("LiveTranscriptCard",true)[0];
     Control selectedPane=form.Controls.Find("SelectedPassage",true)[0];
     Control candidatesPane=form.Controls.Find("CandidateSection",true)[0];
     foreach(Control pane in new Control[]{transcript,selectedPane,candidatesPane})
       if(pane.Right>panes.ClientSize.Width || pane.Left<0)throw new Exception("Workspace pane clips horizontally at "+size+" pane="+pane.Name+" bounds="+pane.Bounds+" area="+panes.ClientSize);
     if(size.Width==1920 && (transcript.Bounds.IntersectsWith(candidatesPane.Bounds) || candidatesPane.Bounds.IntersectsWith(selectedPane.Bounds) || transcript.Bounds.IntersectsWith(selectedPane.Bounds)))throw new Exception("Wide workspace panes overlap");
     Control toolbar=form.Controls.Find("ListeningToolbar",true)[0];
     for(int a=0;a<toolbar.Controls.Count;a++)for(int b=a+1;b<toolbar.Controls.Count;b++)
      if(toolbar.Controls[a].Bounds.IntersectsWith(toolbar.Controls[b].Bounds))throw new Exception("Listening toolbar overlaps after resize");
     Button editorToggle=(Button)form.Controls.Find("EditReference",true)[0];
     Control editor=form.Controls.Find("ReferenceEditor",true)[0];
     if(editor.Visible)throw new Exception("Reference editor must start collapsed");
     editorToggle.PerformClick();Application.DoEvents();
     if(!editor.Visible)throw new Exception("Reference editor cannot be opened");
     foreach(Control field in editor.Controls)if(field.Right>editor.ClientSize.Width)throw new Exception("Reference editor clipped after resize to "+size);
     editorToggle.PerformClick();Application.DoEvents();
     if(size.Width==1920){using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"expanded-window.png"),System.Drawing.Imaging.ImageFormat.Png);}}
    }
    Console.WriteLine("PASS: expansion to 1920x1080 and restore do not clip transcript or overlap audio controls.");
    form.Size=new Size(1920,1080);Application.DoEvents();
    for(int i=0;i<tabs.TabCount;i++){
     tabs.SelectedIndex=i;Application.DoEvents();
     Control[] frames=tabs.TabPages[i].Controls.Find("CenteredPageContent",true);
     if(frames.Length!=1)throw new Exception("Missing shared page frame: "+i);
     Control frame=frames[0];
     if(frame.Width!=(i==1?frame.Parent.ClientSize.Width:1440) || Math.Abs(frame.Left-(frame.Parent.ClientSize.Width-frame.Right))>1)throw new Exception("Page is not centered: "+i);
     using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"wide-page-"+i+".png"),System.Drawing.Imaging.ImageFormat.Png);}
    }
    form.Size=requestedSize;Application.DoEvents();
    Console.WriteLine("PASS: live workspace fills the window; secondary pages remain centered; editor opens and closes.");
    for(int i=0;i<tabs.TabCount;i++) {
     tabs.SelectedIndex=i; form.PerformLayout();tabs.PerformLayout();Application.DoEvents();
     if(!tabs.SelectedTab.Visible || tabs.SelectedTab.Controls.Count==0) throw new Exception("Page not rendered");
     using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) {
      form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
      bitmap.Save(Path.Combine(args[0],"page-"+i+".png"),System.Drawing.Imaging.ImageFormat.Png);
     }
    }
    // Exercise the actual UI final-result callback with the reported split reference.
    System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    Control[] navigation=form.Controls.Find("GazelyNavigation",true);
    if(navigation.Length!=1 || navigation[0].Controls.Count!=9 || navigation[0].Controls.Find("OpenMiniOverlay",true).Length!=1)throw new Exception("Navigation controls missing");
    foreach(Control button in navigation[0].Controls)
    {
     if(button.Right>navigation[0].ClientSize.Width || button.Bottom>navigation[0].ClientSize.Height)throw new Exception("Navigation clipped: "+button.Text);
     if(button.Width<TextRenderer.MeasureText(button.Text,button.Font).Width+20)throw new Exception("Navigation label clipped: "+button.Text);
    }
    Console.WriteLine("PASS: all navigation controls fit the window without clipped labels.");
    int[] pageOrder={1,0,4,3,2,6,5};
    for(int i=0;i<pageOrder.Length;i++){((Button)navigation[0].Controls[i]).PerformClick();Application.DoEvents();if(tabs.SelectedIndex!=pageOrder[i])throw new Exception("Navigation opens wrong page");}
    tabs.SelectedIndex=1;Application.DoEvents();
    Button workspaceOptions=(Button)form.Controls.Find("WorkspaceOptions",true)[0];
    ToolStripItem reset=workspaceOptions.ContextMenuStrip.Items[1];
    if(!workspaceOptions.Visible || reset.Name!="WorkspaceResetVersion")throw new Exception("Workspace reset menu missing");
    EasyWorshipAutomation automation=(EasyWorshipAutomation)typeof(MainForm).GetField("automation",flags).GetValue(form);
    BibleVersionPosition position=(BibleVersionPosition)typeof(EasyWorshipAutomation).GetField("versionPosition",flags).GetValue(automation);
    position.Configure(9,"test map");position.Bind("test process");
    reset.PerformClick();
    if(automation.GetVersionStateSummary()!="unknown")throw new Exception("Workspace reset did not invalidate version tracking");
    Console.WriteLine("PASS: workspace version reset works while Present page is hidden; no adapter commands sent.");
    tabs.SelectedIndex=4;Application.DoEvents();
    ComboBox engine=(ComboBox)typeof(MainForm).GetField("speechEngineCombo",flags).GetValue(form);
    ComboBox model=(ComboBox)typeof(MainForm).GetField("whisperModelCombo",flags).GetValue(form);
    ComboBox recognizer=(ComboBox)typeof(MainForm).GetField("recognizerCombo",flags).GetValue(form);
    engine.SelectedIndex=1;Application.DoEvents();if(!recognizer.Visible || model.Visible)throw new Exception("Windows recognizer selector broke");
    if(engine.Items.Count!=5 || !engine.Items[4].ToString().StartsWith("Nemotron"))throw new Exception("Nemotron selector missing");
    engine.SelectedIndex=4;Application.DoEvents();
    if(model.Visible || recognizer.Visible || ((CheckBox)typeof(MainForm).GetField("autoLiveCheck",flags).GetValue(form)).Enabled)throw new Exception("Experimental streaming settings exposed automatic presentation or Whisper controls");
    engine.SelectedIndex=0;Application.DoEvents();if(!model.Visible || recognizer.Visible)throw new Exception("Local model selector broke");
    TextBox manualInput=(TextBox)typeof(MainForm).GetField("scriptureTextBox",flags).GetValue(form);
    if(manualInput.Width>350 || engine.Width>270)throw new Exception("Compact input sizing regressed");
    Console.WriteLine("PASS: navigation actions, both speech engine selectors and compact form sizing.");
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,true);
    System.Reflection.MethodInfo final=typeof(MainForm).GetMethod("OnFinalTranscript",flags);
    final.Invoke(form,new object[]{"because it is a very, very important subject. In Genesis chapter",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    ScriptureDetector detector=(ScriptureDetector)typeof(MainForm).GetField("scriptureDetector",flags).GetValue(form);
    typeof(ScriptureDetector).GetField("pendingUtc",flags).SetValue(detector,DateTime.UtcNow.AddSeconds(-7));
    final.Invoke(form,new object[]{"Chapter 49 verse 3 and verse 4 Jacob was pronounced in Blessing",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    Label reference=(Label)typeof(MainForm).GetField("detectedReferenceLabel",flags).GetValue(form);
    if(reference.Text!="Genesis 49:3") throw new Exception("Spoken range did not start at first verse: "+reference.Text);
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,false);
    tabs.SelectedIndex=1;Application.DoEvents();
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) {
     form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
     bitmap.Save(Path.Combine(args[0],"split-reference-regression.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
    Console.WriteLine("PASS: reported seven-second split reference reaches the actual scripture panel.");
    typeof(MainForm).GetField("historyEventKey",flags).SetValue(form,string.Empty);
    ScriptureDetection quote=new ScriptureDetection { Book="Matthew",Chapter=6,Verse=33,Confidence=.75,MatchScore=.91,RequiresReview=true,EvidenceKind="quotation",
     Reason="Possible quotation - matched local KJV wording; review before presenting",
     SourceText="Seek ye first the kingdom and all these other things shall be added unto you",
     MatchedText="But seek ye first the kingdom of God, and his righteousness; and all these things shall be added unto you." };
    typeof(MainForm).GetMethod("ShowDetection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,new object[]{quote});
    tabs.SelectedIndex=1;Application.DoEvents();
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) {
     form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
     bitmap.Save(Path.Combine(args[0],"quotation-fixture.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
    ListView history=(ListView)typeof(MainForm).GetField("suggestionHistory",flags).GetValue(form);
    bool hasGenesis=false;foreach(ListViewItem item in history.Items)if(item.SubItems[1].Text=="Genesis 49:3")hasGenesis=true;
    if(!hasGenesis || history.Items[0].SubItems[2].Text!="91%") throw new Exception("History lost an earlier suggestion or misreported the quote score");
    int earlierRows=history.Items.Count;
    System.Reflection.MethodInfo record=typeof(MainForm).GetMethod("RecordSuggestion",flags);
    ScriptureDetection provisional=new ScriptureDetection{Book="Titus",Chapter=2,Verse=11,SourceText="Titus two eleven",Confidence=.98};
    record.Invoke(form,new object[]{provisional,true,"fixture:one"});
    provisional.Verse=12;record.Invoke(form,new object[]{provisional,true,"fixture:one"});
    record.Invoke(form,new object[]{provisional,false,"fixture:one"});provisional.Verse=99;
    if(history.Items.Count!=earlierRows+1 || ((ScriptureDetection)history.Items[0].Tag).Verse!=12 || history.Items[0].SubItems[4].Text!="Final suggestion") throw new Exception("History revision or snapshot isolation failed");
    tabs.SelectedIndex=6;history.Items[1].Selected=true;Application.DoEvents();
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) {
     form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
     bitmap.Save(Path.Combine(args[0],"history-fixture.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
    Console.WriteLine("PASS: history retains final suggestions, updates provisional rows, uses quotation match scores and snapshots evidence.");
    FlowLayoutPanel recent=(FlowLayoutPanel)typeof(MainForm).GetField("recentHistory",flags).GetValue(form);
    if(recent==null || !tabs.TabPages[1].Contains(recent.Parent))throw new Exception("Recent history is missing from Live workspace");
    System.Reflection.MethodInfo selectHistory=typeof(MainForm).GetMethod("SelectHistoryReference",flags);
    System.Reflection.MethodInfo canPresent=typeof(MainForm).GetMethod("HistoryCanPresent",flags);
    ListViewItem historyItem=history.Items[0];int originalCount=history.Items.Count;
    selectHistory.Invoke(form,new object[]{historyItem});
    ScriptureDetection recalled=(ScriptureDetection)typeof(MainForm).GetField("lastDetection",flags).GetValue(form);
    if(recalled==null || recalled.ReferenceText!="Titus 2:12" || !recalled.RequiresReview || history.Items.Count!=originalCount)throw new Exception("History selection changed evidence, duplicated history or lost review gating");
    if(!((bool)canPresent.Invoke(form,new object[]{historyItem})))throw new Exception("Valid final history item cannot be presented");
    historyItem.SubItems[4].Text="Provisional";
    if((bool)canPresent.Invoke(form,new object[]{historyItem}))throw new Exception("Provisional history is eligible for presentation");
    historyItem.SubItems[4].Text="Not confirmed";
    if((bool)canPresent.Invoke(form,new object[]{historyItem}))throw new Exception("Unconfirmed history is eligible for presentation");
    historyItem.SubItems[4].Text="Final suggestion";
    if(((ScriptureDetection)historyItem.Tag).RequiresReview)throw new Exception("History selection mutated the saved snapshot");
    Console.WriteLine("PASS: workspace history selection is review-only, snapshot-safe, and excludes provisional/unconfirmed actions.");
    typeof(MainForm).GetMethod("ClearDetectionUi",flags).Invoke(form,null);
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,true);
    final.Invoke(form,new object[]{"Genesis",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    ComboBox book=(ComboBox)typeof(MainForm).GetField("draftBook",flags).GetValue(form);
    if(book.DropDownStyle!=ComboBoxStyle.DropDown || book.AutoCompleteMode!=AutoCompleteMode.SuggestAppend || book.Items.Count!=66 || !book.Items.Contains("Titus") || !book.Items.Contains("1 Timothy"))throw new Exception("Searchable book dropdown is incomplete");
    TextBox chapter=(TextBox)typeof(MainForm).GetField("draftChapter",flags).GetValue(form);
    TextBox verse=(TextBox)typeof(MainForm).GetField("draftVerse",flags).GetValue(form);
    Button prepare=(Button)typeof(MainForm).GetField("previewDetectedButton",flags).GetValue(form);
    if(book.Text!="Genesis" || reference.Text!="Genesis" || prepare.Enabled)throw new Exception("Bare book did not open an incomplete draft: book="+book.Text+"; reference="+reference.Text+"; prepare="+prepare.Enabled+"; held="+typeof(MainForm).GetField("operatorDraft",flags).GetValue(form));
    chapter.Text="49";verse.Text="3-4";
    final.Invoke(form,new object[]{"John chapter 3 verse 16",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(book.Text!="Genesis" || chapter.Text!="49" || prepare.Enabled)throw new Exception("Speech overwrote an operator edit");
    typeof(MainForm).GetMethod("ApplyDraft",flags).Invoke(form,null);
    ScriptureDetection held=(ScriptureDetection)typeof(MainForm).GetField("lastDetection",flags).GetValue(form);
    if(held==null || held.ReferenceText!="Genesis 49:3-4" || !held.RequiresReview || held.EvidenceKind!="operator" || !prepare.Enabled)throw new Exception("Operator range did not validate");
    verse.Text="999";typeof(MainForm).GetMethod("ApplyDraft",flags).Invoke(form,null);
    if(prepare.Enabled)throw new Exception("Invalid verse enabled presentation");
    verse.Text="4";typeof(MainForm).GetMethod("ApplyDraft",flags).Invoke(form,null);
    tabs.SelectedIndex=1;Application.DoEvents();
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"operator-draft.png"),System.Drawing.Imaging.ImageFormat.Png);}
    book.SelectedItem="Titus";chapter.Text="2";verse.Text="11";typeof(MainForm).GetMethod("ApplyDraft",flags).Invoke(form,null);
    held=(ScriptureDetection)typeof(MainForm).GetField("lastDetection",flags).GetValue(form);
    if(held==null || held.ReferenceText!="Titus 2:11" || !held.RequiresReview)throw new Exception("Dropdown selection lost manual validation/review");
    book.Text="Not a Bible book";typeof(MainForm).GetMethod("ApplyDraft",flags).Invoke(form,null);
    if(prepare.Enabled)throw new Exception("Free text bypassed Bible book validation");
    Console.WriteLine("PASS: all 66 books available; editable autocomplete, selection and invalid-book rejection preserve operator safeguards.");
    typeof(MainForm).GetMethod("ClearDetectionUi",flags).Invoke(form,null);
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,false);
    Console.WriteLine("PASS: bare-book draft, operator priority, range validation and invalid-verse rejection.");
    if(bool.Parse(args[4])) {
     var wait=System.Diagnostics.Stopwatch.StartNew();
     while(typeof(MainForm).GetField("chapterRecovery",flags).GetValue(form)==null && wait.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
     if(typeof(MainForm).GetField("chapterRecovery",flags).GetValue(form)==null)throw new Exception("Pronunciation recovery unavailable");
     typeof(MainForm).GetField("audioListening",flags).SetValue(form,true);
     final.Invoke(form,new object[]{"In the Genesis chapter for tonight, verse 8, 3, and verse 4.",1.0,"Local Whisper",0.0,-1,null});
     wait.Restart();
     while(reference.Text!="Possible Genesis 49" && wait.ElapsedMilliseconds<5000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
     if(reference.Text!="Possible Genesis 49")throw new Exception("Pronunciation candidate failed to reach scripture window: "+reference.Text);
     Button present=(Button)typeof(MainForm).GetField("liveDetectedButton",flags).GetValue(form);
     if(present.Enabled)throw new Exception("Unresolved verses enabled presentation");
     tabs.SelectedIndex=1;Application.DoEvents();
     using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) {
      form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
      bitmap.Save(Path.Combine(args[0],"pronunciation-recovery.png"),System.Drawing.Imaging.ImageFormat.Png);
     }
     typeof(MainForm).GetField("audioListening",flags).SetValue(form,false);
     Console.WriteLine("PASS: reported phrase renders Possible Genesis 49 with unresolved verses and presentation disabled.");
    }
    tabs.SelectedIndex=1;Application.DoEvents();
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,true);
    int session=(int)typeof(MainForm).GetField("listeningSession",flags).GetValue(form);long revision=(long)typeof(MainForm).GetField("interpretationRevision",flags).GetValue(form);
    string heldReference=reference.Text;
    string burst="Unstable as water thou shalt not excel. Thy word is a lamp unto my feet and a light unto my path. Trust in the Lord with all thine heart and lean not unto thine own understanding.";
    typeof(MainForm).GetMethod("FindQuotation",flags).Invoke(form,new object[]{burst,burst,session,revision-1});
    var quoteWait=System.Diagnostics.Stopwatch.StartNew();bool allQuotes=false;
    while(!allQuotes && quoteWait.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(15);bool a=false,b=false,c=false;foreach(ListViewItem r in history.Items){a|=r.SubItems[1].Text=="Genesis 49:4";b|=r.SubItems[1].Text=="Psalms 119:105";c|=r.SubItems[1].Text=="Proverbs 3:5";}allQuotes=a&&b&&c;}
    if(!allQuotes || reference.Text!=heldReference)throw new Exception("Older quotation results were dropped or stole the held main reference");
    int beforeClear=history.Items.Count;
    typeof(MainForm).GetMethod("ClearCandidatePanel",flags).Invoke(form,null);
    if(history.Items.Count!=beforeClear)throw new Exception("Clear detections destroyed History");
    int clearedEpoch=(int)typeof(MainForm).GetField("candidateEpoch",flags).GetValue(form);
    typeof(MainForm).GetMethod("PublishQuotations",flags).Invoke(form,new object[]{new System.Collections.Generic.List<ScriptureDetection>{new ScriptureDetection{Book="John",Chapter=3,Verse=16}},session,revision,clearedEpoch-1});Application.DoEvents();
    foreach(ListViewItem r in history.Items)if(r.SubItems[4].Text!="Dismissed")throw new Exception("Pre-clear result refilled detections");
    string tail="and all these other things shall be added onto you.";
    string joined="Unless you choose. and single out the men of the inner, as Jesus said, seeking fresh the kingdom and all the "+tail;
    typeof(MainForm).GetMethod("FindQuotation",flags).Invoke(form,new object[]{tail,joined,session,revision});
    quoteWait.Restart();bool matthew=false;
    while(!matthew && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);foreach(ListViewItem r in history.Items)if(r.SubItems[1].Text=="Matthew 6:33" && r.SubItems[4].Text!="Dismissed")matthew=true;}
    if(!matthew)throw new Exception("Reported Matthew quotation failed to reach detections");
    Console.WriteLine("PASS: confirmed-clear operation preserves History, rejects stale results, and the reported Matthew quotation reaches new detections.");
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,false);
    Console.WriteLine("PASS: asynchronous multi-quote results survive newer interpretation and retain held main reference.");
    CandidateTestAdapter fake=new CandidateTestAdapter();PresentationCoordinator coordinator=new PresentationCoordinator(fake);coordinator.SetEnabled(true);
    typeof(MainForm).GetField("destination",flags).SetValue(form,coordinator);
    for(int i=1;i<=25;i++)record.Invoke(form,new object[]{new ScriptureDetection{Book="Psalms",Chapter=119,Verse=i,RequiresReview=true,EvidenceKind="quotation",SourceText="Candidate burst fixture "+i,MatchedText="Test evidence",Confidence=.75},false,"burst:"+i});
    typeof(MainForm).GetMethod("RenderCandidates",flags).Invoke(form,null);Application.DoEvents();
    if(recent.Controls.Count!=20)throw new Exception("Candidate page must render 20 without discarding the remainder");
    foreach(Control row in recent.Controls)if(row.Right>recent.ClientSize.Width)throw new Exception("Candidate row clips horizontally");
    TableLayoutPanel firstRow=(TableLayoutPanel)recent.Controls[0];
    typeof(MainForm).GetMethod("RenderCandidates",flags).Invoke(form,null);
    if(!object.ReferenceEquals(firstRow,recent.Controls[0]))throw new Exception("Unchanged detection cards were recreated on refresh");
    Button selected=(Button)firstRow.GetControlFromPosition(0,0);
    if(selected.Text!="Psalms 119:25")throw new Exception("Unexpected candidate ordering");
    selected.Focus();Application.DoEvents();
    record.Invoke(form,new object[]{new ScriptureDetection{Book="Psalms",Chapter=119,Verse=26,RequiresReview=true,EvidenceKind="quotation",SourceText="Later arrival",Confidence=.75},false,"later-arrival"});
    if(!object.ReferenceEquals(recent.Controls[0],firstRow) || selected.Text!="Psalms 119:25")throw new Exception("Incoming candidate moved focused action target");
    FlowLayoutPanel actions=(FlowLayoutPanel)firstRow.GetControlFromPosition(1,0);Button direct=(Button)actions.Controls[0];
    direct.PerformClick();var actionWait=System.Diagnostics.Stopwatch.StartNew();
    while((fake.Calls==0 || (bool)typeof(MainForm).GetField("presentationRunning",flags).GetValue(form)) && actionWait.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
    if(fake.Calls!=1 || fake.Last.Scripture.ReferenceText!="Psalms 119:25" || fake.Last.Version!="KJV")throw new Exception("Direct candidate Present failed or used wrong reference/version");
    typeof(MainForm).GetField("candidatePage",flags).SetValue(form,1);typeof(MainForm).GetMethod("RenderCandidates",flags).Invoke(form,null);
    if(recent.Controls.Count==0)throw new Exception("Older candidates inaccessible");
    ((Button)form.Controls.Find("LatestCandidates",true)[0]).PerformClick();Application.DoEvents();
    if((int)typeof(MainForm).GetField("candidatePage",flags).GetValue(form)!=0)throw new Exception("Latest did not return to page one");
    Label pagingLabel=(Label)typeof(MainForm).GetField("candidateCount",flags).GetValue(form);
    if(!pagingLabel.Text.Contains("Newest first"))throw new Exception("Newest page ordering not explained");
    TextBox filter=(TextBox)typeof(MainForm).GetField("candidateFilter",flags).GetValue(form);filter.Text="Candidate burst fixture";Application.DoEvents();
    Label countLabel=(Label)typeof(MainForm).GetField("candidateCount",flags).GetValue(form);
    if(!countLabel.Text.StartsWith("25 candidates"))throw new Exception("Burst candidates were lost");
    filter.Text="";typeof(MainForm).GetField("candidatePage",flags).SetValue(form,0);typeof(MainForm).GetMethod("RenderCandidates",flags).Invoke(form,null);
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"candidate-burst.png"),System.Drawing.Imaging.ImageFormat.Png);}
    ((Button)form.Controls.Find("ExpandCandidates",true)[0]).PerformClick();Application.DoEvents();
    Control expanded=form.Controls.Find("CandidateSection",true)[0];
    if(expanded.Width<expanded.Parent.ClientSize.Width-30 || expanded.Height<expanded.Parent.ClientSize.Height-20 || recent.Height<expanded.Height-200)throw new Exception("Expanded candidates do not use available workspace area");
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"candidate-expanded.png"),System.Drawing.Imaging.ImageFormat.Png);}
    ((Button)form.Controls.Find("ExpandCandidates",true)[0]).PerformClick();Application.DoEvents();
    if(!form.Controls.Find("SelectedPassage",true)[0].Visible || !form.Controls.Find("LiveTranscriptCard",true)[0].Visible)throw new Exception("Returning from expanded detections lost workspace panels");
    TableLayoutPanel menuRow=(TableLayoutPanel)recent.Controls[0];
    Button more=(Button)((FlowLayoutPanel)menuRow.GetControlFromPosition(1,0)).Controls[2];
    more.ContextMenuStrip.Items[0].PerformClick();
    if(more.ContextMenuStrip.Items[0].Text!="Unpin")throw new Exception("Pin menu failed");
    int retained=history.Items.Count;
    more.ContextMenuStrip.Items[1].PerformClick();
    if(history.Items.Count!=retained || menuRow.Enabled)throw new Exception("Dismiss lost history or left row actionable");
    Console.WriteLine("PASS: candidate icon actions, pin/dismiss menus and expanded-view return retain behavior and history.");
    Console.WriteLine("PASS: 25 candidates retained, paged and searched; direct Present routes exact row snapshot and KJV through fake adapter without confirmation.");
    typeof(MainForm).GetMethod("ClearCandidatePanel",flags).Invoke(form,null);
    typeof(MainForm).GetField("operatorDraft",flags).SetValue(form,false);
    typeof(MainForm).GetField("audioListening",flags).SetValue(form,true);
    final.Invoke(form,new object[]{"The transcription of shoot to you, Isaiah 51, I want to don't forget it.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(!reference.Text.StartsWith("Isaiah 51"))throw new Exception("Uncertain Isaiah verse lost chapter draft: "+reference.Text);
    final.Invoke(form,new object[]{"I can to mean ye that follow of power righteousness, ye that seek the Lord.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"Look onto the rock once you are healed. And to the whole of the pit, once you are deep.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    quoteWait.Restart();while(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Isaiah 51:1") && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Isaiah 51:1") || reference.Text!="Isaiah 51:\u2026")throw new Exception("Isaiah quote must reach history without completing active chapter draft: "+reference.Text+" Candidates: "+string.Join(";",history.Items.Cast<ListViewItem>().Select(r=>r.SubItems[1].Text).ToArray()));
    final.Invoke(form,new object[]{"is a look onto your father Abraham and unto Sarah that hold you for I caught",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"For I called him he Loon and blessed him and increased him.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    quoteWait.Restart();while(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Isaiah 51:2") && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Isaiah 51:2") || reference.Text!="Isaiah 51:\u2026")throw new Exception("Following Isaiah quote must remain separate from active draft");
    CheckBox automaticPrepare=(CheckBox)typeof(MainForm).GetField("autoPreviewCheck",flags).GetValue(form);automaticPrepare.Checked=true;
    int calls=fake.Calls;
    foreach(string command in new string[]{"Genesis chapter 49 verses 3 and verse 4","Next verse"}){
      final.Invoke(form,new object[]{command,1.0,"Local Whisper",0.0,-1,null});quoteWait.Restart();
      while((fake.Calls==calls || (bool)typeof(MainForm).GetField("presentationRunning",flags).GetValue(form)) && quoteWait.ElapsedMilliseconds<4000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
      calls++;if(fake.Calls!=calls || fake.Last.Scripture.VerseEnd!=0)throw new Exception("Automatic single-verse range/navigation failed");
    }
    if(fake.Last.Scripture.ReferenceText!="Genesis 49:4")throw new Exception("Automatic next verse did not prepare verse 4");
    automaticPrepare.Checked=false;
    // Version-only changes and navigation must reach the shared output adapter.
    var testVersionMap=(System.Collections.Generic.Dictionary<string,int>)typeof(MainForm).GetField("versionMap",flags).GetValue(form);
    testVersionMap["NIV"]=0;testVersionMap["MSG"]=11;
    foreach(string[] step in new string[][]{new[]{"John 3:16 in NIV","John 3:16","NIV"},new[]{"verse seventeen","John 3:17","NIV"},new[]{"use the Message","John 3:17","MSG"},new[]{"next verse","John 3:18","MSG"},new[]{"Romans 8:1","Romans 8:1","KJV"}}){
      final.Invoke(form,new object[]{step[0],1.0,"Nemotron",0.0,-1,null});Application.DoEvents();
      var versionDetection=(ScriptureDetection)typeof(MainForm).GetField("lastDetection",flags).GetValue(form);
      if(versionDetection.ReferenceText!=step[1] || (versionDetection.TranslationExplicit?versionDetection.Translation:"KJV")!=step[2])throw new Exception("Active translation mismatch: "+step[0]);
      if(step[2]!="KJV" && !((Label)typeof(MainForm).GetField("verseExcerpt",flags).GetValue(form)).Text.StartsWith("KJV preview (output: "+step[2]+")"))throw new Exception("Preview mislabels output translation");
      int beforeVersion=fake.Calls;
      typeof(MainForm).GetMethod("RunDetected",flags).Invoke(form,new object[]{true});quoteWait.Restart();
      while((fake.Calls==beforeVersion || (bool)typeof(MainForm).GetField("presentationRunning",flags).GetValue(form)) && quoteWait.ElapsedMilliseconds<4000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
      if(fake.Calls!=beforeVersion+1 || fake.Last.Version!=step[2] || fake.Last.Scripture.ReferenceText!=step[1])throw new Exception("Adapter received wrong translation/reference");
    }
    Console.WriteLine("PASS: speech translation switches, retained verse versions, new-reference KJV reset and truthful preview reach the shared fake adapter.");
    calls=fake.Calls;
    final.Invoke(form,new object[]{"John chapter three in NIV",1.0,"Nemotron",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"verse sixteen",1.0,"Nemotron",0.0,-1,null});Application.DoEvents();
    var splitVersion=(ScriptureDetection)typeof(MainForm).GetField("lastDetection",flags).GetValue(form);
    if(splitVersion.ReferenceText!="John 3:16" || splitVersion.Translation!="NIV" || !splitVersion.TranslationExplicit)throw new Exception("Draft reseeding lost requested translation");
    typeof(MainForm).GetMethod("ClearCandidatePanel",flags).Invoke(form,null);
    final.Invoke(form,new object[]{"Now if you go to Leviticus, let's jump a bit.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"Let's see what the vertical says. Sixth verse 13.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13")throw new Exception("Split Leviticus ordinal reference did not select immediately");
    // Even a current-revision quote must not displace a spoken instruction.
    typeof(MainForm).GetField("lastSpeechDetectionUtc",flags).SetValue(form,DateTime.UtcNow.AddSeconds(-20));
    revision=(long)typeof(MainForm).GetField("interpretationRevision",flags).GetValue(form);
    int activeEpoch=(int)typeof(MainForm).GetField("candidateEpoch",flags).GetValue(form);
    var competing=new System.Collections.Generic.List<ScriptureDetection>{new ScriptureDetection{Book="Deuteronomy",Chapter=4,Verse=24,RequiresReview=true,EvidenceKind="quotation",SourceText="competing fire quote"}};
    typeof(MainForm).GetMethod("PublishQuotations",flags).Invoke(form,new object[]{competing,session,revision,activeEpoch});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13")throw new Exception("Quote stole selected spoken reference after ten seconds");
    final.Invoke(form,new object[]{"He says go to verse 11 let's start from verse 11",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:11")throw new Exception("Contextual verse directive did not select verse 11");
    revision=(long)typeof(MainForm).GetField("interpretationRevision",flags).GetValue(form);
    competing[0]=new ScriptureDetection{Book="Leviticus",Chapter=6,Verse=13,RequiresReview=true,EvidenceKind="quotation",SourceText="old verse wording"};
    typeof(MainForm).GetMethod("PublishQuotations",flags).Invoke(form,new object[]{competing,session,revision,activeEpoch});Application.DoEvents();
    if(reference.Text!="Leviticus 6:11")throw new Exception("Older verse quote reversed newer contextual instruction");
    final.Invoke(form,new object[]{"without the camp onto a clean place. Okay, verse 12.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:12")throw new Exception("Verse 12 directive embedded after quotation was lost");
    Console.WriteLine("PASS: Leviticus split reference and verse 11/12 commands; competing quotes cannot steal or reverse spoken selection.");
    typeof(MainForm).GetMethod("ClearCandidatePanel",flags).Invoke(form,null);
    final.Invoke(form,new object[]{"Malachi chapter four verse two",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"No Micah",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Micah 4:2" || (string)typeof(MainForm).GetField("quoteScopeBook",flags).GetValue(form)!="Micah")throw new Exception("Book correction failed to update selected reference and quote scope");
    final.Invoke(form,new object[]{"It is a come to pass that the mountain of the house of the Lord shall be established rather",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"in the top of the mountains and it shall be exalted above eels.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    quoteWait.Restart();while(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Micah 4:1") && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(!history.Items.Cast<ListViewItem>().Any(r=>r.SubItems[1].Text=="Micah 4:1") || reference.Text!="Micah 4:2")throw new Exception("Micah quote must reach detections without replacing corrected active verse: "+reference.Text);
    if(fake.Calls!=calls)throw new Exception("Correction/quotation sent an unexpected presentation command");
    Console.WriteLine("PASS: corrected book updates scope; actual Micah replay quotation reaches detections without replacing active scripture.");
    Button release=(Button)form.Controls.Find("ReleaseScripture",true)[0];release.PerformClick();Application.DoEvents();
    var active=(ActiveReferenceState)typeof(MainForm).GetField("activeReference",flags).GetValue(form);
    var partial=typeof(MainForm).GetMethod("OnPartialTranscript",flags);
    long utterance=(long)typeof(MainForm).GetField("lastFinalUtteranceId",flags).GetValue(form)+100;
    var previewChunk=new SpeechChunk{Session=session,UtteranceId=utterance};
    partial.Invoke(form,new object[]{"I like what is said in the book of Leviticus",previewChunk,0.0});Application.DoEvents();
    if(reference.Text!="Leviticus ?" || active.Stage!="Book identified")throw new Exception("Partial book intent was not visible immediately");
    partial.Invoke(form,new object[]{"I like what is said",previewChunk,0.0});Application.DoEvents();
    if(!active.IsActive || reference.Text!="Leviticus ?" || active.Current.EvidenceKind!="retained")throw new Exception("Withdrawn book hypothesis was not retained for review");
    final.Invoke(form,new object[]{"I like what is said in the book of Leviticus",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus" || active.Stage!="Book identified")throw new Exception("Confirmed book not retained");
    typeof(MainForm).GetField("lastSpeechDetectionUtc",flags).SetValue(form,DateTime.UtcNow.AddHours(-1));
    final.Invoke(form,new object[]{"This is important for the life of the church",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus")throw new Exception("Ordinary explanation released book intent");
    revision=(long)typeof(MainForm).GetField("interpretationRevision",flags).GetValue(form);
    activeEpoch=(int)typeof(MainForm).GetField("candidateEpoch",flags).GetValue(form);
    typeof(MainForm).GetMethod("PublishQuotations",flags).Invoke(form,new object[]{competing,session,revision,activeEpoch});Application.DoEvents();
    if(reference.Text!="Leviticus")throw new Exception("Quote displaced book-only SP after elapsed time");
    final.Invoke(form,new object[]{"chapter five",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 5:\u2026" || active.Stage!="Chapter identified")throw new Exception("Chapter did not complete active book");
    final.Invoke(form,new object[]{"verse seven",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 5:7" || active.Stage!="Reference resolved")throw new Exception("Verse did not complete active chapter");
    final.Invoke(form,new object[]{"John chapter three verse sixteen",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="John 3:16")throw new Exception("New explicit reference did not replace active context");
    final.Invoke(form,new object[]{"Elsewhere Scripture says thy word is a lamp unto my feet and a light unto my path",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    quoteWait.Restart();while(reference.Text!="Psalms 119:105" && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(reference.Text!="Psalms 119:105")throw new Exception("Clear transition plus strong literal quotation did not switch active scripture");
    release.PerformClick();Application.DoEvents();if(active.IsActive)throw new Exception("Release did not clear active context");
    final.Invoke(form,new object[]{"If you go to Leviticus, let us jump a bit",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    previewChunk.UtteranceId+=100;
    partial.Invoke(form,new object[]{"Leviticus chapter six verse thirteen",previewChunk,0.0});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13 ?")throw new Exception("Complete partial was not shown");
    partial.Invoke(form,new object[]{"Leviticus",previewChunk,0.0});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13 ?")throw new Exception("Book-only partial erased verse");
    final.Invoke(form,new object[]{"Let's jump a bit, let's see what the medical says.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13 ?" || !active.Current.RequiresReview || active.Current.EvidenceKind!="retained")throw new Exception("Incomplete final erased the retained verse or confirmed it without evidence: "+reference.Text);
    revision=(long)typeof(MainForm).GetField("interpretationRevision",flags).GetValue(form);
    activeEpoch=(int)typeof(MainForm).GetField("candidateEpoch",flags).GetValue(form);
    typeof(MainForm).GetMethod("PublishQuotations",flags).Invoke(form,new object[]{competing,session,revision,activeEpoch});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13 ?")throw new Exception("Quote displaced retained chapter/verse");
    final.Invoke(form,new object[]{"Go to VES 11. Let's start from VES 11.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:11" || !active.Current.RequiresReview)throw new Exception("VES instruction did not use retained chapter safely: "+reference.Text);
    final.Invoke(form,new object[]{"Leviticus",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:11")throw new Exception("Repeated book erased confirmed verse");
    final.Invoke(form,new object[]{"John chapter three verse sixteen",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="John 3:16" || active.Retained!=null)throw new Exception("Explicit new scripture failed to replace retained context");
    if(fake.Calls!=calls)throw new Exception("Unconfirmed recovery triggered automatic presentation");
    release.PerformClick();Application.DoEvents();
    var noContext=new ScriptureDetector();
    if(noContext.Feed("Go to VES 11")!=null)throw new Exception("VES recovery invented missing book/chapter");
    active.Confirm(new ScriptureDetection{Book="Leviticus",EvidenceKind="draft",RequiresReview=true});
    typeof(MainForm).GetMethod("RenderActiveReference",flags).Invoke(form,new object[]{false});
    var remember=typeof(MainForm).GetMethod("RememberQuoteAlternative",flags);
    remember.Invoke(form,new object[]{new ScriptureDetection{Book="Leviticus",Chapter=6,Verse=13,MatchScore=0.98,EvidenceKind="quotation",RequiresReview=true,MatchedText="The fire shall ever be burning upon the altar; it shall never go out."}});
    remember.Invoke(form,new object[]{new ScriptureDetection{Book="Matthew",Chapter=6,Verse=33,MatchScore=0.97,EvidenceKind="quotation",RequiresReview=true,MatchedText="Seek ye first the kingdom of God"}});
    typeof(MainForm).GetMethod("RefreshPassageAlternatives",flags).Invoke(form,null);Application.DoEvents();
    var alternatives=form.Controls.Find("PassageAlternatives",true)[0];
    if(reference.Text!="Leviticus" || !alternatives.Controls.Cast<Control>().Any(c=>c.Text.Contains("Leviticus 6:13")) || !alternatives.Controls.Cast<Control>().Any(c=>c.Text.Contains("Matthew 6:33")))throw new Exception("Strong quote must coexist beneath spoken book: "+reference.Text);
    ((Button)alternatives.Controls.Cast<Control>().First(c=>c.Text.Contains("Leviticus 6:13"))).PerformClick();Application.DoEvents();
    if(reference.Text!="Leviticus 6:13" || active.Current.Verse!=0 || alternatives.Controls.Find("RestoreSpokenAnchor",true).Length!=1)throw new Exception("Quote promotion lost spoken anchor");
    final.Invoke(form,new object[]{"Leviticus chapter six verse eleven",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13" || active.Current.ReferenceText!="Leviticus 6:11")throw new Exception("Spoken anchor did not advance independently of operator quote selection");
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"passage-alternatives.png"),System.Drawing.Imaging.ImageFormat.Png);}
    Size compactSize=form.Size;form.Size=new Size(1920,1080);Application.DoEvents();
    using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(args[0],"passage-alternatives-wide.png"),System.Drawing.Imaging.ImageFormat.Png);}
    form.Size=compactSize;Application.DoEvents();
    ((Button)alternatives.Controls.Find("RestoreSpokenAnchor",true)[0]).PerformClick();Application.DoEvents();
    if(reference.Text!="Leviticus 6:11" || !alternatives.Controls.Cast<Control>().Any(c=>c.Text.Contains("Leviticus 6:13")) || !alternatives.Controls.Cast<Control>().Any(c=>c.Text.Contains("Matthew 6:33")) || fake.Calls!=calls)throw new Exception("Returning to spoken anchor lost alternate quote or presented unexpectedly");
    release.PerformClick();Application.DoEvents();
    active.Confirm(new ScriptureDetection{Book="Leviticus",EvidenceKind="draft",RequiresReview=true});
    Console.WriteLine("PASS: strong quotes coexist below SP; selection swaps with spoken anchor, further speech updates anchor, other quotes survive.");
    var recovery=(ReferenceRefinement)typeof(MainForm).GetField("referenceRefinement",flags).GetValue(form);
    var ring=new RecentSpeechAudio();ring.Append(new byte[32000]);
    recovery.UpdateIntent(active.Current.ReferenceText,session);recovery.Observe(1000,100);
    recovery.Request(ring,0);var recoveryJob=recovery.Take(0);
    var recovered=typeof(MainForm).GetMethod("OnRefinedTranscript",flags);
    recovered.Invoke(form,new object[]{"Leviticus chapter six verse thirteen",recoveryJob,100.0});Application.DoEvents();
    if(reference.Text!="Leviticus 6:13" || !active.Current.RequiresReview || active.Current.EvidenceKind!="audio-refinement" || fake.Calls!=calls)throw new Exception("Audio recovery must populate SP for review without presenting");
    release.PerformClick();Application.DoEvents();
    recovered.Invoke(form,new object[]{"Leviticus chapter six verse thirteen",recoveryJob,100.0});Application.DoEvents();
    if(active.IsActive)throw new Exception("Stale recovery restored a released scripture");
    active.Confirm(new ScriptureDetection{Book="Malachi",Chapter=4,Verse=2,EvidenceKind="explicit",Confidence=0.97});
    recovery.UpdateIntent(active.Current.ReferenceText,session);recovery.Observe(1000,100);recovery.Request(ring,0,-1,true);recoveryJob=recovery.Take(0);
    recovered.Invoke(form,new object[]{"No Micah",recoveryJob,100.0});Application.DoEvents();
    if(reference.Text!="Micah 4:2" || !active.Current.RequiresReview || fake.Calls!=calls)throw new Exception("Audio book correction must inherit numbers for review without presenting");
    release.PerformClick();Application.DoEvents();
    Console.WriteLine("PASS: audio refinement updates SP for review only; release rejects stale completion.");
    Console.WriteLine("PASS: complete partial survives incomplete final, quotes stay separate, VES uses active chapter with review, explicit switch and release work.");
    Console.WriteLine("PASS: provisional book, revision, persistent book/chapter/verse states, explicit switch, quote transition and manual release.");
    Console.WriteLine("PASS: Isaiah readings remain in Detections; range-first and next-verse automation use fake adapter.");
    Console.WriteLine("PASS: rendered seven application pages without microphone or real presentation actions.");
    // The overlay uses the same live selection and fake presentation adapter.
    var miniType=typeof(MainForm).GetNestedType("MiniOverlay",System.Reflection.BindingFlags.NonPublic);
    var mini=(Form)Activator.CreateInstance(miniType,new object[]{form});
    typeof(MainForm).GetField("miniOverlay",flags).SetValue(form,mini);
    miniType.GetField("monitorName",flags).SetValue(mini,Screen.FromControl(form).DeviceName);
    miniType.GetMethod("Open").Invoke(mini,null);Application.DoEvents();
    if(!mini.TopMost || mini.FormBorderStyle!=FormBorderStyle.None || mini.ShowInTaskbar)throw new Exception("Overlay window styles changed");
    var styles=(CreateParams)miniType.GetProperty("CreateParams",flags).GetValue(mini,null);
    if((styles.ExStyle & 0x08000080)!=0x08000080)throw new Exception("Overlay must be non-activating and a tool window");
    typeof(MainForm).GetMethod("ShowDetection",flags).Invoke(form,new object[]{new ScriptureDetection{Book="Genesis",Chapter=49,Verse=3,RequiresReview=true,Confidence=.92}});
    foreach(string state in new[]{"Collapsed","Compact","Expanded"}){
      miniType.GetMethod("SetMode",flags).Invoke(mini,new object[]{state});Application.DoEvents();
      using(var bitmap=new Bitmap(mini.Width,mini.Height)){mini.DrawToBitmap(bitmap,new Rectangle(Point.Empty,mini.Size));bitmap.Save(Path.Combine(args[0],"overlay-"+state+".png"));}
      if(!Screen.FromControl(form).WorkingArea.Contains(mini.Bounds))throw new Exception("Overlay escaped operator monitor");
    }
    // Reproduce menu close followed by item activation, then reopen repeatedly.
    // A Closed handler must not dispose a menu still in WinForms click dispatch.
    for(int cycle=0;cycle<3;cycle++){
      Console.WriteLine("Overlay return cycle "+cycle+": restore workspace");
      miniType.GetMethod("ReturnToWorkspace",flags).Invoke(mini,null);
      form.WindowState=FormWindowState.Maximized;
      Console.WriteLine("Overlay return cycle "+cycle+": open mini");
      typeof(MainForm).GetMethod("OpenMiniOverlay",flags).Invoke(form,null);Application.DoEvents();
      if(form.Visible || !mini.Visible)throw new Exception("Mini mode did not replace workspace");
      Console.WriteLine("Overlay return cycle "+cycle+": show menu");
      miniType.GetMethod("ShowOptions",flags).Invoke(mini,new object[]{mini});Application.DoEvents();
      var menu=(ContextMenuStrip)miniType.GetField("optionsMenu",flags).GetValue(mini);
      var returnItem=menu.Items[menu.Items.Count-1];
      menu.Close(ToolStripDropDownCloseReason.ItemClicked);
      Console.WriteLine("Overlay return cycle "+cycle+": activate return item");
      if(menu.IsDisposed || returnItem.IsDisposed)throw new Exception("Overlay menu disposed during click dispatch");
      returnItem.PerformClick();Application.DoEvents();
      if(!form.Visible || mini.Visible || form.WindowState!=FormWindowState.Maximized)throw new Exception("Menu did not restore full maximized workspace");
    }
    typeof(MainForm).GetMethod("OpenMiniOverlay",flags).Invoke(form,null);Application.DoEvents();
    var overlayBody=(TableLayoutPanel)miniType.GetField("body",flags).GetValue(mini);
    var heading=overlayBody.GetControlFromPosition(0,0);
    ((Button)heading.Controls[heading.Controls.Count-1]).PerformClick();Application.DoEvents();
    if(!form.Visible || mini.Visible)throw new Exception("Overlay return arrow did not restore workspace");
    miniType.GetMethod("Open").Invoke(mini,null);Application.DoEvents();
    Console.WriteLine("PASS: repeated menu close/click and return arrow restore maximized workspace without disposed menus.");
    int beforeMini=fake.Calls;
    ((Button)miniType.GetField("live",flags).GetValue(mini)).PerformClick();
    var miniWait=System.Diagnostics.Stopwatch.StartNew();while(fake.Calls==beforeMini && miniWait.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(fake.Calls!=beforeMini+1 || fake.Last.Scripture.ReferenceText!="Genesis 49:3")throw new Exception("Overlay did not use existing Present action");
    mini.Location=new Point(-30000,-30000);Application.DoEvents();
    if(!Screen.FromControl(form).WorkingArea.Contains(mini.Bounds))throw new Exception("Overlay drag bounds escaped operator monitor");
    miniType.GetMethod("Save",flags).Invoke(mini,null);typeof(MainForm).GetMethod("SaveWorkspacePreferences",flags).Invoke(form,null);
    if(WorkspacePreferences.Load()["overlay.mode"]!="Expanded")throw new Exception("Host settings erased overlay settings");
    typeof(MainForm).GetField("presentationRunning",flags).SetValue(form,true);miniType.GetMethod("FollowTarget",flags).Invoke(mini,null);
    if(mini.Visible)throw new Exception("Overlay could intercept adapter automation clicks");
    typeof(MainForm).GetField("presentationRunning",flags).SetValue(form,false);miniType.GetMethod("FollowTarget",flags).Invoke(mini,null);
    miniType.GetField("targetProcess",flags).SetValue(mini,System.Diagnostics.Process.GetCurrentProcess().ProcessName);
    miniType.GetField("targetTitle",flags).SetValue(mini,form.Text);miniType.GetField("corner",flags).SetValue(mini,"Top right");
    miniType.GetField("targetHandle",flags).SetValue(mini,form.Handle);
    form.WindowState=FormWindowState.Minimized;Application.DoEvents();miniType.GetMethod("FollowTarget",flags).Invoke(mini,null);
    if(mini.Visible)throw new Exception("Overlay did not hide with minimized anchor");
    form.WindowState=FormWindowState.Normal;form.Show();Application.DoEvents();miniType.GetMethod("FollowTarget",flags).Invoke(mini,null);
    if(!mini.Visible)throw new Exception("Overlay did not return with restored anchor");
    miniType.GetField("monitorName",flags).SetValue(mini,"DISCONNECTED-TEST");miniType.GetMethod("FollowTarget",flags).Invoke(mini,null);
    if(mini.Visible)throw new Exception("Overlay appeared on fallback monitor after disconnect");
    miniType.GetMethod("Shutdown").Invoke(mini,null);
    var restored=(Form)Activator.CreateInstance(miniType,new object[]{form});
    if((string)miniType.GetField("mode",flags).GetValue(restored)!="Expanded" || restored.Size!=mini.Size)throw new Exception("Overlay size/state not restored");
    miniType.GetMethod("Shutdown").Invoke(restored,null);
    Console.WriteLine("PASS: overlay states, monitor bounds/disconnect, non-activation flags, minimize/restore, persistence and shared Present action.");
    form.Close();
   }
   WorkspacePreferences.Save(new System.Collections.Generic.Dictionary<string,string>{{"dark","True"},{"autoLive","True"},{"autoPrepare","False"},{"audioRefinement","True"},{"model","Balanced"},{"engine","Local AI (recommended)"}});
   using(MainForm reopened=new MainForm()){
    if(!((CheckBox)typeof(MainForm).GetField("refinementCheck",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(reopened)).Checked)throw new Exception("Audio refinement preference did not restore");
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    if(!GazelyTheme.Dark || !((CheckBox)typeof(MainForm).GetField("autoLiveCheck",flags).GetValue(reopened)).Checked || (bool)typeof(MainForm).GetField("audioListening",flags).GetValue(reopened))throw new Exception("Saved theme/automation not restored or listening started unexpectedly");
    if(((ComboBox)typeof(MainForm).GetField("whisperModelCombo",flags).GetValue(reopened)).Text!="Balanced")throw new Exception("Saved speech profile not restored");
    ((CheckBox)typeof(MainForm).GetField("autoLiveCheck",flags).GetValue(reopened)).Checked=false;
    if(WorkspacePreferences.Load()["autoLive"]!="False")throw new Exception("Preference changes were not saved");
    reopened.Close();
   }
   Console.WriteLine("PASS: preferences restore across form instances without starting audio; mode changes save; unchanged cards retain identity.");
   return 0;
  }
 }
}
'@
$refs=@()
foreach($name in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Speech','WindowsBase','UIAutomationClient','UIAutomationTypes')) {
 Add-Type -AssemblyName $name
 $refs+=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq $name}|Select-Object -First 1).Location
}
$exe=Join-Path $PSScriptRoot ('InterfaceTest-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
 Add-Type -TypeDefinition $source -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
 & $exe $output $Width $Height ([bool]$Light).ToString() ([bool]$Pronunciation).ToString()
 if($LASTEXITCODE -ne 0){throw "Interface test failed: $LASTEXITCODE"}
} finally { if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe} }

