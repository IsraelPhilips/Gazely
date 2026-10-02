param([Parameter(Mandatory=$true)][string]$OutputDirectory,[int]$Width=1020,[int]$Height=780,[switch]$Light,[switch]$Pronunciation)
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new output directory to preserve screenshots.'}
New-Item -ItemType Directory -Path $output | Out-Null
$source=Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$source='using System.Speech.Synthesis;'+$source
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
    if(navigation.Length!=1 || navigation[0].Controls.Count!=8)throw new Exception("Navigation controls missing");
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
    quoteWait.Restart();while(reference.Text!="Isaiah 51:1" && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(reference.Text!="Isaiah 51:1")throw new Exception("Reported Isaiah reading did not reach Selected passage: "+reference.Text);
    final.Invoke(form,new object[]{"is a look onto your father Abraham and unto Sarah that hold you for I caught",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    final.Invoke(form,new object[]{"For I called him he Loon and blessed him and increased him.",1.0,"Local Whisper",0.0,-1,null});Application.DoEvents();
    quoteWait.Restart();while(reference.Text!="Isaiah 51:2" && quoteWait.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
    if(reference.Text!="Isaiah 51:2")throw new Exception("Following Isaiah quotation did not update Selected passage: "+reference.Text);
    CheckBox automaticPrepare=(CheckBox)typeof(MainForm).GetField("autoPreviewCheck",flags).GetValue(form);automaticPrepare.Checked=true;
    int calls=fake.Calls;
    foreach(string command in new string[]{"Genesis chapter 49 verses 3 and verse 4","Next verse"}){
      final.Invoke(form,new object[]{command,1.0,"Local Whisper",0.0,-1,null});quoteWait.Restart();
      while((fake.Calls==calls || (bool)typeof(MainForm).GetField("presentationRunning",flags).GetValue(form)) && quoteWait.ElapsedMilliseconds<4000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
      calls++;if(fake.Calls!=calls || fake.Last.Scripture.VerseEnd!=0)throw new Exception("Automatic single-verse range/navigation failed");
    }
    if(fake.Last.Scripture.ReferenceText!="Genesis 49:4")throw new Exception("Automatic next verse did not prepare verse 4");
    automaticPrepare.Checked=false;
    Console.WriteLine("PASS: reported Isaiah readings update Selected passage; range-first and next-verse automation use fake adapter.");
    Console.WriteLine("PASS: rendered seven application pages without microphone or real presentation actions.");
    form.Close();
   }
   WorkspacePreferences.Save(new System.Collections.Generic.Dictionary<string,string>{{"dark","True"},{"autoLive","True"},{"autoPrepare","False"},{"model","Balanced"},{"engine","Local AI (recommended)"}});
   using(MainForm reopened=new MainForm()){
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

