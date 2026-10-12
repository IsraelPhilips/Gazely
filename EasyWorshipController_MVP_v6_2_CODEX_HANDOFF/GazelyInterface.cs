using System;
using System.Drawing;
using System.Windows.Forms;

namespace EasyWorshipControllerMvp
{
    internal sealed class BufferedDetectionPanel : FlowLayoutPanel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
        private bool redrawPaused;
        public BufferedDetectionPanel(){DoubleBuffered=true;ResizeRedraw=true;}
        public void BeginUpdate(){if(IsHandleCreated && Visible){SendMessage(Handle,0x000B,IntPtr.Zero,IntPtr.Zero);redrawPaused=true;}}
        public void EndUpdate(){if(redrawPaused){redrawPaused=false;if(IsHandleCreated){SendMessage(Handle,0x000B,new IntPtr(1),IntPtr.Zero);Invalidate(true);}}}
    }
    internal sealed class WorkspacePreferences
    {
        public static string PathOverride { get; set; }
        private static string FilePath {get{return PathOverride??System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"gazely-preferences.txt");}}
        public static System.Collections.Generic.Dictionary<string,string> Load()
        {
            var data=new System.Collections.Generic.Dictionary<string,string>();
            if(!System.IO.File.Exists(FilePath))return data;
            foreach(string line in System.IO.File.ReadAllLines(FilePath)){int p=line.IndexOf('=');if(p>0){try{data[line.Substring(0,p)]=Uri.UnescapeDataString(line.Substring(p+1));}catch(ArgumentException){}}}
            return data;
        }
        public static void Save(System.Collections.Generic.Dictionary<string,string> data)
        {
            var lines=new System.Collections.Generic.List<string>();foreach(var pair in data)lines.Add(pair.Key+"="+Uri.EscapeDataString(pair.Value??""));
            string temp=FilePath+".tmp";System.IO.File.WriteAllLines(temp,lines.ToArray());
            if(System.IO.File.Exists(FilePath))System.IO.File.Replace(temp,FilePath,null);else System.IO.File.Move(temp,FilePath);
        }
    }
    internal static class RoundedShape
    {
        public static System.Drawing.Drawing2D.GraphicsPath Path(Rectangle r,int radius)
        {
            int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            System.Drawing.Drawing2D.GraphicsPath p=new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
    }
    internal sealed class GazelyButton : Button
    {
        public string IconName;
        public bool IconOnly;
        private bool hovering;
        public GazelyButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        public override Size GetPreferredSize(Size proposedSize){Size text=TextRenderer.MeasureText(Text,Font);return new Size(IconOnly?36:text.Width+28+(IconName==null?0:22),Math.Max(34,text.Height+12));}
        protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovering=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?GazelyTheme.Background:Parent.BackColor);
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var p=RoundedShape.Path(new Rectangle(1,1,Width-3,Height-3),10))
            using(var b=new SolidBrush(Enabled?(hovering && (Tag as string)!="primary"?GazelyTheme.Soft:BackColor):GazelyTheme.Surface))
            using(var pen=new Pen(Focused?GazelyTheme.Accent:(Tag as string)=="quiet"?BackColor:GazelyTheme.Line,Focused?2:1))
            {e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}
            Rectangle caption=ClientRectangle;caption.Inflate(-10,-2);
            Color ink=Enabled?ForeColor:GazelyTheme.Muted;
            if(IconName!=null)
            {
                float x=IconOnly?Width/2f-7:12,y=Height/2f-7;
                using(Pen pen=new Pen(ink,1.6f))using(Brush brush=new SolidBrush(ink))
                {
                    if(IconName=="play")e.Graphics.FillPolygon(brush,new PointF[]{new PointF(x+3,y),new PointF(x+14,y+7),new PointF(x+3,y+14)});
                    else if(IconName=="stop")e.Graphics.FillRectangle(brush,x+2,y+2,11,11);
                    else if(IconName=="collapse")e.Graphics.DrawLine(pen,x+1,y+7,x+13,y+7);
                    else if(IconName=="expand")e.Graphics.DrawRectangle(pen,x+1,y+1,12,12);
                    else if(IconName=="open") {e.Graphics.DrawLine(pen,x+2,y+12,x+12,y+2);e.Graphics.DrawLine(pen,x+5,y+2,x+12,y+2);e.Graphics.DrawLine(pen,x+12,y+2,x+12,y+9);}
                    else if(IconName=="more")for(int i=0;i<3;i++)e.Graphics.FillEllipse(brush,x+i*6,y+6,3,3);
                    else if(IconName=="left" || IconName=="right") {float a=IconName=="left"?10:4,b=14-a;e.Graphics.DrawLines(pen,new PointF[]{new PointF(x+a,y+1),new PointF(x+b,y+7),new PointF(x+a,y+13)});}
                    else if(IconName=="prepare") {e.Graphics.DrawRectangle(pen,x,y+3,14,11);e.Graphics.DrawLine(pen,x+4,y,x+10,y);e.Graphics.DrawLine(pen,x+4,y+7,x+10,y+7);}
                    else if(IconName=="edit") {e.Graphics.DrawLine(pen,x+2,y+12,x+12,y+2);e.Graphics.DrawLine(pen,x+1,y+14,x+6,y+13);e.Graphics.DrawLine(pen,x+9,y+1,x+13,y+5);}
                    else if(IconName=="refresh") {e.Graphics.DrawArc(pen,x+1,y+1,12,12,35,285);e.Graphics.DrawLine(pen,x+13,y+1,x+13,y+6);e.Graphics.DrawLine(pen,x+13,y+6,x+8,y+5);}
                    else {e.Graphics.DrawEllipse(pen,x,y,14,14);e.Graphics.DrawLine(pen,x+7,y+6,x+7,y+11);e.Graphics.FillEllipse(brush,x+6,y+3,2,2);}
                }
                caption.X+=22;caption.Width-=22;
            }
            if(!IconOnly)TextRenderer.DrawText(e.Graphics,Text,Font,caption,ink,(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        }
    }
    internal sealed class GazelyToggle : CheckBox
    {
        public GazelyToggle(){AutoSize=false;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int y=(Height-20)/2;
            using(var p=RoundedShape.Path(new Rectangle(1,y,38,20),10))
            using(var b=new SolidBrush(Checked?GazelyTheme.Accent:GazelyTheme.Muted))e.Graphics.FillPath(b,p);
            using(var b=new SolidBrush(GazelyTheme.Background))e.Graphics.FillEllipse(b,Checked?21:4,y+3,14,14);
            TextRenderer.DrawText(e.Graphics,Text+(Checked?"  On":"  Off"),Font,new Rectangle(48,0,Width-48,Height),Enabled?ForeColor:GazelyTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
        }
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    }
    internal sealed class GazelyCard : TableLayoutPanel
    {
        public bool AccentEdge;
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if((Tag as string)!="surface"){base.OnPaintBackground(e);return;}
            e.Graphics.Clear(Parent==null?GazelyTheme.Background:Parent.BackColor);
            if(Width<4 || Height<4)return;
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var p=RoundedShape.Path(new Rectangle(0,0,Width-1,Height-1),19))
            using(var b=new SolidBrush(GazelyTheme.Surface))e.Graphics.FillPath(b,p);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if((Tag as string)!="surface")return;
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var p=RoundedShape.Path(new Rectangle(1,1,Width-3,Height-3),14))
            using(var pen=new Pen(GazelyTheme.Line))e.Graphics.DrawPath(pen,p);
            if(AccentEdge)using(var pen=new Pen(GazelyTheme.Accent,2))e.Graphics.DrawLine(pen,20,1,Width-20,1);
        }
    }
    internal sealed class GazelyInputFrame : Panel
    {
        public GazelyInputFrame(Control input)
        {
            Height=40;Width=240;Padding=new Padding(12,9,12,7);input.Dock=DockStyle.Fill;input.Margin=Padding.Empty;
            TextBox text=input as TextBox;if(text!=null)text.BorderStyle=BorderStyle.None;
            Controls.Add(input);input.Enter+=delegate{Invalidate();};input.Leave+=delegate{Invalidate();};
            SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?GazelyTheme.Surface:Parent.BackColor);
            if(Width<4 || Height<4)return;
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var p=RoundedShape.Path(new Rectangle(1,1,Width-3,Height-3),10))
            using(var b=new SolidBrush(GazelyTheme.Background))
            using(var pen=new Pen(ContainsFocus?GazelyTheme.Accent:GazelyTheme.Line))
            {e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}
        }
    }
    internal sealed class GazelyFlowCard : FlowLayoutPanel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?GazelyTheme.Background:Parent.BackColor);
            if(Width<4 || Height<4)return;
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var p=RoundedShape.Path(new Rectangle(0,0,Width-1,Height-1),19))
            using(var b=new SolidBrush(GazelyTheme.Surface))e.Graphics.FillPath(b,p);
        }
    }
    internal static class GazelyTheme
    {
        public static bool Dark=false;
        public static Color Background { get { return Dark?Color.FromArgb(17,19,16):Color.FromArgb(243,241,234); } }
        public static Color Surface { get { return Dark?Color.FromArgb(28,31,27):Color.White; } }
        public static Color Text { get { return Dark?Color.FromArgb(243,241,234):Color.FromArgb(17,19,16); } }
        public static Color Muted { get { return Dark?Color.FromArgb(175,183,170):Color.FromArgb(83,92,79); } }
        public static Color Accent { get { return Dark?Color.FromArgb(184,243,74):Color.FromArgb(57,91,10); } }
        public static Color Warning { get { return Dark?Color.FromArgb(244,192,111):Color.FromArgb(126,72,8); } }
        public static Color Error { get { return Dark?Color.FromArgb(255,158,147):Color.FromArgb(166,42,31); } }
        public static Color Soft { get { return Dark?Color.FromArgb(39,45,35):Color.FromArgb(234,236,227); } }
        public static Color Line { get { return Dark?Color.FromArgb(53,62,46):Color.FromArgb(223,227,214); } }
        private static void PaintDisabledButton(object sender,PaintEventArgs args)
        {
            Button button=(Button)sender;
            if(button.Enabled) return;
            using(SolidBrush fill=new SolidBrush(Surface)) args.Graphics.FillRectangle(fill,button.ClientRectangle);
            using(Pen border=new Pen(Muted)) args.Graphics.DrawRectangle(border,0,0,button.Width-1,button.Height-1);
            TextRenderer.DrawText(args.Graphics,button.Text,button.Font,button.ClientRectangle,Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        public static void Apply(Control parent)
        {
            parent.BackColor=(parent.Tag as string)=="surface"?Surface:(parent.Parent==null?Background:parent.Parent.BackColor);
            TabPage page=parent as TabPage;
            if(page!=null) { page.UseVisualStyleBackColor=false;page.BackColor=Background; }
            parent.ForeColor=Text;
            foreach(Control control in parent.Controls) Apply(control);
            TextBox text=parent as TextBox;
            if(text!=null) { text.BackColor=parent.Parent is GazelyInputFrame?Background:Surface;text.BorderStyle=BorderStyle.None; }
            ListView list=parent as ListView;
            if(list!=null) { list.BackColor=Surface;list.ForeColor=Text;list.BorderStyle=BorderStyle.FixedSingle; }
            ComboBox combo=parent as ComboBox;
            if(combo!=null) { combo.BackColor=Background;combo.FlatStyle=FlatStyle.Flat; }
            Button button=parent as Button;
            if(button!=null)
            {
                button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Dark?Color.FromArgb(66,74,60):Color.FromArgb(192,201,183);
                button.FlatAppearance.MouseOverBackColor=Dark?Color.FromArgb(45,53,37):Color.FromArgb(223,232,213);
                button.BackColor=Surface;
                if((button.Tag as string)=="quiet")button.BackColor=button.Parent.BackColor;
                if((button.Tag as string)=="selected")button.BackColor=Soft;
                if((button.Tag as string)=="muted-action")button.ForeColor=Muted;
                if((button.Tag as string)=="primary") { button.BackColor=Color.FromArgb(184,243,74);button.ForeColor=Color.FromArgb(17,19,16); }
                if(string.IsNullOrEmpty(button.AccessibleName))button.AccessibleName=button.Text;
                button.Paint-=PaintDisabledButton;button.Paint+=PaintDisabledButton;
            }
            if(parent is Label && (parent.Tag as string)=="muted") parent.ForeColor=Muted;
            if(parent is Label || parent is CheckBox) parent.BackColor=parent.Parent==null?Background:parent.Parent.BackColor;
            parent.Invalidate();
        }
    }

    internal sealed partial class MainForm
    {
        private MiniOverlay miniOverlay;
        private double overlayRms=-90,overlayPeak=-90;
        private void OpenMiniOverlay()
        {
            if(miniOverlay==null || miniOverlay.IsDisposed)miniOverlay=new MiniOverlay(this);
            if(!miniOverlay.EnsureOperatorMonitor())return;
            miniOverlay.Open();Hide();
        }
        // A view of MainForm state: all listening, interpretation and presentation
        // commands continue through the same methods used by the full workspace.
        private sealed class MiniOverlay : Form
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ReleaseCapture();
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h,out NativeRect r);
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct NativeRect { public int Left,Top,Right,Bottom; }
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct WindowPosition { public IntPtr Window,After;public int X,Y,Width,Height;public uint Flags; }
            private readonly MainForm host;
            private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
            private readonly ToolTip tips=new ToolTip();
            private readonly NotifyIcon tray=new NotifyIcon();
            private readonly ContextMenuStrip optionsMenu=new ContextMenuStrip();
            private FormWindowState workspaceWindowState=FormWindowState.Normal;
            private readonly TableLayoutPanel body;
            private readonly Label status,reference,score,source,output,pageLabel;
            private readonly TextBox transcript;
            private readonly AudioLevelMeter meter=new AudioLevelMeter();
            private readonly Button listen,preview,live;
            private readonly CheckBox autoPreview=new GazelyToggle(),autoLive=new GazelyToggle();
            private readonly BufferedDetectionPanel detections=new BufferedDetectionPanel();
            private string monitorName="",targetProcess="",targetTitle="",corner="Free",mode="Compact",signature="";
            private bool enabled,syncing,placing,trackingMouse,closingHost,lastDark;
            private int page;
            private IntPtr targetHandle;
            private readonly System.Collections.Generic.Dictionary<string,Size> sizes=new System.Collections.Generic.Dictionary<string,Size>();
            protected override bool ShowWithoutActivation {get{return true;}}
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x00000080;return p;}}
            public MiniOverlay(MainForm owner)
            {
                host=owner;Name="GazelyMiniOverlay";Text="Gazely Mini Overlay";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
                StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Segoe UI",10);Padding=new Padding(8);DoubleBuffered=true;
                sizes["Collapsed"]=new Size(420,100);sizes["Compact"]=new Size(440,292);sizes["Expanded"]=new Size(510,760);
                body=Vertical(38,36,62,42,40,0,0,0,0,0,0);Controls.Add(body);
                var heading=Row();heading.WrapContents=false;heading.Dock=DockStyle.Fill;
                var title=CopyLabel("Gazely mini  ·  drag",11,true);title.Width=170;title.AutoSize=false;title.Height=32;
                title.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){corner="Free";ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);Save();}};
                heading.Controls.Add(title);
                AddButton(heading,"−",delegate{SetMode("Collapsed");},"Collapse");
                AddButton(heading,"□",delegate{SetMode(mode=="Expanded"?"Compact":"Expanded");},"Compact / expanded");
                Button menu=AddButton(heading,"⋯",delegate{},"Overlay options");menu.Click+=delegate{ShowOptions(menu);};
                AddButton(heading,"↗",ReturnToWorkspace,"Open full workspace and settings");body.Controls.Add(heading,0,0);
                var signals=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};signals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));signals.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));signals.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,95));
                status=CopyLabel("Stopped",10,false);status.Dock=DockStyle.Fill;signals.Controls.Add(status,0,0);meter.Dock=DockStyle.Fill;signals.Controls.Add(meter,1,0);
                listen=AddButton(signals,"Listen",delegate{if(host.audioListening)host.StopListening();else host.StartListening();},"Start / stop listening");signals.SetCellPosition(listen,new TableLayoutPanelCellPosition(2,0));body.Controls.Add(signals,0,1);
                reference=CopyLabel("Waiting for a reference",18,true);reference.AutoSize=false;reference.Dock=DockStyle.Fill;reference.AutoEllipsis=true;body.Controls.Add(reference,0,2);
                score=CopyLabel("Confidence: —",9,false);score.AutoSize=false;score.Dock=DockStyle.Fill;body.Controls.Add(score,0,3);
                var actions=Row();preview=AddButton(actions,"PREVIEW",delegate{if(host.previewDetectedButton.Enabled)host.RunDetected(false);},"Prepare selected scripture in the presentation app");preview.Width=135;
                live=AddButton(actions,"SHOW LIVE",delegate{if(host.liveDetectedButton.Enabled)host.RunDetected(true);},"Present selected scripture now");live.Width=150;live.Tag="primary";body.Controls.Add(actions,0,4);
                source=CopyLabel("",9,false);source.AutoSize=false;source.AutoEllipsis=true;source.Dock=DockStyle.Fill;body.Controls.Add(source,0,5);
                transcript=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,AccessibleName="Live hearing and recent transcript"};body.Controls.Add(transcript,0,6);
                autoPreview.Text="Auto Preview";autoLive.Text="Auto Live";autoPreview.Size=new Size(170,32);autoLive.Size=new Size(150,32);
                autoPreview.CheckedChanged+=delegate{if(!syncing && host.autoPreviewCheck.Enabled)host.autoPreviewCheck.Checked=autoPreview.Checked;};
                autoLive.CheckedChanged+=delegate{if(!syncing && host.autoLiveCheck.Enabled)host.autoLiveCheck.Checked=autoLive.Checked;};body.Controls.Add(Row(autoPreview,autoLive),0,7);
                var historyBar=Row();pageLabel=CopyLabel("Detections",10,true);pageLabel.Width=180;pageLabel.AutoSize=false;historyBar.Controls.Add(pageLabel);
                AddButton(historyBar,"‹",delegate{page=Math.Max(0,page-1);RefreshDetections(true);},"Newer detections");
                AddButton(historyBar,"›",delegate{page++;RefreshDetections(true);},"Older detections");
                AddButton(historyBar,"↻",delegate{page=0;RefreshDetections(true);},"Latest detections");body.Controls.Add(historyBar,0,8);
                detections.Dock=DockStyle.Fill;detections.AutoScroll=true;detections.FlowDirection=FlowDirection.TopDown;detections.WrapContents=false;body.Controls.Add(detections,0,9);
                detections.MouseEnter+=delegate{trackingMouse=true;};detections.MouseLeave+=delegate{trackingMouse=false;};
                detections.Resize+=delegate{foreach(Control c in detections.Controls)c.Width=Math.Max(280,detections.ClientSize.Width-24);};
                output=CopyLabel("",9,false);output.AutoSize=false;output.AutoEllipsis=true;output.Dock=DockStyle.Fill;body.Controls.Add(output,0,10);
                ResizeEnd+=delegate{Constrain();sizes[mode]=Size;Save();};LocationChanged+=delegate{if(!placing)Constrain();};
                FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!closingHost){e.Cancel=true;ReturnToWorkspace();}};
                tray.Icon=host.Icon??SystemIcons.Application;tray.Text="Gazely · Mini Overlay";
                tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Open workspace / settings",null,delegate{ReturnToWorkspace();});
                tray.ContextMenuStrip.Items.Add("Show floating overlay",null,delegate{corner="Free";if(EnsureOperatorMonitor())Open();});
                tray.DoubleClick+=delegate{ReturnToWorkspace();};
                timer.Interval=250;timer.Tick+=delegate{if(enabled){RefreshState();FollowTarget();}};timer.Start();
                Restore();SetMode(mode);lastDark=GazelyTheme.Dark;GazelyTheme.Apply(this);
            }
            private Button AddButton(Control parent,string text,Action action,string hint)
            {var b=new GazelyButton{Text=text,Width=text.Length<=1?34:90,Height=32,Margin=new Padding(2),AccessibleName=hint};
                if(text.Length==1){b.IconOnly=true;b.IconName=text=="−"?"collapse":text=="□"?"expand":text=="↗"?"open":text=="▶"?"play":text=="‹"?"left":text=="›"?"right":text=="↻"?"refresh":"more";}
                b.Click+=delegate{action();};parent.Controls.Add(b);tips.SetToolTip(b,hint);return b;}
            private Screen OperatorScreen {get{foreach(Screen s in Screen.AllScreens)if(s.DeviceName==monitorName)return s;return null;}}
            public bool EnsureOperatorMonitor()
            {
                if(OperatorScreen!=null)return true;
                using(var dialog=new Form{Text="Choose the operator monitor",Size=new Size(540,230),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false})
                {
                    var label=new Label{Text="Choose the operator screen, never the projector.\r\nUse Windows Extend mode: mirrored displays show the same content.",Dock=DockStyle.Top,Height=65,Padding=new Padding(12)};
                    var list=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Left=15,Top=80,Width=490};
                    foreach(Screen s in Screen.AllScreens)list.Items.Add(s.DeviceName+"  ·  "+s.Bounds.Width+" × "+s.Bounds.Height+(s.Primary?"  ·  Primary":""));
                    list.SelectedIndex=Array.IndexOf(Screen.AllScreens,Screen.FromControl(host));if(list.SelectedIndex<0)list.SelectedIndex=0;
                    var ok=new Button{Text="Use this monitor",DialogResult=DialogResult.OK,Left=330,Top=125,Width=175};dialog.Controls.Add(label);dialog.Controls.Add(list);dialog.Controls.Add(ok);dialog.AcceptButton=ok;
                    if(dialog.ShowDialog(host)!=DialogResult.OK)return false;
                    Screen chosen=Screen.AllScreens[list.SelectedIndex];monitorName=chosen.DeviceName;Location=new Point(chosen.WorkingArea.Right-Width-16,chosen.WorkingArea.Top+16);Save();return true;
                }
            }
            public void Open(){if(!enabled)workspaceWindowState=host.WindowState;enabled=true;tray.Visible=true;Constrain();RefreshState();FollowTarget();Save();}
            public void Shutdown(){if(closingHost)return;closingHost=true;Save();timer.Stop();tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();optionsMenu.Dispose();Close();timer.Dispose();tips.Dispose();}
            public bool WantsOpen {get{return enabled;}}
            private void ReturnToWorkspace()
            {
                Screen s=OperatorScreen;
                if(s!=null){host.WindowState=FormWindowState.Normal;host.Bounds=Clamp(host.Bounds,s.WorkingArea);}
                host.WindowState=workspaceWindowState==FormWindowState.Minimized?FormWindowState.Normal:workspaceWindowState;
                host.Show();host.Activate();
                enabled=false;tray.Visible=false;Hide();Save();
            }
            internal static Rectangle Clamp(Rectangle r,Rectangle area)
            {int w=Math.Min(r.Width,area.Width),h=Math.Min(r.Height,area.Height);return new Rectangle(Math.Max(area.Left,Math.Min(r.Left,area.Right-w)),Math.Max(area.Top,Math.Min(r.Top,area.Bottom-h)),w,h);}
            private void Constrain()
            {if(placing || OperatorScreen==null)return;placing=true;try{Bounds=Clamp(Bounds,OperatorScreen.WorkingArea);}finally{placing=false;}}
            private void SetMode(string value)
            {
                if(value!="Collapsed" && value!="Compact" && value!="Expanded")value="Compact";
                mode=value;bool expanded=mode=="Expanded",collapsed=mode=="Collapsed";
                MinimumSize=collapsed?new Size(340,92):expanded?new Size(430,580):new Size(380,265);
                int[] heights={38,36,62,42,40,34,90,36,36,0,38};
                for(int i=0;i<body.RowStyles.Count;i++){body.RowStyles[i].SizeType=SizeType.Absolute;body.RowStyles[i].Height=(collapsed && i>1)||(!expanded && i>4)?0:heights[i];}
                if(expanded){body.RowStyles[9].SizeType=SizeType.Percent;body.RowStyles[9].Height=100;}
                for(int i=2;i<=10;i++){Control c=body.GetControlFromPosition(0,i);if(c!=null)c.Visible=!collapsed && (i<=4 || expanded);}
                Size=sizes[mode];Constrain();signature="";RefreshState();Save();
            }
            private void RefreshState()
            {
                if(host.IsDisposed)return;
                status.Text=host.audioListening?"● Listening":"○ Stopped";listen.Text=host.audioListening?"Stop":"Listen";
                listen.Enabled=host.audioListening?host.stopListeningButton.Enabled:host.startListeningButton.Enabled;
                meter.SetLevels(host.audioListening?host.overlayRms:-90,host.audioListening?host.overlayPeak:-90);
                ScriptureDetection d=host.lastDetection;
                string version=d!=null && d.TranslationExplicit && !string.IsNullOrEmpty(d.Translation)?d.Translation:"KJV";
                reference.Text=host.detectedReferenceLabel.Text+"  ·  "+version;
                double confidence=d==null?-1:d.MatchScore>=0?d.MatchScore:d.Confidence;
                score.Text=(confidence<0?"Confidence: pending":"Match score: "+(confidence*100).ToString("0")+"%")+"\r\n"+host.detectedConfidenceLabel.Text;
                preview.Enabled=host.previewDetectedButton.Enabled;live.Enabled=host.liveDetectedButton.Enabled;
                source.Text="Input: "+host.audioDeviceCombo.Text;
                string text=host.liveTranscript.Text;if(text.Length>2500)text=text.Substring(text.Length-2500);
                if(transcript.Text!=text){transcript.Text=text;transcript.SelectionStart=transcript.TextLength;transcript.ScrollToCaret();}
                syncing=true;autoPreview.Checked=host.autoPreviewCheck.Checked;autoLive.Checked=host.autoLiveCheck.Checked;
                autoPreview.Enabled=host.autoPreviewCheck.Enabled;autoLive.Enabled=host.autoLiveCheck.Enabled;syncing=false;
                output.Text=host.presentationStatus.Text;
                if(lastDark!=GazelyTheme.Dark){lastDark=GazelyTheme.Dark;GazelyTheme.Apply(this);}
                if(mode=="Expanded")RefreshDetections(false);
            }
            private void RefreshDetections(bool force)
            {
                if(host.suggestionHistory==null)return;
                if(!force && (trackingMouse || detections.RectangleToScreen(detections.ClientRectangle).Contains(Cursor.Position)))return;
                var rows=new System.Collections.Generic.List<ListViewItem>();foreach(ListViewItem row in host.suggestionHistory.Items)if(row.SubItems[4].Text!="Dismissed")rows.Add(row);
                int pages=Math.Max(1,(rows.Count+5)/6);page=Math.Min(page,pages-1);pageLabel.Text="Detections · "+(page+1)+" / "+pages;
                string next=page+":"+GazelyTheme.Dark;for(int i=page*6;i<Math.Min(rows.Count,page*6+6);i++)next+="|"+rows[i].GetHashCode()+rows[i].SubItems[1].Text+rows[i].SubItems[2].Text+rows[i].SubItems[4].Text;
                if(next==signature)return;signature=next;int scroll=-detections.AutoScrollPosition.Y;detections.BeginUpdate();detections.SuspendLayout();
                try{
                    while(detections.Controls.Count>0)detections.Controls[0].Dispose();
                    if(rows.Count==0)detections.Controls.Add(CopyLabel("Quoted and spoken references appear here.",10,false));
                    for(int i=page*6;i<Math.Min(rows.Count,page*6+6);i++)
                    {
                        ListViewItem original=rows[i],snapshot=(ListViewItem)original.Clone();snapshot.Tag=((ScriptureDetection)original.Tag).Snapshot();
                        var d=(ScriptureDetection)snapshot.Tag;var card=new TableLayoutPanel{Width=Math.Max(280,detections.ClientSize.Width-24),Height=136,ColumnCount=1,RowCount=3,Padding=new Padding(8),Margin=new Padding(0,3,0,5),Tag="surface"};
                        card.RowStyles.Add(new RowStyle(SizeType.Absolute,34));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));card.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
                        var rowBar=Row();Button select=AddButton(rowBar,snapshot.SubItems[1].Text,delegate{host.SelectHistoryReference(snapshot);},"Select this detection for review");select.Width=210;
                        Button show=AddButton(rowBar,"▶",delegate{if(original.ListView!=host.suggestionHistory || original.SubItems[4].Text=="Dismissed" || !host.HistoryCanPresent(snapshot))return;host.SelectHistoryReference(snapshot);host.RunDetected(true);},"Show "+d.ReferenceText+" live");show.Tag="primary";show.Enabled=host.HistoryCanPresent(snapshot);card.Controls.Add(rowBar,0,0);
                        string verse=host.quotationIndex==null?d.MatchedText:host.quotationIndex.GetVerseText(d.Book,d.Chapter,d.Verse,d.VerseEnd);
                        var excerpt=CopyLabel(string.IsNullOrEmpty(verse)?"Reference incomplete — select to review":"KJV · "+verse,10,false);excerpt.AutoSize=false;excerpt.AutoEllipsis=true;excerpt.Dock=DockStyle.Fill;card.Controls.Add(excerpt,0,1);
                        var evidence=CopyLabel(snapshot.SubItems[2].Text+" match · "+snapshot.SubItems[4].Text,9,false);evidence.Tag="muted";card.Controls.Add(evidence,0,2);detections.Controls.Add(card);GazelyTheme.Apply(card);
                    }
                }finally{detections.ResumeLayout();detections.AutoScrollPosition=new Point(0,scroll);detections.EndUpdate();}
            }
            private void ShowOptions(Control button)
            {
                // Keep the menu alive through WinForms' entire close/click dispatch.
                // Disposing it in Closed can invalidate the pending item action.
                var menu=optionsMenu;while(menu.Items.Count>0){var item=menu.Items[0];menu.Items.RemoveAt(0);item.Dispose();}
                foreach(string value in new[]{"Collapsed","Compact","Expanded"}){string selected=value;menu.Items.Add(value,null,delegate{SetMode(selected);});}
                menu.Items.Add("Choose operator monitor…",null,delegate{string previous=monitorName;monitorName="";if(!EnsureOperatorMonitor())monitorName=previous;Constrain();});
                var follow=new ToolStripMenuItem("Anchor to presentation window");menu.Items.Add(follow);
                follow.DropDownItems.Add("Free floating",null,delegate{corner="Free";targetProcess=targetTitle="";Save();});
                foreach(var process in System.Diagnostics.Process.GetProcesses())
                {
                    using(process){try{if(process.MainWindowHandle==IntPtr.Zero || !(process.ProcessName.StartsWith("EasyWorship",StringComparison.OrdinalIgnoreCase) || process.ProcessName.StartsWith("ProPresenter",StringComparison.OrdinalIgnoreCase)))continue;
                        string name=process.ProcessName,title=process.MainWindowTitle;IntPtr selectedHandle=process.MainWindowHandle;var window=new ToolStripMenuItem(title);follow.DropDownItems.Add(window);
                        foreach(string position in new[]{"Top right","Top left","Bottom right","Bottom left"}){string selected=position;window.DropDownItems.Add(position,null,delegate{targetHandle=selectedHandle;targetProcess=name;targetTitle=title;corner=selected;FollowTarget();Save();});}
                    }catch(InvalidOperationException){}}
                }
                menu.Items.Add("Full workspace / settings",null,delegate{ReturnToWorkspace();});menu.Show(button,new Point(0,button.Height));
            }
            private void FollowTarget()
            {
                if(!enabled)return;Screen screen=OperatorScreen;if(screen==null || host.presentationRunning){Hide();return;}
                if(corner!="Free")
                {
                    IntPtr handle=IsWindow(targetHandle)?targetHandle:IntPtr.Zero;
                    if(handle==IntPtr.Zero)foreach(var process in System.Diagnostics.Process.GetProcessesByName(targetProcess))using(process){try{if(process.MainWindowTitle==targetTitle){handle=process.MainWindowHandle;break;}}catch(InvalidOperationException){}}
                    targetHandle=handle;
                    NativeRect r;if(handle==IntPtr.Zero || IsIconic(handle) || !GetWindowRect(handle,out r)){Hide();return;}
                    Rectangle window=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
                    if(Screen.FromRectangle(window).DeviceName!=monitorName){Hide();return;}
                    placing=true;try{Bounds=Clamp(new Rectangle(corner.EndsWith("right")?window.Right-Width-12:window.Left+12,corner.StartsWith("Bottom")?window.Bottom-Height-12:window.Top+40,Width,Height),screen.WorkingArea);}finally{placing=false;}
                }
                Constrain();if(!Visible)Show();
            }
            private void Save()
            {
                try{var data=WorkspacePreferences.Load();data["overlay.enabled"]=enabled.ToString();data["overlay.monitor"]=monitorName;data["overlay.mode"]=mode;data["overlay.x"]=Left.ToString();data["overlay.y"]=Top.ToString();data["overlay.corner"]=corner;data["overlay.process"]=targetProcess;data["overlay.title"]=targetTitle;
                    foreach(var item in sizes){data["overlay."+item.Key+".w"]=item.Value.Width.ToString();data["overlay."+item.Key+".h"]=item.Value.Height.ToString();}WorkspacePreferences.Save(data);
                }catch(Exception ex){host.AddLog("Overlay preferences: "+ex.Message);}
            }
            private void Restore()
            {
                try{var data=WorkspacePreferences.Load();string value;int x=30,y=30,w,h;
                    if(data.TryGetValue("overlay.enabled",out value))bool.TryParse(value,out enabled);
                    if(data.TryGetValue("overlay.monitor",out value))monitorName=value;if(data.TryGetValue("overlay.mode",out value))mode=value;
                    if(data.TryGetValue("overlay.corner",out value))corner=value;if(data.TryGetValue("overlay.process",out value))targetProcess=value;if(data.TryGetValue("overlay.title",out value))targetTitle=value;
                    if(data.TryGetValue("overlay.x",out value))int.TryParse(value,out x);if(data.TryGetValue("overlay.y",out value))int.TryParse(value,out y);Location=new Point(x,y);
                    foreach(string key in new[]{"Collapsed","Compact","Expanded"})if(data.TryGetValue("overlay."+key+".w",out value) && int.TryParse(value,out w) && data.TryGetValue("overlay."+key+".h",out value) && int.TryParse(value,out h) && w>=300 && h>=90)sizes[key]=new Size(w,h);
                }catch(Exception ex){host.AddLog("Overlay restore: "+ex.Message);}
            }
            protected override void WndProc(ref Message m)
            {
                // Clamp before Windows applies a drag/resize, avoiding even a
                // transient move across the projector boundary.
                if(m.Msg==0x46 && m.LParam!=IntPtr.Zero && OperatorScreen!=null)
                {
                    var p=(WindowPosition)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(WindowPosition));
                    Rectangle proposed=new Rectangle((p.Flags&2)!=0?Left:p.X,(p.Flags&2)!=0?Top:p.Y,(p.Flags&1)!=0?Width:p.Width,(p.Flags&1)!=0?Height:p.Height);
                    Rectangle safe=Clamp(proposed,OperatorScreen.WorkingArea);
                    if(safe!=proposed){p.X=safe.X;p.Y=safe.Y;p.Width=safe.Width;p.Height=safe.Height;p.Flags&=~3u;System.Runtime.InteropServices.Marshal.StructureToPtr(p,m.LParam,false);}
                }
                if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
                base.WndProc(ref m);
                if(m.Msg==0x84){Point p=PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));bool l=p.X<7,r=p.X>=Width-7,t=p.Y<7,b=p.Y>=Height-7;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;if(hit!=0)m.Result=new IntPtr(hit);}
            }
        }
        private bool restoringPreferences;
        private void SaveWorkspacePreferences()
        {
            if(restoringPreferences || autoLiveCheck==null)return;
            try{var data=WorkspacePreferences.Load();
                data["dark"]=GazelyTheme.Dark.ToString();data["autoLive"]=autoLiveCheck.Checked.ToString();data["autoPrepare"]=autoPreviewCheck.Checked.ToString();data["earlyResults"]=earlyResultsCheck.Checked.ToString();data["audioRefinement"]=refinementCheck.Checked.ToString();
                data["engine"]=speechEngineCombo.Text;data["model"]=whisperModelCombo.Text;data["pickup"]=vadSensitivityCombo.Text;data["input"]=audioDeviceCombo.Text;
                WorkspacePreferences.Save(data);
            }catch(Exception ex){AddLog("Could not save workspace preferences: "+ex.Message);}
        }
        private void RestoreWorkspacePreferences()
        {
            restoringPreferences=true;
            try{
                var data=WorkspacePreferences.Load();string value;bool flag;
                if(data.TryGetValue("dark",out value) && bool.TryParse(value,out flag))GazelyTheme.Dark=flag;
                string[] keys={"engine","model","pickup","input"};ComboBox[] boxes={speechEngineCombo,whisperModelCombo,vadSensitivityCombo,audioDeviceCombo};
                for(int i=0;i<keys.Length;i++)if(data.TryGetValue(keys[i],out value)){int index=boxes[i].FindStringExact(value);if(index>=0)boxes[i].SelectedIndex=index;}
                if(data.TryGetValue("audioRefinement",out value) && bool.TryParse(value,out flag))refinementCheck.Checked=flag;
                if(data.TryGetValue("earlyResults",out value) && bool.TryParse(value,out flag))earlyResultsCheck.Checked=flag;
                if(data.TryGetValue("autoPrepare",out value) && bool.TryParse(value,out flag))autoPreviewCheck.Checked=flag;
                if(data.TryGetValue("autoLive",out value) && bool.TryParse(value,out flag))autoLiveCheck.Checked=flag;
                if(autoLiveCheck.Checked)autoPreviewCheck.Checked=false;
            }catch(Exception ex){AddLog("Could not restore workspace preferences: "+ex.Message);}
            finally{restoringPreferences=false;}
            GazelyTheme.Apply(this);
            autoLiveCheck.CheckedChanged+=delegate{SaveWorkspacePreferences();};autoPreviewCheck.CheckedChanged+=delegate{SaveWorkspacePreferences();};earlyResultsCheck.CheckedChanged+=delegate{SaveWorkspacePreferences();};refinementCheck.CheckedChanged+=delegate{referenceRefinement.Reset();SaveWorkspacePreferences();};
            foreach(ComboBox box in new ComboBox[]{speechEngineCombo,whisperModelCombo,vadSensitivityCombo,audioDeviceCombo})box.SelectedIndexChanged+=delegate{SaveWorkspacePreferences();};
        }
        private ComboBox draftBook;
        private TextBox draftChapter,draftVerse;
        private Label draftStatus;
        private bool operatorDraft,settingDraft;
        private string waitingSource=string.Empty;
        private DateTime waitingSince=DateTime.MinValue;
        private void TrackWaitingReference(string source)
        {
            if(source==waitingSource)return;
            waitingSource=source;waitingSince=DateTime.UtcNow;
        }
        private void SetDraft(string book,int chapter,int verse,int end)
        {
            if(draftBook==null || operatorDraft)return;
            if(string.IsNullOrEmpty(book)){waitingSource=string.Empty;waitingSince=DateTime.MinValue;}
            settingDraft=true;draftBook.Text=book;draftChapter.Text=chapter>0?chapter.ToString():"";
            draftVerse.Text=verse>0?verse.ToString()+(end>verse?"-"+end:""):"";settingDraft=false;
        }
        private void EditDraft(object sender,EventArgs args)
        {
            if(settingDraft)return;
            referenceRefinement.UpdateIntent(null,listeningSession);operatorDraft=true;RefreshPassageAlternatives();++interpretationRevision;lastDetection=null;
            previewDetectedButton.Enabled=liveDetectedButton.Enabled=false;
            draftStatus.Text="Your edit is held. Choose Use, or Release scripture.";
        }
        private void ApplyDraft()
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(draftChapter.Text.Trim(),@"^\d+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(draftVerse.Text.Trim(),@"^\d+(?:\s*-\s*\d+)?$"))
            {draftStatus.Text="Use chapter and verse numbers, such as 49 and 3-4.";return;}
            ScriptureDetection d=new ScriptureDetector().Feed(draftBook.Text+" chapter "+draftChapter.Text+" verse "+draftVerse.Text);
            if(d==null || !string.Equals(d.Book,draftBook.Text.Trim(),StringComparison.OrdinalIgnoreCase) || !BibleCanon.IsValid(d.Book,d.Chapter,d.Verse))
            {draftStatus.Text="Enter a valid book, chapter and verse (or range).";return;}
            d.RequiresReview=true;d.EvidenceKind="operator";d.Reason="Reference entered by the operator";
            d.Confidence=1;d.SourceText="Operator: "+draftBook.Text+" "+draftChapter.Text+":"+draftVerse.Text;
            operatorDraft=false;historyEventKey="operator:"+DateTime.UtcNow.Ticks;ShowDetection(d);referenceRefinement.UpdateIntent(null,listeningSession);operatorDraft=true;RefreshPassageAlternatives();
            draftStatus.Text="Operator reference held • Release scripture to follow new suggestions.";
        }
        private Label workspacePresentationStatus;
        private BufferedDetectionPanel recentHistory;
        private readonly System.Collections.Generic.Dictionary<ListViewItem,Control> candidateRows=new System.Collections.Generic.Dictionary<ListViewItem,Control>();
        private readonly System.Collections.Generic.Dictionary<ListViewItem,string> candidateRowVersions=new System.Collections.Generic.Dictionary<ListViewItem,string>();
        private bool selectingHistory;
        private Label verseExcerpt;
        private readonly System.Collections.Generic.List<ScriptureDetection> passageAlternatives=new System.Collections.Generic.List<ScriptureDetection>();
        private BufferedDetectionPanel passageAlternativesPanel;
        private TableLayoutPanel passageBody;
        private string passageAlternativesStamp=string.Empty;
        private void RememberQuoteAlternative(ScriptureDetection candidate)
        {
            if(candidate.EvidenceKind!="quotation" || candidate.MatchScore<0.94 ||
                (candidate.Reason??string.Empty).StartsWith("Shared wording") || !BibleCanon.IsValid(candidate.Book,candidate.Chapter,candidate.Verse))return;
            passageAlternatives.RemoveAll(delegate(ScriptureDetection d){return d.ReferenceText==candidate.ReferenceText;});
            passageAlternatives.Insert(0,candidate.Snapshot());
            if(passageAlternatives.Count>20)passageAlternatives.RemoveAt(20); // Full session evidence remains in History.
        }
        private void SelectPassageAlternative(ScriptureDetection quote)
        {
            referenceRefinement.UpdateIntent(null,listeningSession);++interpretationRevision;
            ScriptureDetection snapshot=quote.Snapshot();snapshot.RequiresReview=true;
            operatorDraft=false;selectingHistory=true;
            try{ShowDetection(snapshot);}finally{selectingHistory=false;operatorDraft=true;}
            draftStatus.Text="Quote selected for review. Spoken anchor remains below.";
            RefreshPassageAlternatives();
        }
        private void RestoreSpokenAnchor()
        {
            referenceRefinement.UpdateIntent(null,listeningSession);++interpretationRevision;operatorDraft=false;
            RenderActiveReference(true);RefreshPassageAlternatives();
        }
        private void RefreshPassageAlternatives()
        {
            if(passageAlternativesPanel==null)return;
            ScriptureDetection anchor=activeReference.Current;
            string selected=lastDetection==null?string.Empty:lastDetection.ReferenceText;
            string stamp=(anchor==null?"":ActiveReferenceText(anchor))+"|"+selected+"|"+operatorDraft;
            foreach(ScriptureDetection quote in passageAlternatives)stamp+="|"+quote.ReferenceText;
            if(stamp==passageAlternativesStamp)return;passageAlternativesStamp=stamp;
            int scroll=-passageAlternativesPanel.AutoScrollPosition.Y;
            passageAlternativesPanel.BeginUpdate();passageAlternativesPanel.SuspendLayout();
            try{
                while(passageAlternativesPanel.Controls.Count>0)passageAlternativesPanel.Controls[0].Dispose();
                if(operatorDraft && anchor!=null){
                    Button restore=new GazelyButton{Name="RestoreSpokenAnchor",Text="Spoken anchor  ·  "+ActiveReferenceText(anchor),Tag="muted-action",Font=new Font("Segoe UI",12),TextAlign=ContentAlignment.MiddleLeft,Height=36};
                    restore.Click+=delegate{RestoreSpokenAnchor();};passageAlternativesPanel.Controls.Add(restore);
                    evidenceTip.SetToolTip(restore,"Return to the current spoken reference without losing quote suggestions");
                }
                foreach(ScriptureDetection entry in passageAlternatives){
                    if(entry.ReferenceText==selected)continue;
                    ScriptureDetection snapshot=entry.Snapshot();
                    Button pick=new GazelyButton{Name="QuoteAlternative",Text="Possible quote  ·  "+snapshot.ReferenceText,Tag="muted-action",Font=new Font("Segoe UI",12),TextAlign=ContentAlignment.MiddleLeft,Height=36};
                    pick.AccessibleName="Review possible quotation "+snapshot.ReferenceText;
                    pick.Click+=delegate{SelectPassageAlternative(snapshot);};passageAlternativesPanel.Controls.Add(pick);
                    evidenceTip.SetToolTip(pick,"Select for review, without presenting.\r\n"+snapshot.MatchedText);
                }
                foreach(Control row in passageAlternativesPanel.Controls)row.Width=Math.Max(180,passageAlternativesPanel.ClientSize.Width-24);
                passageBody.RowStyles[0].Height=passageAlternativesPanel.Controls.Count==0?0:Math.Min(134,passageAlternativesPanel.Controls.Count*42+8);
                GazelyTheme.Apply(passageAlternativesPanel);
            }finally{passageAlternativesPanel.ResumeLayout();passageAlternativesPanel.AutoScrollPosition=new Point(0,scroll);passageAlternativesPanel.EndUpdate();}
        }
        private bool HistoryCanPresent(ListViewItem row)
        {
            ScriptureDetection d=row.Tag as ScriptureDetection;
            return d!=null && row.SubItems[4].Text!="Provisional" && row.SubItems[4].Text!="Not confirmed" &&
                BibleCanon.IsValid(d.Book,d.Chapter,d.Verse) && (d.VerseEnd==0 || (d.VerseEnd>=d.Verse && BibleCanon.IsValid(d.Book,d.Chapter,d.VerseEnd)));
        }
        private void SelectHistoryReference(ListViewItem row)
        {
            ScriptureDetection d=((ScriptureDetection)row.Tag).Snapshot();
            ++interpretationRevision;operatorDraft=false;
            if(HistoryCanPresent(row))
            {
                d.RequiresReview=true;selectingHistory=true;
                try{ShowDetection(d);}finally{selectingHistory=false;}
                draftStatus.Text="History reference held. Release scripture to follow new suggestions.";
            }
            else
            {
                lastDetection=null;SetDraft(d.Book,d.Chapter,d.Verse,d.VerseEnd);
                detectedReferenceLabel.Text=row.SubItems[1].Text;detectedConfidenceLabel.Text="Complete or confirm this reference";
                detectedReasonLabel.Text=d.Reason;detectedSourceLabel.Text="Heard: "+d.SourceText;
                previewDetectedButton.Enabled=liveDetectedButton.Enabled=false;
                draftStatus.Text="Review the numbers, then choose Use.";UpdateVerseExcerpt(null);
            }
            referenceRefinement.UpdateIntent(null,listeningSession);operatorDraft=true;RefreshPassageAlternatives();
        }
        private Label candidateSearchStatus;
        private Action showCandidateDetails;
        private Label candidateCount;
        private Button candidateUpdates;
        private TextBox candidateFilter;
        private int candidatePage;
        private bool candidateDirty;
        private bool candidateMenuOpen;
        private void ClearCandidatePanel()
        {
            candidateEpoch++;
            lock(quotationJobs)quotationJobs.Clear();
            quotationContext.Reset();pinnedCandidates.Clear();candidatePage=0;
            quoteScopeBook=null;quoteScopeChapter=0;lastQuotationReference=string.Empty;
            passageAlternatives.Clear();RefreshPassageAlternatives();
            foreach(ListViewItem row in suggestionHistory.Items)row.SubItems[4].Text="Dismissed";
            RenderCandidates();
        }
        private System.Windows.Forms.Timer candidateTimer;
        private readonly System.Collections.Generic.HashSet<ListViewItem> pinnedCandidates=new System.Collections.Generic.HashSet<ListViewItem>();
        private void RefreshRecentHistory()
        {
            if(recentHistory==null)return;
            if(candidateMenuOpen || recentHistory.ContainsFocus || (recentHistory.Visible && recentHistory.RectangleToScreen(recentHistory.ClientRectangle).Contains(Cursor.Position)))
            {candidateDirty=true;if(candidateUpdates!=null){candidateUpdates.Text="Updates ready";candidateUpdates.AccessibleName="New detections available. Refresh the list.";candidateUpdates.Tag="primary";GazelyTheme.Apply(candidateUpdates);}return;}
            RenderCandidates();
        }
        private void RenderCandidates()
        {
            if(recentHistory==null || suggestionHistory==null)return;
            candidateDirty=false;if(candidateUpdates!=null){candidateUpdates.Text="Latest";candidateUpdates.AccessibleName="Return to newest detections";candidateUpdates.Tag=null;GazelyTheme.Apply(candidateUpdates);}
            string filter=candidateFilter==null?string.Empty:candidateFilter.Text.Trim();
            var rows=new System.Collections.Generic.List<ListViewItem>();
            foreach(ListViewItem item in suggestionHistory.Items)
                if(item.SubItems[4].Text!="Dismissed" && (filter.Length==0 || (item.SubItems[1].Text+" "+item.SubItems[5].Text).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0))rows.Add(item);
            rows.Sort(delegate(ListViewItem a,ListViewItem b){int pin=pinnedCandidates.Contains(b).CompareTo(pinnedCandidates.Contains(a));return pin!=0?pin:a.Index.CompareTo(b.Index);});
            const int pageSize=20;int pages=Math.Max(1,(rows.Count+pageSize-1)/pageSize);candidatePage=Math.Min(candidatePage,pages-1);
            if(candidateCount!=null)candidateCount.Text=rows.Count+" candidates · "+(candidatePage+1)+" / "+pages+" · "+(candidatePage==0?"Newest first (pinned first)":"Older detections");
            int scroll=-recentHistory.AutoScrollPosition.Y;recentHistory.BeginUpdate();recentHistory.SuspendLayout();
            try{
            var visibleRows=new System.Collections.Generic.HashSet<ListViewItem>();
            for(int n=recentHistory.Controls.Count-1;n>=0;n--)if(!(recentHistory.Controls[n] is GazelyCard))recentHistory.Controls[n].Dispose();
            if(rows.Count==0){Label empty=CopyLabel(filter.Length>0?"No candidates match your search.":"Quoted and mentioned passages will collect here. Every suggestion stays in History.",10,false);recentHistory.Controls.Add(empty);GazelyTheme.Apply(empty);}
            for(int i=candidatePage*pageSize;i<Math.Min(rows.Count,(candidatePage+1)*pageSize);i++)
            {
                ListViewItem original=rows[i];visibleRows.Add(original);
                string stamp=pinnedCandidates.Contains(original).ToString();foreach(ListViewItem.ListViewSubItem cell in original.SubItems)stamp+="\n"+cell.Text;
                ScriptureDetection source=(ScriptureDetection)original.Tag;stamp+="\n"+source.MatchedText+"\n"+source.RequiresReview+"\n"+source.Translation+"\n"+source.TranslationExplicit+"\n"+source.Reason+"\n"+source.Signature;
                Control cached;string prior;
                if(candidateRows.TryGetValue(original,out cached) && candidateRowVersions.TryGetValue(original,out prior) && prior==stamp){recentHistory.Controls.SetChildIndex(cached,i-candidatePage*pageSize);continue;}
                if(cached!=null)cached.Dispose();
                ListViewItem snapshot=(ListViewItem)original.Clone();snapshot.Tag=((ScriptureDetection)original.Tag).Snapshot();
                ScriptureDetection d=(ScriptureDetection)snapshot.Tag;
                TableLayoutPanel line=new GazelyCard();line.Height=178;line.Width=Math.Max(240,recentHistory.ClientSize.Width-22);
                line.ColumnCount=2;line.RowCount=2;line.RowStyles.Add(new RowStyle(SizeType.Absolute,40));line.RowStyles.Add(new RowStyle(SizeType.Percent,100));line.Padding=new Padding(6);line.Margin=new Padding(0,0,0,10);line.Tag="surface";
                line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));
                Button select=new GazelyButton();select.Text=snapshot.SubItems[1].Text;select.Dock=DockStyle.Fill;select.TextAlign=ContentAlignment.MiddleLeft;
                select.Tag="quiet";select.AccessibleName="Review "+snapshot.SubItems[1].Text;select.Click+=delegate{SelectHistoryReference(snapshot);if(showCandidateDetails!=null)showCandidateDetails();};line.Controls.Add(select,0,0);
                string excerpt=string.IsNullOrEmpty(d.MatchedText)?(quotationIndex==null?string.Empty:quotationIndex.GetVerseText(d.Book,d.Chapter,d.Verse,d.VerseEnd)):d.MatchedText;
                string strength=d.EvidenceKind=="paraphrase"?"Possible meaning match":d.EvidenceKind=="quotation"?"Quotation match":snapshot.SubItems[4].Text;
                if(pinnedCandidates.Contains(original))strength+=" · Pinned";
                TableLayoutPanel details=Vertical(22,-100,30);details.Padding=new Padding(10,0,8,0);line.Controls.Add(details,0,1);line.SetColumnSpan(details,2);
                Label meta=CopyLabel(snapshot.Text+" · "+strength,8,false);meta.Tag="muted";Fill(meta);details.Controls.Add(meta,0,0);
                Label scriptureText=CopyLabel("KJV  ·  "+excerpt,11,false);scriptureText.Font=new Font("Georgia",11);scriptureText.AutoEllipsis=true;Fill(scriptureText);details.Controls.Add(scriptureText,0,1);
                Label heard=CopyLabel("Heard: "+d.SourceText,8,false);heard.Tag="muted";heard.AutoEllipsis=true;Fill(heard);details.Controls.Add(heard,0,2);
                evidenceTip.SetToolTip(scriptureText,"KJV: "+excerpt);evidenceTip.SetToolTip(heard,"Heard: "+d.SourceText+"\r\n"+d.Reason);
                FlowLayoutPanel actions=Row();actions.WrapContents=false;actions.Margin=Padding.Empty;line.Controls.Add(actions,1,0);
                Button present=new GazelyButton();present.Text=HistoryCanPresent(snapshot)?"Present":"Complete / edit";present.Size=new Size(102,34);present.Tag=HistoryCanPresent(snapshot)?"primary":null;
                present.Click+=delegate{SelectHistoryReference(snapshot);if(HistoryCanPresent(snapshot))RunDetected(true);else if(showCandidateDetails!=null)showCandidateDetails();};
                Button prepare=new GazelyButton();prepare.Text="Prepare";prepare.Size=new Size(80,34);prepare.Enabled=HistoryCanPresent(snapshot);
                prepare.Click+=delegate{if(!HistoryCanPresent(snapshot))return;SelectHistoryReference(snapshot);RunDetected(false);};
                StyleIcon(present,HistoryCanPresent(snapshot)?"play":"edit",true,present.Text+" "+d.ReferenceText);StyleIcon(prepare,"prepare",true,"Prepare "+d.ReferenceText);
                Button more=new GazelyButton();more.Text="More";StyleIcon(more,"more",true,"Pin or dismiss "+d.ReferenceText);
                ContextMenuStrip options=new ContextMenuStrip();
                more.ContextMenuStrip=options;options.Opened+=delegate{candidateMenuOpen=true;};options.Closed+=delegate{candidateMenuOpen=false;};
                ToolStripItem pin=options.Items.Add(pinnedCandidates.Contains(original)?"Unpin":"Pin");
                pin.Click+=delegate{if(!pinnedCandidates.Add(original))pinnedCandidates.Remove(original);pin.Text=pinnedCandidates.Contains(original)?"Unpin":"Pin";candidateDirty=true;};
                options.Items.Add("Dismiss",null,delegate{original.SubItems[4].Text="Dismissed";line.Enabled=false;candidateDirty=true;});
                more.Click+=delegate{options.Show(more,new Point(0,more.Height));};line.Disposed+=delegate{options.Dispose();};
                present.Margin=prepare.Margin=more.Margin=new Padding(1,2,1,0);actions.Controls.Add(present);actions.Controls.Add(prepare);actions.Controls.Add(more);
                recentHistory.Controls.Add(line);
                GazelyTheme.Apply(line);
                candidateRows[original]=line;candidateRowVersions[original]=stamp;recentHistory.Controls.SetChildIndex(line,i-candidatePage*pageSize);
            }
            foreach(ListViewItem obsolete in new System.Collections.Generic.List<ListViewItem>(candidateRows.Keys))if(!visibleRows.Contains(obsolete)){candidateRows[obsolete].Dispose();candidateRows.Remove(obsolete);candidateRowVersions.Remove(obsolete);}
            }finally{recentHistory.ResumeLayout();recentHistory.AutoScrollPosition=new Point(0,scroll);recentHistory.EndUpdate();}
        }
        private void UpdateVerseExcerpt(ScriptureDetection d)
        {
            if(verseExcerpt==null)return;
            string text=d==null?string.Empty:quotationIndex==null?d.MatchedText:quotationIndex.GetVerseText(d.Book,d.Chapter,d.Verse,d.VerseEnd);
            string previewLabel=d!=null && d.TranslationExplicit && d.Translation!="KJV"?
                "KJV preview (output: "+d.Translation+") · ":"KJV · ";
            verseExcerpt.Text=string.IsNullOrEmpty(text)?"Say a reference, or complete it below.":previewLabel+text;
        }
        private static Control Field(string title,Control input,int width)
        {
            TableLayoutPanel field=Vertical(22,42);field.Dock=DockStyle.None;field.Size=new Size(width,70);field.Margin=new Padding(0,0,14,14);
            Label label=CopyLabel(title,9,false);label.Tag="muted";field.Controls.Add(label,0,0);
            GazelyInputFrame frame=new GazelyInputFrame(input);frame.Dock=DockStyle.Fill;field.Controls.Add(frame,0,1);return field;
        }
        private void StyleIcon(Button button,string icon,bool only,string description)
        {
            GazelyButton styled=button as GazelyButton;
            if(styled!=null){styled.IconName=icon;styled.IconOnly=only;}
            button.AccessibleName=description;evidenceTip.SetToolTip(button,description);
            if(only)button.Size=new Size(36,34);
        }
        private void RefineLivePage(TabPage page,Button evidence,Button useDraft,Button followSpeech)
        {
            bool candidatesExpanded=false,editing=false;
            Action refreshArrangement=delegate{};
            TableLayoutPanel layout=Vertical(68,-100,28);layout.Name="LiveContent";layout.Padding=new Padding(24,12,24,10);
            Panel canvas=MountCenteredPage(page,layout,580,true);
            TableLayoutPanel header=new TableLayoutPanel();header.Dock=DockStyle.Fill;header.ColumnCount=2;header.RowCount=1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));layout.Controls.Add(header,0,0);
            TableLayoutPanel heading=Vertical(32,24);heading.Controls.Add(CopyLabel("In the moment.",20,true),0,0);
            Label tagline=CopyLabel("Listen. Discover. Bring it forward.",10,false);tagline.Tag="muted";heading.Controls.Add(tagline,0,1);header.Controls.Add(heading,0,0);
            startListeningButton.Size=new Size(146,36);stopListeningButton.Size=new Size(36,36);
            StyleIcon(startListeningButton,"play",false,"Start listening");StyleIcon(stopListeningButton,"stop",true,"Stop listening");
            audioStatusLabel.Size=new Size(80,30);whisperStatusLabel.Size=new Size(95,30);
            workspaceAudioMeter.MinimumSize=new Size(72,24);workspaceAudioMeter.Size=new Size(90,24);
            FlowLayoutPanel listening=Row(workspaceAudioMeter,audioStatusLabel,startListeningButton,stopListeningButton,whisperStatusLabel);listening.Name="ListeningToolbar";
            listening.Dock=DockStyle.Right;listening.Width=510;
            listening.Padding=new Padding(0,9,0,0);header.Controls.Add(listening,1,0);

            TableLayoutPanel body=new TableLayoutPanel();body.Name="WorkspacePanes";body.Dock=DockStyle.Fill;body.Margin=Padding.Empty;layout.Controls.Add(body,0,1);
            TableLayoutPanel scripture=Vertical(26,48,24,-100,34,0,24,44,36);scripture.Name="SelectedPassage";scripture.Tag="surface";scripture.Padding=new Padding(16);scripture.Margin=new Padding(7,0,0,0);
            ((GazelyCard)scripture).AccentEdge=true;
            Label suggested=CopyLabel("SELECTED PASSAGE",10,true);suggested.Tag="muted";Fill(suggested);scripture.Controls.Add(suggested,0,0);scripture.Controls.Add(detectedReferenceLabel,0,1);
            detectedReferenceLabel.Font=new Font("Segoe UI",23,FontStyle.Bold);detectedReferenceLabel.AutoEllipsis=true;
            scripture.Controls.Add(detectedConfidenceLabel,0,2);
            Panel readingViewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};scripture.Controls.Add(readingViewport,0,3);
            passageBody=Vertical(0,-100);passageBody.Dock=DockStyle.None;readingViewport.Controls.Add(passageBody);
            Action arrangeReading=delegate{int height=Math.Max(220,readingViewport.ClientSize.Height);passageBody.SetBounds(0,readingViewport.AutoScrollPosition.Y,Math.Max(0,readingViewport.ClientSize.Width-(height>readingViewport.ClientSize.Height?SystemInformation.VerticalScrollBarWidth:0)),height);};
            readingViewport.Resize+=delegate{arrangeReading();};arrangeReading();
            passageAlternativesPanel=new BufferedDetectionPanel{Name="PassageAlternatives",Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Margin=Padding.Empty};
            passageBody.Controls.Add(passageAlternativesPanel,0,0);
            passageAlternativesPanel.Resize+=delegate{foreach(Control row in passageAlternativesPanel.Controls)row.Width=Math.Max(180,passageAlternativesPanel.ClientSize.Width-24);};
            verseExcerpt=CopyLabel("Scripture text will appear here. Select a detection, or enter a reference.",16,false);verseExcerpt.Font=new Font("Georgia",16);Fill(verseExcerpt);verseExcerpt.AutoEllipsis=true;passageBody.Controls.Add(verseExcerpt,0,1);
            Button edit=new GazelyButton();edit.Name="EditReference";edit.Text="Edit reference";edit.Size=new Size(145,34);StyleIcon(edit,"edit",false,"Enter or adjust book, chapter and verse");
            followSpeech.Name="ReleaseScripture";followSpeech.Text="Release scripture";followSpeech.Size=new Size(132,34);followSpeech.Tag="quiet";
            evidenceTip.SetToolTip(followSpeech,"Release the active scripture or operator hold and resume searching. Quote suggestions may select a new passage.");
            scripture.Controls.Add(Row(edit,followSpeech),0,4);
            useDraft.Text="Use edit";useDraft.Size=new Size(82,34);useDraft.Margin=new Padding(0,0,0,0);
            FlowLayoutPanel fields=Row(Field("Book",draftBook,142),Field("Chapter",draftChapter,62),Field("Verse / range",draftVerse,80),useDraft);fields.Name="ReferenceEditor";fields.Visible=false;
            scripture.Controls.Add(fields,0,5);
            Action showEditor=delegate{editing=true;fields.Visible=true;scripture.RowStyles[5].Height=112;edit.Text="Close editor";refreshArrangement();};
            edit.Click+=delegate{editing=!editing;fields.Visible=editing;scripture.RowStyles[5].Height=editing?112:0;edit.Text=editing?"Close editor":"Edit reference";refreshArrangement();};
            scripture.Controls.Add(draftStatus,0,6);
            liveDetectedButton.Text="Present";liveDetectedButton.Size=new Size(138,40);StyleIcon(liveDetectedButton,"play",false,"Present selected scripture now");
            previewDetectedButton.Size=new Size(112,40);StyleIcon(previewDetectedButton,"prepare",false,"Prepare selected scripture");StyleIcon(evidence,"info",true,"View source and matching evidence");
            scripture.Controls.Add(Row(liveDetectedButton,previewDetectedButton,evidence),0,7);
            autoPreviewCheck.Dock=autoLiveCheck.Dock=DockStyle.None;autoPreviewCheck.Size=new Size(172,30);autoLiveCheck.Size=new Size(180,30);autoPreviewCheck.Text="Auto prepare";
            scripture.Controls.Add(Row(autoPreviewCheck,autoLiveCheck),0,8);

            TableLayoutPanel transcript=Vertical(40,0,-100);transcript.Name="LiveTranscriptCard";transcript.Tag="surface";transcript.Padding=new Padding(20);transcript.Margin=new Padding(0,0,7,0);
            clearTranscriptButton.Size=new Size(70,30);clearTranscriptButton.Tag="quiet";
            transcript.Controls.Add(Row(CopyLabel("Live transcript",13,true),clearTranscriptButton),0,0);transcript.Controls.Add(hypothesisLabel,0,1);transcript.Controls.Add(transcriptTextBox,0,2);
            hypothesisLabel.Visible=false;
            transcriptTextBox.Font=new Font("Segoe UI",11);hypothesisLabel.Font=new Font("Segoe UI",10);

            TableLayoutPanel candidates=Vertical(40,32,40,-100,34);candidates.Name="CandidateSection";candidates.Tag="surface";candidates.Padding=new Padding(16);candidates.Margin=new Padding(7,0,7,0);
            Button expand=new GazelyButton();expand.Name="ExpandCandidates";expand.Text="Expand list";expand.Size=new Size(98,30);expand.Tag="quiet";
            Button menuButton=new GazelyButton();menuButton.Name="WorkspaceOptions";menuButton.Text="Workspace options";StyleIcon(menuButton,"more",true,"History and workspace options");
            ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("All history",null,delegate{((TabControl)page.Parent).SelectedIndex=6;});
            ToolStripItem reset=menu.Items.Add("Reset version tracking",null,delegate{ResetVersionTracking();});reset.Name="WorkspaceResetVersion";reset.Enabled=resyncVersionButton.Enabled;
            resyncVersionButton.EnabledChanged+=delegate{reset.Enabled=resyncVersionButton.Enabled;};
            menuButton.ContextMenuStrip=menu;menuButton.Click+=delegate{menu.Show(menuButton,new Point(0,menuButton.Height));};FormClosed+=delegate{menu.Dispose();};
            Button clearCandidates=new GazelyButton();clearCandidates.Name="ClearCandidates";clearCandidates.Text="Clear";clearCandidates.Size=new Size(62,30);clearCandidates.Tag="quiet";
            clearCandidates.Click+=delegate{if(MessageBox.Show(this,"Clear the detections panel? Entries remain in History. New speech will add new detections.","Clear detections",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)ClearCandidatePanel();};
            candidates.Controls.Add(Row(CopyLabel("Detections",14,true),expand,clearCandidates,menuButton),0,0);
            candidateCount=CopyLabel("0 candidates",9,false);candidateCount.Tag="muted";candidates.Controls.Add(candidateCount,0,1);
            candidateFilter=new TextBox();candidateFilter.AccessibleName="Search scripture candidates";
            GazelyInputFrame searchFrame=new GazelyInputFrame(candidateFilter);searchFrame.Dock=DockStyle.Fill;
            evidenceTip.SetToolTip(searchFrame,"Filter by scripture reference or words that were heard");
            TableLayoutPanel searchRow=new TableLayoutPanel();searchRow.Dock=DockStyle.Fill;searchRow.ColumnCount=2;searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,45));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));searchRow.Controls.Add(CopyLabel("Find",10,false),0,0);searchRow.Controls.Add(searchFrame,1,0);
            candidates.Controls.Add(searchRow,0,2);candidateFilter.TextChanged+=delegate{candidatePage=0;RenderCandidates();};
            recentHistory=new BufferedDetectionPanel();recentHistory.Dock=DockStyle.Fill;recentHistory.AutoScroll=true;recentHistory.FlowDirection=FlowDirection.TopDown;recentHistory.WrapContents=false;recentHistory.Margin=new Padding(0,10,0,8);
            recentHistory.Resize+=delegate{foreach(Control line in recentHistory.Controls)line.Width=Math.Max(240,recentHistory.ClientSize.Width-22);};candidates.Controls.Add(recentHistory,0,3);
            Button previous=new GazelyButton();previous.Text="Previous";StyleIcon(previous,"left",true,"Previous page of detections");previous.Click+=delegate{candidatePage=Math.Max(0,candidatePage-1);recentHistory.AutoScrollPosition=Point.Empty;RenderCandidates();};
            Button next=new GazelyButton();next.Text="Next";StyleIcon(next,"right",true,"Next page of detections");next.Click+=delegate{candidatePage++;recentHistory.AutoScrollPosition=Point.Empty;RenderCandidates();};
            candidateUpdates=new GazelyButton();candidateUpdates.Name="LatestCandidates";candidateUpdates.Text="Latest";StyleIcon(candidateUpdates,"refresh",true,"Return to newest detections on page 1; pinned entries come first");
            candidateUpdates.Click+=delegate{candidateFilter.Text=string.Empty;candidatePage=0;recentHistory.AutoScrollPosition=Point.Empty;RenderCandidates();};
            candidateSearchStatus=CopyLabel("Quotation search loading…",8,false);candidateSearchStatus.MaximumSize=new Size(210,32);candidateSearchStatus.Tag="muted";
            candidates.Controls.Add(Row(previous,next,candidateUpdates,candidateSearchStatus),0,4);
            layout.Controls.Add(workspacePresentationStatus,0,2);
            bool arranging=false;
            Action arrange=delegate{
                if(arranging)return;arranging=true;body.SuspendLayout();
                try{
                    bool wide=canvas.ClientSize.Width>=1400;
                    transcript.RowStyles[0].Height=wide?40:32;transcript.RowStyles[1].Height=wide?50:26;transcript.Padding=new Padding(wide?20:16);
                    body.Controls.Clear();body.SetColumnSpan(candidates,1);body.SetRowSpan(candidates,1);body.ColumnStyles.Clear();body.RowStyles.Clear();
                    transcript.Visible=scripture.Visible=!candidatesExpanded;
                    if(candidatesExpanded){body.ColumnCount=1;body.RowCount=1;body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(candidates,0,0);}
                    else if(wide){body.ColumnCount=3;body.RowCount=1;body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(transcript,0,0);body.Controls.Add(candidates,1,0);body.Controls.Add(scripture,2,0);}
                    else if(editing){body.ColumnCount=2;body.RowCount=1;body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(scripture,0,0);body.Controls.Add(candidates,1,0);transcript.Visible=false;}
                    else{body.ColumnCount=2;body.RowCount=2;body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Min(420,Math.Max(330,body.Height-150))));body.Controls.Add(transcript,0,0);body.Controls.Add(scripture,0,1);body.Controls.Add(candidates,1,0);body.SetRowSpan(candidates,2);}
                    scripture.Margin=wide?new Padding(7,0,0,0):new Padding(0,10,7,0);
                    if(wide || candidatesExpanded)body.SetColumnSpan(candidates,1);
                    body.AutoScroll=false;body.AutoScrollMinSize=Size.Empty;
                    expand.Text=candidatesExpanded?"Show details":"Expand list";
                }finally{body.ResumeLayout();arranging=false;}
            };
            refreshArrangement=arrange;canvas.Resize+=delegate{arrange();};expand.Click+=delegate{candidatesExpanded=!candidatesExpanded;arrange();};
            showCandidateDetails=delegate{candidatesExpanded=false;arrange();if(lastDetection==null)showEditor();};
            arrange();RefreshRecentHistory();
            candidateTimer=new System.Windows.Forms.Timer();candidateTimer.Interval=750;candidateTimer.Tick+=delegate{if(candidateDirty)RefreshRecentHistory();};candidateTimer.Start();
            FormClosed+=delegate{candidateTimer.Stop();candidateTimer.Dispose();};
        }
        private static TableLayoutPanel PageContent(TabPage page,string title,string subtitle,int minHeight)
        {
            TableLayoutPanel content=Vertical(44,44,-100);content.Padding=new Padding(28,20,28,20);
            MountCenteredPage(page,content,minHeight);
            content.Controls.Add(CopyLabel(title,24,true),0,0);Label note=CopyLabel(subtitle,10,false);note.Tag="muted";Fill(note);content.Controls.Add(note,0,1);return content;
        }
        private static Panel MountCenteredPage(TabPage page,Control content,int minHeight,bool fullWidth=false)
        {
            page.Controls.Clear();page.AutoScroll=false;
            Panel viewport=new Panel();viewport.Dock=DockStyle.Fill;viewport.Margin=Padding.Empty;viewport.AutoScroll=minHeight>0;
            viewport.AutoScrollMinSize=new Size(0,minHeight);page.Controls.Add(viewport);
            content.Dock=DockStyle.None;content.Name="CenteredPageContent";content.Margin=Padding.Empty;viewport.Controls.Add(content);
            bool arranging=false;
            Action arrange=delegate{
                if(arranging)return;arranging=true;
                try{int width=fullWidth?viewport.ClientSize.Width:Math.Min(1440,viewport.ClientSize.Width);
                    content.SetBounds(Math.Max(0,(viewport.ClientSize.Width-width)/2),viewport.AutoScrollPosition.Y,width,Math.Max(minHeight,viewport.ClientSize.Height));
                }finally{arranging=false;}
            };
            viewport.Resize+=delegate{arrange();};arrange();return viewport;
        }
        private static FlowLayoutPanel FormCard(int width)
        {
            FlowLayoutPanel panel=new GazelyFlowCard();panel.Width=width;panel.Height=430;panel.Padding=new Padding(24);panel.FlowDirection=FlowDirection.TopDown;
            panel.WrapContents=false;panel.Tag="surface";panel.AutoScroll=true;return panel;
        }
        private static void AddAside(TableLayoutPanel page,Control card,Control aside)
        {
            FlowLayoutPanel group=new FlowLayoutPanel();group.Dock=DockStyle.Fill;group.AutoScroll=true;group.WrapContents=true;
            card.Margin=new Padding(0,0,22,20);aside.Margin=new Padding(0,0,0,20);
            group.Controls.Add(card);group.Controls.Add(aside);page.Controls.Add(group,0,2);
            bool arranging=false;
            Action arrange=delegate{
                if(arranging)return;arranging=true;
                try{
                    int available=Math.Max(0,group.ClientSize.Width-24);
                    if(available>=920){card.Width=(int)((available-22)*0.62);aside.Width=available-22-card.Width;}
                    else{card.Width=Math.Min(660,available);aside.Width=Math.Min(360,available);}
                }finally{arranging=false;}
            };
            group.Resize+=delegate{arrange();};arrange();
        }
        private void RefineSecondaryPages(TabPage manualPage,TabPage setupPage,TabPage mapPage,TabPage integrationsPage,TabPage diagnosticsPage)
        {
            TableLayoutPanel manual=PageContent(manualPage,"A reference, ready to go.","Find it yourself. Take it to the room.",570);
            FlowLayoutPanel manualCard=FormCard(650);manual.Controls.Add(manualCard,0,2);
            manualCard.Controls.Add(CopyLabel("Choose your scripture",15,true));
            manualCard.Controls.Add(Field("Reference · e.g. Genesis 49:3-4",scriptureTextBox,330));
            manualCard.Controls.Add(Field("Bible version",versionCombo,250));
            changeVersionCheck.AutoSize=false;changeVersionCheck.Size=new Size(420,30);manualCard.Controls.Add(changeVersionCheck);
            previewButton.Size=liveButton.Size=new Size(112,38);testVersionButton.Size=new Size(150,38);
            liveButton.Width=138;previewButton.Width=126;StyleIcon(liveButton,"play",false,"Present scripture now");StyleIcon(previewButton,"prepare",false,"Prepare scripture");
            FlowLayoutPanel actions=Row(previewButton,liveButton,testVersionButton);actions.Dock=DockStyle.None;actions.Size=new Size(550,48);manualCard.Controls.Add(actions);
            FlowLayoutPanel tools=Row(refreshButton,resyncVersionButton);tools.Dock=DockStyle.None;tools.Size=new Size(550,42);manualCard.Controls.Add(tools);
            refreshButton.Size=new Size(155,34);resyncVersionButton.Size=new Size(185,34);
            connectionLabel.AutoSize=true;manualCard.Controls.Add(connectionLabel);manualCard.Controls.Add(versionStateLabel);
            manualCard.Height=520;
            Label manualResult=CopyLabel(presentationStatus.Text,10,false);manualResult.MaximumSize=new Size(560,0);manualResult.Tag="muted";manualCard.Controls.Add(manualResult);
            presentationStatus.TextChanged+=delegate{manualResult.Text=presentationStatus.Text;};
            TableLayoutPanel preview=Vertical(28,48,-100,52);preview.Tag="surface";preview.Padding=new Padding(20);preview.Dock=DockStyle.None;preview.Size=new Size(280,380);
            preview.Controls.Add(CopyLabel("Scripture preview",11,true),0,0);
            Label manualReference=CopyLabel("",18,false);Fill(manualReference);preview.Controls.Add(manualReference,0,1);
            Label manualVerse=CopyLabel("",12,false);manualVerse.Font=new Font("Georgia",12);Fill(manualVerse);manualVerse.AutoEllipsis=true;preview.Controls.Add(manualVerse,0,2);
            Label previewNote=CopyLabel("KJV preview · Output uses the selected Bible version.",9,false);Fill(previewNote);previewNote.Tag="muted";preview.Controls.Add(previewNote,0,3);
            Action updatePreview=delegate{
                ScriptureDetection reference=new ScriptureDetector().Feed(scriptureTextBox.Text);
                manualReference.Text=reference==null?"Enter a reference":reference.ReferenceText;
                manualVerse.Text=reference==null?"Use a book, chapter and verse, such as John 3:16.":quotationIndex==null?"Loading local KJV text…":quotationIndex.GetVerseText(reference.Book,reference.Chapter,reference.Verse,reference.VerseEnd);
            };
            scriptureTextBox.TextChanged+=delegate{updatePreview();};updatePreview();
            System.Windows.Forms.Timer previewTimer=new System.Windows.Forms.Timer();previewTimer.Interval=1000;
            previewTimer.Tick+=delegate{if(quotationIndex!=null){updatePreview();previewTimer.Stop();}};previewTimer.Start();FormClosed+=delegate{previewTimer.Dispose();};
            AddAside(manual,manualCard,preview);

            TableLayoutPanel setup=PageContent(setupPage,"A clear starting point.","Set up your sound, then let Gazely listen.",670);
            FlowLayoutPanel setupCard=FormCard(660);setupCard.Height=585;setup.Controls.Add(setupCard,0,2);
            setupCard.Controls.Add(CopyLabel("Audio input",15,true));setupCard.Controls.Add(Field("Listen to",audioDeviceCombo,390));
            setupCard.Controls.Add(CopyLabel("Speech recognition",15,true));
            FlowLayoutPanel pair=Row();pair.Dock=DockStyle.None;pair.Size=new Size(580,88);
            pair.Controls.Add(Field("Engine",speechEngineCombo,250));
            Panel profile=new Panel();profile.Controls.Add(whisperModelCombo);profile.Controls.Add(recognizerCombo);whisperModelCombo.Dock=recognizerCombo.Dock=DockStyle.Fill;
            pair.Controls.Add(Field("Speed / accuracy",profile,240));setupCard.Controls.Add(pair);
            setupCard.Controls.Add(Field("Voice pickup",vadSensitivityCombo,230));
            earlyResultsCheck.AutoSize=false;earlyResultsCheck.Size=new Size(550,32);earlyResultsCheck.Text="Show references while speech is being revised";setupCard.Controls.Add(earlyResultsCheck);
            refinementCheck.Size=new Size(550,32);setupCard.Controls.Add(refinementCheck);
            audioMeter.Dock=DockStyle.None;audioMeter.Size=new Size(370,28);setupCard.Controls.Add(audioMeter);
            audioDbLabel.AutoSize=true;setupCard.Controls.Add(audioDbLabel);recognizerStatusLabel.AutoSize=true;setupCard.Controls.Add(recognizerStatusLabel);
            refreshAudioButton.Size=new Size(195,36);setupCard.Controls.Add(refreshAudioButton);
            foreach(Control item in setupCard.Controls)item.Anchor=AnchorStyles.Left|AnchorStyles.Top;
            foreach(Control item in manualCard.Controls)item.Anchor=AnchorStyles.Left|AnchorStyles.Top;
            TableLayoutPanel inputNote=Vertical(30,48,-100);inputNote.Tag="surface";inputNote.Dock=DockStyle.None;inputNote.Size=new Size(280,280);inputNote.Padding=new Padding(22);
            inputNote.Controls.Add(CopyLabel("Input check",11,true),0,0);inputNote.Controls.Add(CopyLabel("Make yourself heard.",16,false),0,1);
            Label inputHelp=CopyLabel("Speak at your normal volume. The meter should move with your voice.\r\n\r\nLocal AI processes speech on this PC. More accurate uses the larger model and needs more processing time.\r\n\r\nSettings apply when you start listening.",10,false);Fill(inputHelp);inputHelp.Tag="muted";inputNote.Controls.Add(inputHelp,0,2);AddAside(setup,setupCard,inputNote);

            TableLayoutPanel mapping=PageContent(mapPage,"Your Bible versions.","Match the installed Bible order in EasyWorship. Recheck after adding or reordering versions.",570);
            TableLayoutPanel mapCard=Vertical(38,-100,48);mapCard.Tag="surface";mapCard.Padding=new Padding(24);mapCard.MaximumSize=new Size(700,0);mapping.Controls.Add(mapCard,0,2);
            mapCard.Controls.Add(CopyLabel("One CODE=INDEX per line, starting at zero.",10,false),0,0);Fill(mapEditor);mapCard.Controls.Add(mapEditor,0,1);mapCard.Controls.Add(Row(saveMapButton,reloadMapButton),0,2);

            // Keep the existing integration controls and event handlers, including cancellation.
            FlowLayoutPanel old=integrationsPage.Controls[0] as FlowLayoutPanel;
            Control[] integrationControls=new Control[old.Controls.Count];old.Controls.CopyTo(integrationControls,0);
            TableLayoutPanel integrations=PageContent(integrationsPage,"Connected to your workflow.","Choose where Gazely prepares and presents content.",730);
            FlowLayoutPanel integrationCard=FormCard(800);integrationCard.Height=600;integrations.Controls.Add(integrationCard,0,2);
            for(int i=2;i<integrationControls.Length;i++)
            {
                Control item=integrationControls[i];item.MaximumSize=new Size(735,0);item.Margin=new Padding(0,0,0,15);
                if(item==easyWorshipEnabled){item.AutoSize=false;item.Size=new Size(640,34);}
                integrationCard.Controls.Add(item);
            }
            TableLayoutPanel diagnostics=PageContent(diagnosticsPage,"Behind the scenes.","Local session events, speech timings and presentation results.",540);
            TableLayoutPanel logCard=Vertical(-100);logCard.Padding=new Padding(22);logCard.Tag="surface";diagnostics.Controls.Add(logCard,0,2);Fill(logTextBox);logCard.Controls.Add(logTextBox);
        }
        private AudioLevelMeter workspaceAudioMeter;
        private ListView suggestionHistory;
        private Button historyPrepareButton,historyPresentButton,historyLoadButton;
        private TextBox historyDetails;
        private string historyEventKey=string.Empty;
        private ChapterRecoverySuggestion lastChapterRecovery=null;
        private string lastChapterRecoverySource=string.Empty;
        private void RequestChapterRecovery(string book,string source,int session,long revision)
        {
            ChapterPronunciationRecovery recovery=chapterRecovery;
            if(recovery==null)return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ChapterRecoverySuggestion proposal=recovery.Find(book,source);
                    if(proposal==null || IsDisposed || !IsHandleCreated)return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if(!audioListening || session!=listeningSession || revision!=interpretationRevision)return;
                        lastChapterRecovery=proposal;lastChapterRecoverySource=source;
                        DisplayChapterRecovery(proposal,source,session);
                    });
                }
                catch(Exception ex){AddLog("Chapter pronunciation lookup failed: "+ex.Message);}
            });
        }
        private void DisplayChapterRecovery(ChapterRecoverySuggestion proposal,string source,int session)
        {
            if(operatorDraft)return;
            SetDraft(proposal.Book,proposal.Chapter,0,0);
            historyEventKey="unresolved:"+session+":"+source;
            if(proposal.Reference!=null)
            {
                if(!object.ReferenceEquals(lastDetection,proposal.Reference))ShowDetection(proposal.Reference);
                return;
            }
            lastDetection=null;
            detectedReferenceLabel.Text="Possible "+proposal.Book+" "+proposal.Chapter;
            detectedConfidenceLabel.Text="Verse unclear — confirmation needed";
            detectedReasonLabel.Text="Pronunciation suggests chapter "+proposal.Chapter+" for '"+proposal.HeardChapter+"'.";
            detectedSourceLabel.Text="Heard: "+source;
            previewDetectedButton.Enabled=liveDetectedButton.Enabled=false;
            ListViewItem row;
            if(historyRows.TryGetValue(historyEventKey,out row))
            {
                row.SubItems[1].Text=detectedReferenceLabel.Text;row.SubItems[2].Text=(proposal.Score*100).ToString("0")+"%";
                row.SubItems[3].Text="pronunciation";row.SubItems[4].Text="Verse unclear";
                row.Tag=new ScriptureDetection{Book=proposal.Book,Chapter=proposal.Chapter,SourceText=source,RequiresReview=true,
                    Reason=detectedReasonLabel.Text+" Verse numbers remain unresolved; presentation is disabled."};
                if(row.Selected)ShowHistoryDetails();
                RefreshRecentHistory();
            }
        }
        private readonly System.Collections.Generic.Dictionary<string,ListViewItem> historyRows=new System.Collections.Generic.Dictionary<string,ListViewItem>();
        private readonly System.Collections.Generic.Dictionary<ListViewItem,System.Collections.Generic.List<string>> candidateEvidence=new System.Collections.Generic.Dictionary<ListViewItem,System.Collections.Generic.List<string>>();
        private readonly System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<string>> candidateAttempts=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<string>>();
        private void RecordCandidateAttempt(ScriptureRequest request,string version,PresentationResult result)
        {
            System.Collections.Generic.List<string> attempts;
            if(!candidateAttempts.TryGetValue(request.ReferenceText,out attempts)){attempts=new System.Collections.Generic.List<string>();candidateAttempts[request.ReferenceText]=attempts;}
            attempts.Add(DateTime.Now.ToString("HH:mm:ss")+" · "+version+" · "+result.Outcome+" · "+result.Detail);
            ShowHistoryDetails();
        }
        private void FinishHistoryCandidate(string key)
        {
            ListViewItem row;
            if(historyRows.TryGetValue(key,out row) && row.SubItems[4].Text=="Provisional") row.SubItems[4].Text="Not confirmed";
            RefreshRecentHistory();
        }
        private void RecordSuggestion(ScriptureDetection detection,bool provisional,string key)
        {
            if(suggestionHistory==null) return;
            if(string.IsNullOrEmpty(key)) key=Guid.NewGuid().ToString("N");
            ListViewItem row;
            if(!historyRows.TryGetValue(key,out row))
            {
                row=new ListViewItem(new string[]{DateTime.Now.ToString("HH:mm:ss"),"","","","",""});
                historyRows[key]=row;suggestionHistory.Items.Insert(0,row);
            }
            System.Collections.Generic.List<string> evidence;
            if(!candidateEvidence.TryGetValue(row,out evidence)){evidence=new System.Collections.Generic.List<string>();candidateEvidence[row]=evidence;}
            string occurrence=DateTime.Now.ToString("HH:mm:ss")+" · "+detection.SourceText;
            if(evidence.Count==0 || evidence[evidence.Count-1]!=occurrence)evidence.Add(occurrence);
            ScriptureDetection previous=row.Tag as ScriptureDetection;
            if(previous!=null && previous.EvidenceKind!="paraphrase" && detection.EvidenceKind=="paraphrase")
            {RefreshRecentHistory();return;}
            bool dismissed=row.SubItems[4].Text=="Dismissed";
            row.SubItems[1].Text=detection.ReferenceText;
            double score=detection.MatchScore>=0?detection.MatchScore:detection.Confidence;
            row.SubItems[2].Text=provisional?"Pending":(100*Math.Max(0,Math.Min(1,score))).ToString("0")+"%";
            row.SubItems[3].Text=detection.EvidenceKind;
            row.SubItems[4].Text=provisional?"Provisional":detection.RequiresReview?"Review required":"Final suggestion";
            if(dismissed)row.SubItems[4].Text="Dismissed";
            row.SubItems[5].Text=detection.SourceText??string.Empty;
            row.Tag=detection.Snapshot();
            if(row.Selected) ShowHistoryDetails();
            RefreshRecentHistory();
        }
        private void ShowHistoryDetails()
        {
            if(historyLoadButton!=null)historyLoadButton.Enabled=suggestionHistory.SelectedItems.Count>0;
            if(historyPrepareButton!=null)historyPrepareButton.Enabled=historyPresentButton.Enabled=suggestionHistory.SelectedItems.Count>0 && HistoryCanPresent(suggestionHistory.SelectedItems[0]);
            if(suggestionHistory.SelectedItems.Count==0) return;
            ListViewItem row=suggestionHistory.SelectedItems[0];ScriptureDetection item=(ScriptureDetection)row.Tag;
            historyDetails.Text=row.SubItems[1].Text+" | "+row.SubItems[2].Text+" match score | "+row.SubItems[4].Text+
                "\r\n\r\nHeard: "+item.SourceText+"\r\n\r\n"+item.Reason+
                (string.IsNullOrEmpty(item.MatchedText)?"":"\r\n\r\nKJV evidence: "+item.MatchedText);
            System.Collections.Generic.List<string> occurrences;
            if(candidateEvidence.TryGetValue(row,out occurrences) && occurrences.Count>1)historyDetails.Text+="\r\n\r\nOccurrences:\r\n"+string.Join("\r\n",occurrences.ToArray());
            System.Collections.Generic.List<string> attempts;
            if(candidateAttempts.TryGetValue(item.ReferenceText,out attempts))historyDetails.Text+="\r\n\r\nPresentation attempts:\r\n"+string.Join("\r\n",attempts.ToArray());
        }
        private void RecordUnresolvedReference(string book,string source)
        {
            if(suggestionHistory==null) return;
            string key="unresolved:"+listeningSession+":"+source;
            if(historyRows.ContainsKey(key)) return;
            ListViewItem row=new ListViewItem(new string[]{DateTime.Now.ToString("HH:mm:ss"),book+" — unclear","Not available","unresolved","Needs confirmation",source});
            row.Tag=new ScriptureDetection { Book=book,SourceText=source,RequiresReview=true,EvidenceKind="unresolved",
                Reason="The book and chapter cue were heard. Chapter/verse numbers remain unresolved; no correction or presentation was attempted." };
            historyRows[key]=row;suggestionHistory.Items.Insert(0,row);
            RefreshRecentHistory();
        }
        private void BuildHistory(TabPage page)
        {
            TableLayoutPanel content=Vertical(52,56,-65,-35,44);content.Padding=new Padding(28,20,28,20);MountCenteredPage(page,content,540);
            content.Controls.Add(CopyLabel("Every reference, in reach.",23,true),0,0);
            Label note=CopyLabel("All suggestions from this app session. Revisions update the same row.\r\nPercentages are matching scores, not a measured probability of correctness; provisional candidates have no final score.",10,false);
            note.MaximumSize=new Size(850,0);note.Tag="muted";content.Controls.Add(note,0,1);
            suggestionHistory=new ListView();suggestionHistory.View=View.Details;suggestionHistory.FullRowSelect=true;suggestionHistory.MultiSelect=false;
            suggestionHistory.HideSelection=false;suggestionHistory.Dock=DockStyle.Fill;suggestionHistory.AccessibleName="Scripture suggestion history";
            suggestionHistory.Columns.Add("Time",80);suggestionHistory.Columns.Add("Scripture",145);suggestionHistory.Columns.Add("Match score",95);
            suggestionHistory.Columns.Add("Evidence",105);suggestionHistory.Columns.Add("Status",130);suggestionHistory.Columns.Add("Heard",400);
            suggestionHistory.SelectedIndexChanged+=delegate { ShowHistoryDetails(); };content.Controls.Add(suggestionHistory,0,2);
            historyDetails=new TextBox();historyDetails.Multiline=true;historyDetails.ReadOnly=true;historyDetails.ScrollBars=ScrollBars.Vertical;
            historyDetails.Dock=DockStyle.Fill;historyDetails.Text="Select a suggestion to inspect its original transcript and evidence.";content.Controls.Add(historyDetails,0,3);
            historyLoadButton=new GazelyButton();historyLoadButton.Text="Review in workspace";historyLoadButton.Size=new Size(185,36);
            historyPrepareButton=new GazelyButton();historyPrepareButton.Text="Prepare";historyPrepareButton.Size=new Size(110,36);
            historyPresentButton=new GazelyButton();historyPresentButton.Text="Present";historyPresentButton.Size=new Size(110,36);historyPresentButton.Tag="primary";
            historyLoadButton.Enabled=historyPrepareButton.Enabled=historyPresentButton.Enabled=false;
            historyLoadButton.Click+=delegate{if(suggestionHistory.SelectedItems.Count==0)return;SelectHistoryReference(suggestionHistory.SelectedItems[0]);((TabControl)page.Parent).SelectedIndex=1;};
            historyPrepareButton.Click+=delegate{if(suggestionHistory.SelectedItems.Count==0 || !HistoryCanPresent(suggestionHistory.SelectedItems[0]))return;SelectHistoryReference(suggestionHistory.SelectedItems[0]);RunDetected(false);};
            historyPresentButton.Click+=delegate{if(suggestionHistory.SelectedItems.Count==0 || !HistoryCanPresent(suggestionHistory.SelectedItems[0]))return;SelectHistoryReference(suggestionHistory.SelectedItems[0]);RunDetected(true);};
            content.Controls.Add(Row(historyLoadButton,historyPrepareButton,historyPresentButton),0,4);
        }
        private static Label CopyLabel(string text,float size,bool bold)
        {
            Label label=new Label();label.Text=text;label.AutoSize=true;
            label.Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular);
            label.Margin=new Padding(0,0,0,3);return label;
        }
        private static TableLayoutPanel Vertical(params float[] rows)
        {
            TableLayoutPanel layout=new GazelyCard();layout.Dock=DockStyle.Fill;
            layout.ColumnCount=1;layout.RowCount=rows.Length;layout.Margin=new Padding(0);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float height in rows) layout.RowStyles.Add(height<0?new RowStyle(SizeType.Percent,-height):new RowStyle(SizeType.Absolute,height));
            return layout;
        }
        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row=new FlowLayoutPanel();row.Dock=DockStyle.Fill;row.WrapContents=false;row.Margin=new Padding(0);
            foreach(Control control in controls) { control.Anchor=AnchorStyles.None;control.Margin=new Padding(0,0,12,0);row.Controls.Add(control); }
            return row;
        }
        private static void Fill(Control control)
        { control.Anchor=AnchorStyles.None;control.Dock=DockStyle.Fill;control.Margin=new Padding(0); }
        private static void AddField(TableLayoutPanel layout,string caption,Control input,int row)
        {
            Label label=CopyLabel(caption,10,false);label.Margin=new Padding(0,8,12,0);
            layout.Controls.Add(label,0,row);input.Dock=DockStyle.Fill;input.Margin=new Padding(0,4,0,14);
            layout.Controls.Add(input,1,row);
        }
        private void BuildGazelyInterface(TabControl tabs,TabPage controlTab,TabPage audioTab,TabPage mapTab,TabPage integrationsTab)
        {
            SuspendLayout();
            KeyPreview=true;
            KeyDown+=delegate(object sender,KeyEventArgs key)
            {
                if(key.KeyCode!=Keys.Escape) return;
                autoLiveCheck.Checked=false;autoPreviewCheck.Checked=false;StopListening();
                presentationStatus.Text="Stopped. Pending presentation work canceled; verify any output already sent.";
                key.Handled=true;key.SuppressKeyPress=true;
            };
            Font=new Font("Segoe UI",10F);MinimumSize=new Size(980,700);Size=new Size(1280,980);
            tabs.Appearance=TabAppearance.FlatButtons;tabs.ItemSize=new Size(0,1);tabs.SizeMode=TabSizeMode.Fixed;
            tabs.Multiline=false;tabs.Padding=new Point(0,0);
            TabPage setupTab=new TabPage("Speech setup"),diagnosticsTab=new TabPage("Diagnostics"),historyTab=new TabPage("History");
            tabs.TabPages.Add(setupTab);tabs.TabPages.Add(diagnosticsTab);tabs.TabPages.Add(historyTab);BuildHistory(historyTab);

            // Reuse the live controls and their event handlers; change layout, not the recognition path.
            audioTab.Controls.Clear();audioTab.AutoScroll=true;
            TableLayoutPanel workspace=Vertical(48,44,28,-100,32);workspace.Padding=new Padding(24,16,24,12);
            audioTab.Controls.Add(workspace);
            Label heading=CopyLabel("Live workspace",23,true);workspace.Controls.Add(heading,0,0);
            startListeningButton.Text="Start listening";startListeningButton.Width=148;startListeningButton.Height=36;startListeningButton.Tag="primary";
            stopListeningButton.Text="Stop";stopListeningButton.Width=80;stopListeningButton.Height=36;
            audioStatusLabel.Size=new Size(104,32);vadStatusLabel.Size=new Size(126,32);whisperStatusLabel.Size=new Size(100,32);
            workspaceAudioMeter=new AudioLevelMeter();workspaceAudioMeter.Size=new Size(180,32);workspaceAudioMeter.AccessibleName="Live input level";
            Label inputCaption=CopyLabel("Input",9,false);inputCaption.Tag="muted";
            workspace.Controls.Add(Row(startListeningButton,stopListeningButton,audioStatusLabel,inputCaption,workspaceAudioMeter,whisperStatusLabel),0,1);
            audioSourceActiveLabel.AutoSize=false;audioSourceActiveLabel.Width=440;audioSourceActiveLabel.Height=26;audioSourceActiveLabel.Tag="muted";
            latencyLabel.Tag="muted";workspace.Controls.Add(Row(audioSourceActiveLabel,latencyLabel),0,2);
            TableLayoutPanel columns=new TableLayoutPanel();columns.Dock=DockStyle.Fill;columns.Margin=new Padding(0);columns.ColumnCount=2;columns.RowCount=1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
            workspace.Controls.Add(columns,0,3);
            TableLayoutPanel transcript=Vertical(36,44,-100);transcript.Tag="surface";transcript.Padding=new Padding(18);transcript.Margin=new Padding(0,0,12,0);
            columns.Controls.Add(transcript,0,0);
            Label transcriptHeading=CopyLabel("TRANSCRIPT",10,true);clearTranscriptButton.Text="Clear";clearTranscriptButton.Size=new Size(68,28);
            transcript.Controls.Add(Row(transcriptHeading,clearTranscriptButton),0,0);
            Fill(hypothesisLabel);hypothesisLabel.Tag="muted";hypothesisLabel.Text="Live text appears here while you speak.";transcript.Controls.Add(hypothesisLabel,0,1);
            Fill(transcriptTextBox);transcriptTextBox.Font=new Font("Segoe UI",11);transcript.Controls.Add(transcriptTextBox,0,2);
            TableLayoutPanel suggestion=Vertical(24,38,22,24,34,-100,38,30,30,24,32,24,32);suggestion.Tag="surface";suggestion.Padding=new Padding(14);
            Panel scriptureScroll=new Panel();scriptureScroll.Dock=DockStyle.Fill;scriptureScroll.AutoScroll=true;columns.Controls.Add(scriptureScroll,1,0);suggestion.Dock=DockStyle.Top;suggestion.Height=466;scriptureScroll.Controls.Add(suggestion);scriptureScroll.Resize+=delegate{suggestion.Height=Math.Max(466,scriptureScroll.ClientSize.Height);};
            suggestion.Controls.Add(CopyLabel("SCRIPTURE",10,true),0,0);
            Fill(detectedReferenceLabel);detectedReferenceLabel.Font=new Font("Segoe UI",20,FontStyle.Bold);suggestion.Controls.Add(detectedReferenceLabel,0,1);
            Fill(detectedVersionLabel);suggestion.Controls.Add(detectedVersionLabel,0,2);
            Fill(detectedConfidenceLabel);suggestion.Controls.Add(detectedConfidenceLabel,0,3);
            Fill(detectedReasonLabel);detectedReasonLabel.Tag="muted";suggestion.Controls.Add(detectedReasonLabel,0,4);
            Fill(detectedSourceLabel);detectedSourceLabel.Tag="muted";suggestion.Controls.Add(detectedSourceLabel,0,5);
            previewDetectedButton.Text="Prepare";previewDetectedButton.Size=new Size(112,34);
            liveDetectedButton.Text="Present";liveDetectedButton.Size=new Size(112,34);liveDetectedButton.Tag="primary";
            Button evidence=new GazelyButton();evidence.Text="Evidence";evidence.Size=new Size(88,34);
            evidence.Click+=delegate { ShowEvidence(); };
            suggestion.Controls.Add(Row(previewDetectedButton,liveDetectedButton,evidence),0,6);
            Fill(autoPreviewCheck);autoPreviewCheck.Text="Automatically prepare clear references";suggestion.Controls.Add(autoPreviewCheck,0,7);
            Fill(autoLiveCheck);autoLiveCheck.Text="Automatic Live";suggestion.Controls.Add(autoLiveCheck,0,8);
            suggestion.Controls.Add(CopyLabel("Book                         Chapter     Verse / range",9,false),0,9);
            draftBook=new ComboBox();draftBook.DropDownStyle=ComboBoxStyle.DropDown;
            draftBook.Items.AddRange(BibleCanon.BookNames);
            draftBook.AutoCompleteMode=AutoCompleteMode.SuggestAppend;draftBook.AutoCompleteSource=AutoCompleteSource.ListItems;
            draftBook.IntegralHeight=false;draftBook.DropDownHeight=300;draftBook.DropDownWidth=250;
            draftChapter=new TextBox();draftVerse=new TextBox();
            draftBook.AccessibleName="Scripture book";draftChapter.AccessibleName="Chapter";draftVerse.AccessibleName="Verse or verse range";
            TableLayoutPanel draftFields=new TableLayoutPanel();draftFields.Dock=DockStyle.Fill;draftFields.ColumnCount=3;draftFields.RowCount=1;
            draftFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));draftFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));draftFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
            Control[] editors={draftBook,draftChapter,draftVerse};for(int i=0;i<editors.Length;i++){editors[i].Dock=DockStyle.Fill;draftFields.Controls.Add(editors[i],i,0);editors[i].TextChanged+=EditDraft;}
            suggestion.Controls.Add(draftFields,0,10);draftStatus=CopyLabel("Listening for a reference — you can enter one too.",8,false);Fill(draftStatus);suggestion.Controls.Add(draftStatus,0,11);
            Button useDraft=new GazelyButton();useDraft.Text="Use";useDraft.Size=new Size(64,28);useDraft.Click+=delegate{ApplyDraft();};
            Button followSpeech=new GazelyButton();followSpeech.Text="Follow speech";followSpeech.Size=new Size(120,28);followSpeech.Click+=delegate{operatorDraft=false;ClearDetectionUi();draftStatus.Text="Listening for the next reference.";};
            suggestion.Controls.Add(Row(useDraft,followSpeech),0,12);
            System.Windows.Forms.Timer draftTimer=new System.Windows.Forms.Timer();draftTimer.Interval=500;
            draftTimer.Tick+=delegate {
                if(activeReference.IsActive || operatorDraft || lastDetection!=null || waitingSince==DateTime.MinValue || (DateTime.UtcNow-waitingSince).TotalSeconds<20)return;
                SetDraft(string.Empty,0,0,0);UpdateVerseExcerpt(null);detectedReferenceLabel.Text="Ready for a reference";
                detectedConfidenceLabel.Text="";detectedReasonLabel.Text="Say the book, chapter and verse, or enter them below.";
                draftStatus.Text="The previous listening window ended.";
            };
            draftTimer.Start();FormClosed+=delegate{draftTimer.Dispose();};
            workspacePresentationStatus=CopyLabel("No presentation command sent. Live output is not verified.",9,false);
            workspacePresentationStatus.Tag="muted";workspacePresentationStatus.AutoSize=false;Fill(workspacePresentationStatus);
            workspace.Controls.Add(workspacePresentationStatus,0,4);
            presentationStatus.TextChanged+=delegate { workspacePresentationStatus.Text=presentationStatus.Text; };

            setupTab.AutoScroll=true;
            TableLayoutPanel setup=Vertical(48,245,48,40,28,-100);setup.Padding=new Padding(28);setupTab.Controls.Add(setup);
            setup.Controls.Add(CopyLabel("Speech setup",23,true),0,0);
            TableLayoutPanel fields=new TableLayoutPanel();fields.ColumnCount=2;fields.RowCount=5;fields.Dock=DockStyle.Fill;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<5;i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            setup.Controls.Add(fields,0,1);
            AddField(fields,"Audio source",audioDeviceCombo,0);AddField(fields,"Speech engine",speechEngineCombo,1);
            Panel profile=new Panel();profile.Dock=DockStyle.Fill;whisperModelCombo.Dock=DockStyle.Fill;recognizerCombo.Dock=DockStyle.Fill;profile.Controls.Add(whisperModelCombo);profile.Controls.Add(recognizerCombo);
            AddField(fields,"Speed / accuracy",profile,2);AddField(fields,"Voice pickup",vadSensitivityCombo,3);
            earlyResultsCheck.AutoSize=true;fields.Controls.Add(earlyResultsCheck,1,4);
            refreshAudioButton.Text="Refresh audio sources";refreshAudioButton.Size=new Size(190,36);
            setup.Controls.Add(Row(refreshAudioButton,recognizerStatusLabel),0,2);
            audioMeter.Dock=DockStyle.Fill;setup.Controls.Add(audioMeter,0,3);
            setup.Controls.Add(Row(audioDbLabel,signalQualityLabel,confidenceLabel),0,4);
            Label setupNote=CopyLabel("Local speech runs on this PC. Balanced is faster; More accurate uses the installed larger model and takes longer.\r\nChoose the input carrying the preacher's voice, then return to Live workspace to start listening.",10,false);
            setupNote.MaximumSize=new Size(850,0);setupNote.Tag="muted";setup.Controls.Add(setupNote,0,5);

            // Diagnostics leaves the main presentation workspace; logs retain their existing destination.
            TableLayoutPanel diagnostics=Vertical(64,42,-100);diagnostics.Padding=new Padding(28);diagnosticsTab.Controls.Add(diagnostics);
            diagnostics.Controls.Add(CopyLabel("Diagnostics",23,true),0,0);
            Label diagnosticsNote=CopyLabel("Local session events, speech timings and adapter results. Transcripts remain on this PC.",10,false);diagnosticsNote.Tag="muted";
            diagnostics.Controls.Add(diagnosticsNote,0,1);Fill(logTextBox);diagnostics.Controls.Add(logTextBox,0,2);

            // Manual presentation uses the same adapter route as detected content.
            controlTab.Controls.Clear();controlTab.AutoScroll=true;
            TableLayoutPanel manual=Vertical(64,48,180,54,60,-100);manual.Padding=new Padding(28);controlTab.Controls.Add(manual);
            manual.Controls.Add(CopyLabel("Present a scripture",23,true),0,0);
            manual.Controls.Add(connectionLabel,0,1);
            TableLayoutPanel manualFields=new TableLayoutPanel();manualFields.ColumnCount=2;manualFields.RowCount=3;manualFields.Dock=DockStyle.Fill;
            manualFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));manualFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<3;i++) manualFields.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
            AddField(manualFields,"Reference",scriptureTextBox,0);AddField(manualFields,"Bible version",versionCombo,1);
            changeVersionCheck.AutoSize=true;manualFields.Controls.Add(changeVersionCheck,1,2);manual.Controls.Add(manualFields,0,2);
            previewButton.Text="Prepare";previewButton.Tag=null;liveButton.Text="Present";liveButton.Tag="primary";
            manual.Controls.Add(Row(previewButton,liveButton,testVersionButton),0,3);
            manual.Controls.Add(Row(refreshButton,resyncVersionButton),0,4);
            versionStateLabel.AutoSize=true;versionStateLabel.Tag="muted";
            TableLayoutPanel manualStatus=Vertical(36,-100);manual.Controls.Add(manualStatus,0,5);
            manualStatus.Controls.Add(Row(versionStateLabel,versionIndexLabel),0,0);
            Label manualResult=CopyLabel(presentationStatus.Text,11,false);manualResult.AutoSize=false;Fill(manualResult);
            manualStatus.Controls.Add(manualResult,0,1);
            presentationStatus.TextChanged+=delegate { manualResult.Text=presentationStatus.Text; };

            mapTab.Controls.Clear();
            TableLayoutPanel mapping=Vertical(58,58,-100,48);mapping.Padding=new Padding(28);mapTab.Controls.Add(mapping);
            mapping.Controls.Add(CopyLabel("Bible versions",23,true),0,0);
            Label mapHint=CopyLabel("Match the installed Bible order in EasyWorship on this PC. Use one CODE=INDEX per line, starting at zero.\r\nRecheck this map when you add, remove or reorder Bible versions.",10,false);
            mapHint.MaximumSize=new Size(820,0);mapHint.Tag="muted";mapping.Controls.Add(mapHint,0,1);
            Fill(mapEditor);mapping.Controls.Add(mapEditor,0,2);
            saveMapButton.Text="Save version map";saveMapButton.Size=new Size(165,34);
            reloadMapButton.Text="Reload";reloadMapButton.Size=new Size(105,34);
            mapping.Controls.Add(Row(saveMapButton,reloadMapButton),0,3);

            // A small shell replaces the legacy tab strip without adding a browser runtime.
            Controls.Remove(tabs);
            TableLayoutPanel shell=Vertical(68,48,-100);Controls.Add(shell);
            PictureBox symbol=new PictureBox();symbol.Size=new Size(40,40);symbol.SizeMode=PictureBoxSizeMode.Zoom;symbol.AccessibleName="Gazely logo";
            string assets=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
            string logoPath=System.IO.Path.Combine(assets,"gazely.png");
            if(System.IO.File.Exists(logoPath))
            {
                using(System.IO.MemoryStream png=new System.IO.MemoryStream(System.IO.File.ReadAllBytes(logoPath)))
                using(Image original=Image.FromStream(png)) symbol.Image=new Bitmap(original);
            }
            string iconPath=System.IO.Path.Combine(assets,"Gazely.ico");
            if(System.IO.File.Exists(iconPath)) Icon=new Icon(iconPath);
            FormClosed+=delegate { if(symbol.Image!=null) symbol.Image.Dispose(); };
            FlowLayoutPanel masthead=Row(symbol,CopyLabel("Gazely",22,true),CopyLabel("Intelligence for live presentation",10,false));masthead.Padding=new Padding(28,10,0,0);
            shell.Controls.Add(masthead,0,0);
            FlowLayoutPanel navigation=Row();navigation.Name="GazelyNavigation";navigation.WrapContents=true;navigation.Padding=new Padding(28,0,0,0);shell.Controls.Add(navigation,0,1);
            shell.Resize+=delegate{
                shell.RowStyles[1].Height=shell.Width<1120?82:48;
                int inset=28;
                masthead.Padding=new Padding(inset,10,0,0);navigation.Padding=new Padding(inset,0,0,0);
            };
            string[] names={"Live workspace","Present","Speech setup","Integrations","Bible versions","History","Diagnostics"};
            TabPage[] pages={audioTab,controlTab,setupTab,integrationsTab,mapTab,historyTab,diagnosticsTab};
            for(int i=0;i<pages.Length;i++)
            {
                TabPage page=pages[i];Button button=new GazelyButton();button.Text=names[i];button.AutoSize=true;button.Height=34;
                button.Margin=new Padding(0,0,8,0);navigation.Controls.Add(button);
                button.Click+=delegate { tabs.SelectedTab=page;foreach(Control item in navigation.Controls) item.Tag="quiet";button.Tag="selected";GazelyTheme.Apply(this); };
                button.Tag=i==0?"selected":"quiet";
            }
            Button theme=new GazelyButton();theme.Text="Light / dark";theme.AutoSize=true;theme.Height=34;
            theme.Click+=delegate { GazelyTheme.Dark=!GazelyTheme.Dark;GazelyTheme.Apply(this);SaveWorkspacePreferences(); };
            navigation.Controls.Add(theme);
            Button mini=new GazelyButton();mini.Name="OpenMiniOverlay";mini.Text="Mini overlay";mini.AutoSize=true;mini.Height=34;mini.Click+=delegate{OpenMiniOverlay();};navigation.Controls.Add(mini);
            tabs.SelectedIndexChanged+=delegate
            {
                for(int i=0;i<pages.Length;i++) navigation.Controls[i].Tag=tabs.SelectedTab==pages[i]?"selected":"quiet";
                GazelyTheme.Apply(this);
            };
            // Crop only the native border through a resizing parent, never a cached window region.
            Panel tabViewport=new Panel();tabViewport.Dock=DockStyle.Fill;tabViewport.Margin=Padding.Empty;shell.Controls.Add(tabViewport,0,2);
            tabs.Dock=DockStyle.None;tabViewport.Controls.Add(tabs);
            tabViewport.Resize+=delegate{tabs.SetBounds(-4,-5,tabViewport.ClientSize.Width+8,tabViewport.ClientSize.Height+9);};
            tabs.SetBounds(-4,-5,tabViewport.ClientSize.Width+8,tabViewport.ClientSize.Height+9);tabs.SelectedTab=audioTab;
            // DisplayRectangle can lag Resize during maximize; never create a clipping region.
            tabs.Region=null;
            RefineLivePage(audioTab,evidence,useDraft,followSpeech);
            RefineSecondaryPages(controlTab,setupTab,mapTab,integrationsTab,diagnosticsTab);
            GazelyTheme.Apply(this);UpdateEngineVisibility();ResumeLayout(true);
        }
        private void ShowEvidence()
        {
            if(lastDetection==null) return;
            ScriptureDetection detection=lastDetection;
            using(Form dialog=new Form())
            {
                dialog.Text="Scripture evidence";dialog.StartPosition=FormStartPosition.CenterParent;
                dialog.Size=new Size(660,470);dialog.MinimumSize=new Size(440,300);dialog.Font=new Font("Segoe UI",11);
                dialog.Padding=new Padding(24);
                TableLayoutPanel content=Vertical(48,-100,44);dialog.Controls.Add(content);
                content.Controls.Add(CopyLabel(detection.ReferenceText,20,true),0,0);
                TextBox body=new TextBox();body.Multiline=true;body.ReadOnly=true;body.ScrollBars=ScrollBars.Vertical;body.Dock=DockStyle.Fill;
                body.Text="Heard\r\n"+detection.SourceText+"\r\n\r\nHow it matched\r\n"+detection.Reason;
                if(!string.IsNullOrEmpty(detection.MatchedText)) body.Text+="\r\n\r\nMatching KJV text\r\n"+detection.MatchedText;
                body.Text+="\r\n\r\n"+(detection.RequiresReview?"Operator review required. This suggestion will not be presented automatically.":"Explicit reference. Automatic actions depend on your selected mode.");
                content.Controls.Add(body,0,1);
                Button close=new GazelyButton();close.Text="Close";close.DialogResult=DialogResult.Cancel;close.Size=new Size(100,34);
                content.Controls.Add(close,0,2);dialog.CancelButton=close;
                GazelyTheme.Apply(dialog);dialog.ShowDialog(this);
            }
        }
    }
}
