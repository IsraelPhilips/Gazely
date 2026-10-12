using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Windows.Automation;
using System.Windows.Forms;

namespace EasyWorshipControllerMvp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class ScriptureRequest
    {
        public string Book;
        public int Chapter;
        public int Verse;
        public int VerseEnd = 0;
        public string VerseSelection { get { return VerseEnd > Verse ? Verse + "-" + VerseEnd : Verse.ToString(); } }
        public string ReferenceText { get { return Book + " " + Chapter + ":" + VerseSelection; } }
    }

    internal sealed class VersionEntry
    {
        public string Code;
        public int Index;
    }

    internal sealed class BibleVersionPosition
    {
        private readonly object gate=new object();
        private string target="",map="",code="";
        private int index=-1,defaultIndex=-1,revision;
        private bool known,assumed,allowStartup=true;
        public void Configure(int kjvIndex,string signature)
        {
            lock(gate){if(map.Length>0 && map!=signature)Invalidate();map=signature;defaultIndex=kjvIndex;}
        }
        public void Bind(string identity)
        {
            lock(gate){if(target==identity)return;bool restart=target.Length>0;target=identity;revision++;known=false;
                if(restart)allowStartup=true;
                if(allowStartup && defaultIndex>=0){known=true;assumed=true;index=defaultIndex;code="KJV";}
            }
        }
        public void Invalidate()
        {lock(gate){revision++;known=false;assumed=false;index=-1;code="";allowStartup=false;}}
        public bool Matches(string requested,int requestedIndex)
        {lock(gate){return known && index==requestedIndex && string.Equals(code,requested,StringComparison.OrdinalIgnoreCase);}}
        public string Summary
        {get{lock(gate){return known?code+" (index "+index+", "+(assumed?"startup default assumed":"tracked")+")":"unknown";}}}
        public bool Navigate(string requested,int requestedIndex,Action focus,Action<int> key,Action settle)
        {
            if(requestedIndex<0 || requestedIndex>250)throw new ArgumentOutOfRangeException("requestedIndex");
            bool hadPosition;int oldIndex,operation;
            lock(gate){if(Matches(requested,requestedIndex))return false;
                hadPosition=known;oldIndex=index;Invalidate();operation=revision;}
            // Invalid until every navigation step and settling delay completes.
            focus();
            if(!hadPosition){for(int i=0;i<60;i++)key(-1);oldIndex=0;}
            int delta=requestedIndex-oldIndex;
            for(int i=0;i<Math.Abs(delta);i++)key(delta>0?1:-1);
            settle();
            lock(gate){if(revision==operation){known=true;assumed=false;code=requested;index=requestedIndex;}}
            return true;
        }
    }

    internal sealed class EasyWorshipAutomation
    {
        public Action<string> Logger;
        public Func<bool> IsCanceled = null;
        private void CheckCanceled()
        {
            if(IsCanceled!=null && IsCanceled()) throw new OperationCanceledException();
        }
        private void Pause(int milliseconds)
        {
            CheckCanceled();
            for(int remaining=milliseconds;remaining>0;remaining-=25)
            {
                Thread.Sleep(Math.Min(remaining,25));
                CheckCanceled();
            }
        }

        private readonly BibleVersionPosition versionPosition=new BibleVersionPosition();
        public void ConfigureVersionMap(int kjvIndex,string signature){versionPosition.Configure(kjvIndex,signature);}
        private void BindVersionSession(AutomationElement window)
        {
            using(Process process=Process.GetProcessById(window.Current.ProcessId))
                versionPosition.Bind(process.Id+":"+process.StartTime.ToUniversalTime().Ticks);
        }

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int SW_RESTORE = 9;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        // Confirmed on the tested EW installation:
        // enough UP messages anchor the Bible version list at the first row.

        // Conservative timings. We can tune these after stress testing.
        private const int ForegroundDelayMs = 180;
        private const int ClickDelayMs = 180;
        private const int FieldDelayMs = 110;
        private const int VersionUpDelayMs = 8;
        private const int VersionDownDelayMs = 15;

        // Reference selector strategy:
        // Click near the right side (verse area), then Shift+Tab twice to reach Book.
        // This is the one part of the controller that still needs repeated cross-machine validation.
        private const double ReferenceVerseClickRatio = 0.90;
        private const int ShiftTabsToBook = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern void mouse_event(
            uint dwFlags,
            uint dx,
            uint dy,
            uint dwData,
            UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            IntPtr processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(
            uint idAttach,
            uint idAttachTo,
            bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam);

        private void Log(string message)
        {
            if (Logger != null)
                Logger(message);
        }

        public AutomationElement FindMainWindow()
        {
            AutomationElement root = AutomationElement.RootElement;
            PropertyCondition condition = new PropertyCondition(
                AutomationElement.ClassNameProperty,
                "TFormEZWorshipMain");

            foreach(AutomationElement window in root.FindAll(TreeScope.Children,condition))
            {
                try
                {
                    using(Process process=Process.GetProcessById(window.Current.ProcessId))
                    {
                        FileVersionInfo version=process.MainModule.FileVersionInfo;
                        if(version.FileMajorPart==7 && string.Equals(version.ProductName,"EasyWorship",StringComparison.OrdinalIgnoreCase)) return window;
                    }
                }
                catch { /* Unreadable or incompatible targets cannot be declared ready. */ }
            }
            return null;
        }

        public bool IsConnected()
        {
            try
            {
                return FindMainWindow() != null;
            }
            catch
            {
                return false;
            }
        }

        private AutomationElement FindDescendantByClass(
            AutomationElement parent,
            string className)
        {
            if (parent == null)
                return null;

            PropertyCondition condition = new PropertyCondition(
                AutomationElement.ClassNameProperty,
                className);

            return parent.FindFirst(TreeScope.Descendants, condition);
        }

        private AutomationElement FindDescendantByName(
            AutomationElement parent,
            string name)
        {
            if (parent == null)
                return null;

            PropertyCondition condition = new PropertyCondition(
                AutomationElement.NameProperty,
                name);

            return parent.FindFirst(TreeScope.Descendants, condition);
        }

        private void RestoreAndForeground(AutomationElement ew)
        {
            if (ew == null)
                throw new InvalidOperationException("EasyWorship is not running.");

            IntPtr hwnd = new IntPtr(ew.Current.NativeWindowHandle);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("EasyWorship window handle was not available.");

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
                Pause(250);
            }

            SetForegroundWindow(hwnd);
            Pause(ForegroundDelayMs);
        }

        private void ClickElementCenter(AutomationElement element)
        {
            if (element == null)
                throw new InvalidOperationException("EasyWorship UI element was not found.");

            System.Windows.Rect rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("EasyWorship UI element has no usable screen rectangle.");

            int x = (int)(rect.X + (rect.Width / 2.0));
            int y = (int)(rect.Y + (rect.Height / 2.0));

            SetCursorPos(x, y);
            Pause(80);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Pause(ClickDelayMs);
        }

        private void ClickReferenceVerseArea(AutomationElement reference)
        {
            System.Windows.Rect rect = reference.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("Scripture reference control has no usable screen rectangle.");

            int x = (int)(rect.X + (rect.Width * ReferenceVerseClickRatio));
            int y = (int)(rect.Y + (rect.Height / 2.0));

            SetCursorPos(x, y);
            Pause(80);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Pause(ClickDelayMs);
        }

        public void EnsureScripturesView(AutomationElement ew)
        {
            RestoreAndForeground(ew);

            AutomationElement scriptures = FindDescendantByName(ew, "Scriptures");
            if (scriptures == null)
                throw new InvalidOperationException(
                    "Could not find the EasyWorship 'Scriptures' control.");

            ClickElementCenter(scriptures);
            Pause(220);
        }

        private void FocusBibleVersionList(
            AutomationElement ew,
            AutomationElement library)
        {
            if (ew == null || library == null)
                throw new InvalidOperationException(
                    "EasyWorship Bible-version controls are unavailable.");

            IntPtr mainHwnd = new IntPtr(ew.Current.NativeWindowHandle);
            IntPtr libHwnd = new IntPtr(library.Current.NativeWindowHandle);

            if (mainHwnd == IntPtr.Zero || libHwnd == IntPtr.Zero)
                throw new InvalidOperationException(
                    "EasyWorship Bible-version control has no usable native window handle.");

            uint currentThread = GetCurrentThreadId();
            uint targetThread = GetWindowThreadProcessId(libHwnd, IntPtr.Zero);
            bool attached = false;

            try
            {
                if (currentThread != targetThread)
                    attached = AttachThreadInput(
                        currentThread,
                        targetThread,
                        true);

                SetForegroundWindow(mainHwnd);
                Pause(80);

                SetFocus(libHwnd);
                Pause(120);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(
                        currentThread,
                        targetThread,
                        false);
            }
        }

        public void ResetVersionState()
        {
            versionPosition.Invalidate();
            Log("Bible-version position cache cleared. Next version change will re-anchor from the top.");
        }

        public string GetVersionStateSummary()
        {
            return versionPosition.Summary;
        }

        private void PostKey(IntPtr hwnd, int virtualKey, int delayMs)
        {
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Target EasyWorship control has no native window handle.");

            PostMessage(hwnd, WM_KEYDOWN, new IntPtr(virtualKey), IntPtr.Zero);
            PostMessage(hwnd, WM_KEYUP, new IntPtr(virtualKey), IntPtr.Zero);

            if (delayMs > 0)
                Pause(delayMs);
        }

        public void SelectBibleVersionByIndex(
            AutomationElement ew,
            string versionCode,
            int index)
        {
            if (index < 0 || index > 250)
                throw new ArgumentOutOfRangeException(
                    "index",
                    "Bible version index is outside the allowed range.");

            CheckCanceled();
            BindVersionSession(ew);
            if(versionPosition.Matches(versionCode,index))
            {
                Log("Bible version unchanged: "+versionPosition.Summary+". Version list untouched.");
                return;
            }
            AutomationElement library = FindDescendantByClass(
                ew,
                "TsdScriptureLibrary");

            if (library == null)
                throw new InvalidOperationException(
                    "Could not find EasyWorship's Bible version list (TsdScriptureLibrary).");

            IntPtr hwnd = new IntPtr(
                library.Current.NativeWindowHandle);

            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException(
                    "The EasyWorship Bible version list has no native window handle.");

            versionPosition.Navigate(versionCode,index,
                delegate { FocusBibleVersionList(ew,library); },
                delegate(int direction) { PostKey(hwnd,direction>0?VK_DOWN:VK_UP,direction>0?VersionDownDelayMs:VersionUpDelayMs); },
                delegate { Pause(100); });
            Log("Bible version tracked: "+versionPosition.Summary);
        }
        private static string NormalizeWhitespace(string value)
        {
            if (value == null)
                return string.Empty;

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private bool ReferenceLooksCorrect(
            AutomationElement ew,
            ScriptureRequest request,
            out string observed)
        {
            observed = string.Empty;

            try
            {
                AutomationElement currentRef = FindDescendantByClass(ew, "TsdCustomIncEdit");
                if (currentRef == null)
                    return false;

                observed = NormalizeWhitespace(currentRef.Current.Name);
                string expected = NormalizeWhitespace(
                    request.ReferenceText);

                return string.Equals(
                    observed,
                    expected,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string SetScriptureReference(
            AutomationElement ew,
            ScriptureRequest request,
            bool goLive)
        {
            AutomationElement reference = FindDescendantByClass(ew, "TsdCustomIncEdit");
            if (reference == null)
                throw new InvalidOperationException(
                    "Could not find the EasyWorship scripture reference control (TsdCustomIncEdit).");

            RestoreAndForeground(ew);

            // Anchor on the Verse portion geometrically, then walk backward to Book.
            ClickReferenceVerseArea(reference);

            int i;
            for (i = 0; i < ShiftTabsToBook; i++)
            {
                SendKeys.SendWait("+{TAB}");
                Pause(FieldDelayMs);
            }

            SendKeys.SendWait(request.Book);
            Pause(FieldDelayMs);

            SendKeys.SendWait("{TAB}");
            Pause(FieldDelayMs);

            SendKeys.SendWait(request.Chapter.ToString());
            Pause(FieldDelayMs);

            SendKeys.SendWait("{TAB}");
            Pause(FieldDelayMs);

            SendKeys.SendWait(request.VerseSelection);
            Pause(220);

            string observed;
            bool verified = ReferenceLooksCorrect(ew, request, out observed);

            if (!verified)
            {
                string expected = request.ReferenceText;

                throw new InvalidOperationException(
                    "Safety stop: EasyWorship reference could not be verified before going live. " +
                    "Expected '" + expected + "' but UI Automation reported '" + observed + "'. " +
                    "Nothing was sent live.");
            }

            if (goLive)
            {
                CheckCanceled();
                SendKeys.SendWait("{ENTER}");
                Pause(220);
                Log("Enter sent. Live-screen confirmation is unavailable.");
            }
            else
            {
                Log("Preview prepared. Enter was NOT sent.");
            }

            return observed;
        }

        public string ShowScripture(
            ScriptureRequest request,
            bool changeVersion,
            string versionCode,
            int versionIndex,
            bool goLive)
        {
            POINT originalMouse;
            GetCursorPos(out originalMouse);

            try
            {
                AutomationElement ew = FindMainWindow();
                if (ew == null)
                    throw new InvalidOperationException(
                        "EasyWorship is not running or its main window could not be found.");

                Log("EasyWorship found: " + ew.Current.Name);

                EnsureScripturesView(ew);
                Log("Scriptures view activated.");

                if (changeVersion)
                {
                    SelectBibleVersionByIndex(ew, versionCode, versionIndex);
                    Log("Bible version selection completed.");
                }

                string observed = SetScriptureReference(ew, request, goLive);
                Log("Reference verified: " + observed);

                return observed;
            }
            finally
            {
                // Restore operator's mouse position. This does not steal focus back from EasyWorship.
                SetCursorPos(originalMouse.X, originalMouse.Y);
            }
        }

        public void TestVersionOnly(string versionCode, int versionIndex)
        {
            AutomationElement ew = FindMainWindow();
            if (ew == null)
                throw new InvalidOperationException(
                    "EasyWorship is not running or its main window could not be found.");

            EnsureScripturesView(ew);
            SelectBibleVersionByIndex(ew, versionCode, versionIndex);
            Log("Version-only test completed. Nothing was sent live.");
        }
    }


    internal sealed class AudioDeviceInfo
    {
        public int Id;
        public string Name;

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class RecognizerItem
    {
        public string Id;
        public string Description;
        public string Culture;

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Culture))
                return Description;

            return Description + " [" + Culture + "]";
        }
    }

    internal sealed class BlockingAudioStream : Stream
    {
        private readonly Queue<byte[]> queue = new Queue<byte[]>();
        private byte[] current;
        private int currentOffset;
        private bool completed;
        private bool disposed;

        // System.Speech's SetInputToAudioStream is surprisingly picky about Stream.
        // Even for a live/non-seekable source it may query Length/Position/Seek.
        // Therefore these members return harmless sentinel values instead of throwing.
        // Also, Read() tries to satisfy the requested byte count; returning a short read
        // during normal live capture can be interpreted as end-of-stream.
        public void WriteChunk(byte[] chunk)
        {
            if (chunk == null || chunk.Length == 0)
                return;

            lock (queue)
            {
                if (completed || disposed)
                    return;

                // Own our copy because the waveIn callback buffer is reused.
                byte[] copy = new byte[chunk.Length];
                Buffer.BlockCopy(chunk, 0, copy, 0, chunk.Length);

                queue.Enqueue(copy);
                Monitor.PulseAll(queue);
            }
        }

        public void Complete()
        {
            lock (queue)
            {
                completed = true;
                Monitor.PulseAll(queue);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            if (count == 0)
                return 0;

            int total = 0;

            while (total < count)
            {
                if (current != null && currentOffset < current.Length)
                {
                    int available = current.Length - currentOffset;
                    int copy = Math.Min(available, count - total);

                    Buffer.BlockCopy(
                        current,
                        currentOffset,
                        buffer,
                        offset + total,
                        copy);

                    currentOffset += copy;
                    total += copy;

                    if (currentOffset >= current.Length)
                    {
                        current = null;
                        currentOffset = 0;
                    }

                    continue;
                }

                lock (queue)
                {
                    while (queue.Count == 0 && !completed && !disposed)
                        Monitor.Wait(queue, 100);

                    if (queue.Count > 0)
                    {
                        current = queue.Dequeue();
                        currentOffset = 0;
                        continue;
                    }

                    if (completed || disposed)
                        return total;
                }
            }

            return total;
        }

        protected override void Dispose(bool disposing)
        {
            lock (queue)
            {
                disposed = true;
                completed = true;
                queue.Clear();
                Monitor.PulseAll(queue);
            }

            base.Dispose(disposing);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        // Sentinel/no-op members: do NOT throw NotSupportedException.
        public override long Length
        {
            get { return -1L; }
        }

        public override long Position
        {
            get { return 0L; }
            set { }
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0L;
        }

        public override void SetLength(long value)
        {
            // Intentionally ignored for a live stream.
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Use WriteChunk() for live audio input.");
        }
    }

    internal sealed class WaveInRecorder : IDisposable
    {
        public event Action<byte[]> DataAvailable;
        public event Action<double, double> LevelChanged;

        private const uint CALLBACK_FUNCTION = 0x00030000;
        private const uint WIM_DATA = 0x03C0;
        private const ushort WAVE_FORMAT_PCM = 1;
        private const int MMSYSERR_NOERROR = 0;

        private readonly int deviceId;
        private readonly int sampleRate;
        private readonly int channels;
        private readonly int bitsPerSample;

        private IntPtr waveInHandle = IntPtr.Zero;
        private WaveInProc callback;
        private readonly List<BufferBlock> buffers = new List<BufferBlock>();
        private volatile bool running;

        private sealed class BufferBlock
        {
            public IntPtr Data;
            public IntPtr Header;
            public int DataSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;

            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        private delegate void WaveInProc(
            IntPtr hwi,
            uint uMsg,
            IntPtr dwInstance,
            IntPtr dwParam1,
            IntPtr dwParam2);

        [DllImport("winmm.dll")]
        private static extern uint waveInGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int waveInGetDevCaps(
            UIntPtr uDeviceID,
            out WAVEINCAPS pwic,
            uint cbwic);

        [DllImport("winmm.dll")]
        private static extern int waveInOpen(
            out IntPtr phwi,
            uint uDeviceID,
            ref WAVEFORMATEX pwfx,
            WaveInProc dwCallback,
            IntPtr dwInstance,
            uint fdwOpen);

        [DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(
            IntPtr hwi,
            IntPtr pwh,
            uint cbwh);

        [DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(
            IntPtr hwi,
            IntPtr pwh,
            uint cbwh);

        [DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(
            IntPtr hwi,
            IntPtr pwh,
            uint cbwh);

        [DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr hwi);

        [DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr hwi);

        [DllImport("winmm.dll")]
        private static extern int waveInReset(IntPtr hwi);

        [DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr hwi);

        public WaveInRecorder(
            int deviceId,
            int sampleRate,
            int channels)
        {
            this.deviceId = deviceId;
            this.sampleRate = sampleRate;
            this.channels = channels;
            this.bitsPerSample = 16;
        }

        public static List<AudioDeviceInfo> GetDevices()
        {
            List<AudioDeviceInfo> devices = new List<AudioDeviceInfo>();

            uint count = waveInGetNumDevs();

            for (uint i = 0; i < count; i++)
            {
                WAVEINCAPS caps;
                int result = waveInGetDevCaps(
                    new UIntPtr(i),
                    out caps,
                    (uint)Marshal.SizeOf(typeof(WAVEINCAPS)));

                if (result == MMSYSERR_NOERROR)
                {
                    AudioDeviceInfo info = new AudioDeviceInfo();
                    info.Id = (int)i;
                    info.Name = string.IsNullOrWhiteSpace(caps.szPname)
                        ? "Input " + i
                        : caps.szPname.Trim();

                    devices.Add(info);
                }
            }

            return devices;
        }

        public void Start()
        {
            if (running)
                return;

            WAVEFORMATEX format = new WAVEFORMATEX();
            format.wFormatTag = WAVE_FORMAT_PCM;
            format.nChannels = (ushort)channels;
            format.nSamplesPerSec = (uint)sampleRate;
            format.wBitsPerSample = (ushort)bitsPerSample;
            format.nBlockAlign = (ushort)(channels * (bitsPerSample / 8));
            format.nAvgBytesPerSec =
                format.nSamplesPerSec * format.nBlockAlign;
            format.cbSize = 0;

            callback = new WaveInProc(OnWaveIn);

            int result = waveInOpen(
                out waveInHandle,
                (uint)deviceId,
                ref format,
                callback,
                IntPtr.Zero,
                CALLBACK_FUNCTION);

            if (result != MMSYSERR_NOERROR)
            {
                waveInHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    "Could not open the selected audio input. waveInOpen error code: " +
                    result);
            }

            try
            {
                // 40ms buffers give endpoint detection finer time resolution.
                int bufferSize =
                    (sampleRate * channels * (bitsPerSample / 8)) / 25;

                int headerSize = Marshal.SizeOf(typeof(WAVEHDR));

                for (int i = 0; i < 6; i++)
                {
                    BufferBlock block = new BufferBlock();
                    block.DataSize = bufferSize;
                    block.Data = Marshal.AllocHGlobal(bufferSize);
                    block.Header = Marshal.AllocHGlobal(headerSize);

                    WAVEHDR header = new WAVEHDR();
                    header.lpData = block.Data;
                    header.dwBufferLength = (uint)bufferSize;
                    header.dwBytesRecorded = 0;

                    Marshal.StructureToPtr(
                        header,
                        block.Header,
                        false);

                    result = waveInPrepareHeader(
                        waveInHandle,
                        block.Header,
                        (uint)headerSize);

                    if (result != MMSYSERR_NOERROR)
                        throw new InvalidOperationException(
                            "waveInPrepareHeader failed: " + result);

                    result = waveInAddBuffer(
                        waveInHandle,
                        block.Header,
                        (uint)headerSize);

                    if (result != MMSYSERR_NOERROR)
                        throw new InvalidOperationException(
                            "waveInAddBuffer failed: " + result);

                    buffers.Add(block);
                }

                running = true;

                result = waveInStart(waveInHandle);

                if (result != MMSYSERR_NOERROR)
                    throw new InvalidOperationException(
                        "waveInStart failed: " + result);
            }
            catch
            {
                Stop();
                throw;
            }
        }

        private void OnWaveIn(
            IntPtr hwi,
            uint uMsg,
            IntPtr dwInstance,
            IntPtr dwParam1,
            IntPtr dwParam2)
        {
            if (uMsg != WIM_DATA || dwParam1 == IntPtr.Zero)
                return;

            WAVEHDR header =
                (WAVEHDR)Marshal.PtrToStructure(
                    dwParam1,
                    typeof(WAVEHDR));

            if (header.dwBytesRecorded > 0)
            {
                int count = (int)header.dwBytesRecorded;
                byte[] data = new byte[count];

                Marshal.Copy(
                    header.lpData,
                    data,
                    0,
                    count);

                double rmsDb;
                double peakDb;
                CalculateLevels(data, out rmsDb, out peakDb);

                Action<double, double> levelHandler = LevelChanged;
                if (levelHandler != null)
                    levelHandler(rmsDb, peakDb);

                Action<byte[]> dataHandler = DataAvailable;
                if (dataHandler != null)
                    dataHandler(data);
            }

            if (running)
            {
                header.dwBytesRecorded = 0;
                Marshal.StructureToPtr(
                    header,
                    dwParam1,
                    false);

                waveInAddBuffer(
                    waveInHandle,
                    dwParam1,
                    (uint)Marshal.SizeOf(typeof(WAVEHDR)));
            }
        }

        private static void CalculateLevels(
            byte[] data,
            out double rmsDb,
            out double peakDb)
        {
            if (data == null || data.Length < 2)
            {
                rmsDb = -90;
                peakDb = -90;
                return;
            }

            long sampleCount = data.Length / 2;
            double sumSquares = 0.0;
            int peak = 0;

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                short sample = (short)(
                    data[i] |
                    (data[i + 1] << 8));

                int abs = Math.Abs((int)sample);
                if (abs > peak)
                    peak = abs;

                double normalized = sample / 32768.0;
                sumSquares += normalized * normalized;
            }

            double rms = Math.Sqrt(
                sumSquares / Math.Max(1, sampleCount));

            double peakNorm = peak / 32768.0;

            rmsDb = rms > 0.000001
                ? 20.0 * Math.Log10(rms)
                : -90.0;

            peakDb = peakNorm > 0.000001
                ? 20.0 * Math.Log10(peakNorm)
                : -90.0;
        }

        public void Stop()
        {
            bool wasRunning = running;
            running = false;

            if (waveInHandle != IntPtr.Zero)
            {
                try { waveInStop(waveInHandle); }
                catch { }

                try { waveInReset(waveInHandle); }
                catch { }

                int headerSize =
                    Marshal.SizeOf(typeof(WAVEHDR));

                foreach (BufferBlock block in buffers)
                {
                    try
                    {
                        waveInUnprepareHeader(
                            waveInHandle,
                            block.Header,
                            (uint)headerSize);
                    }
                    catch { }

                    if (block.Header != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(block.Header);
                        block.Header = IntPtr.Zero;
                    }

                    if (block.Data != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(block.Data);
                        block.Data = IntPtr.Zero;
                    }
                }

                buffers.Clear();

                try { waveInClose(waveInHandle); }
                catch { }

                waveInHandle = IntPtr.Zero;
            }

            callback = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class LiveSpeechRecognizer : IDisposable
    {
        public event Action<string, double> FinalRecognized;
        public event Action<string> Hypothesized;
        public event Action<string> StateChanged;
        public event Action<string> Error;

        private SpeechRecognitionEngine engine;

        public void Start(
            string recognizerId,
            BlockingAudioStream stream)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(recognizerId))
                throw new InvalidOperationException(
                    "No Windows speech recognizer was selected.");

            engine = new SpeechRecognitionEngine(recognizerId);

            engine.LoadGrammar(new DictationGrammar());

            engine.SpeechRecognized += OnSpeechRecognized;
            engine.SpeechHypothesized += OnSpeechHypothesized;
            engine.SpeechRecognitionRejected += OnRejected;
            engine.AudioStateChanged += OnAudioStateChanged;
            engine.RecognizeCompleted += OnRecognizeCompleted;

            SpeechAudioFormatInfo format =
                new SpeechAudioFormatInfo(
                    16000,
                    AudioBitsPerSample.Sixteen,
                    AudioChannel.Mono);

            engine.SetInputToAudioStream(
                stream,
                format);

            engine.RecognizeAsync(
                RecognizeMode.Multiple);

            RaiseState("Recognizer listening");
        }

        private void OnSpeechRecognized(
            object sender,
            SpeechRecognizedEventArgs e)
        {
            if (e == null || e.Result == null)
                return;

            Action<string, double> handler = FinalRecognized;
            if (handler != null)
            {
                handler(
                    e.Result.Text,
                    e.Result.Confidence);
            }
        }

        private void OnSpeechHypothesized(
            object sender,
            SpeechHypothesizedEventArgs e)
        {
            if (e == null || e.Result == null)
                return;

            Action<string> handler = Hypothesized;
            if (handler != null)
                handler(e.Result.Text);
        }

        private void OnRejected(
            object sender,
            SpeechRecognitionRejectedEventArgs e)
        {
            RaiseState("Speech heard, not confidently recognized");
        }

        private void OnAudioStateChanged(
            object sender,
            AudioStateChangedEventArgs e)
        {
            RaiseState("Recognizer audio: " + e.AudioState);
        }

        private void OnRecognizeCompleted(
            object sender,
            RecognizeCompletedEventArgs e)
        {
            if (e != null && e.Error != null)
            {
                Action<string> errorHandler = Error;
                if (errorHandler != null)
                    errorHandler(e.Error.Message);
            }

            RaiseState("Recognizer stopped");
        }

        private void RaiseState(string state)
        {
            Action<string> handler = StateChanged;
            if (handler != null)
                handler(state);
        }

        public void Stop()
        {
            if (engine == null)
                return;

            SpeechRecognitionEngine old = engine;
            engine = null;

            try { old.RecognizeAsyncCancel(); }
            catch { }

            try { old.SetInputToNull(); }
            catch { }

            try { old.Dispose(); }
            catch { }

            RaiseState("Recognizer stopped");
        }

        public void Dispose()
        {
            Stop();
        }
    }


    internal sealed class ScriptureDetection
    {
        public string Book;
        public int Chapter;
        public int Verse;
        public int VerseEnd = 0;
        public bool RequiresReview = false;
        public string EvidenceKind = "explicit";
        public string MatchedText = string.Empty;
        public string Translation;
        public bool TranslationExplicit;
        public double Confidence;
        public double MatchScore = -1;
        public ScriptureDetection Snapshot() { return (ScriptureDetection)MemberwiseClone(); }
        public string SourceText;
        public string Reason;

        public string ReferenceText
        {
            get
            {
                return string.Format(
                    "{0} {1}:{2}",
                    Book,
                    Chapter,
                    VerseEnd > Verse ? Verse + "-" + VerseEnd : Verse.ToString());
            }
        }

        public string Signature
        {
            get
            {
                return ReferenceText +
                    "|" +
                    (Translation ?? string.Empty);
            }
        }
    }

    internal sealed class LiveTranscriptBuffer
    {
        private string committed=string.Empty, pending=string.Empty, pendingKey=string.Empty, stamp=string.Empty;
        public string Text { get { return committed+pending; } }
        public void Clear() { committed=pending=pendingKey=stamp=string.Empty; }
        public void DiscardPartial() { pending=pendingKey=stamp=string.Empty; }
        public void Update(string key,string text,bool final,string time)
        {
            if(final)
            {
                if(!string.IsNullOrWhiteSpace(text))committed+="["+(key==pendingKey?stamp:time)+"] "+text+Environment.NewLine;
                if(key==pendingKey)DiscardPartial();
            }
            else
            {
                if(key!=pendingKey){pendingKey=key;stamp=time;}
                pending=string.IsNullOrWhiteSpace(text)?string.Empty:"["+stamp+"] "+text;
            }
        }
    }

    internal sealed class ActiveReferenceState
    {
        public ScriptureDetection Confirmed { get; private set; }
        public ScriptureDetection Provisional { get; private set; }
        public ScriptureDetection Retained { get; private set; }
        public ScriptureDetection Context { get { return PreferSpecific(Confirmed,Retained); } }
        public ScriptureDetection Current { get { return PreferSpecific(Context,Provisional); } }
        public bool IsActive { get { return Current!=null; } }
        public string Stage { get { ScriptureDetection d=Current;return d==null?"Searching":d.Chapter==0?"Book identified":d.Verse==0?"Chapter identified":"Reference resolved"; } }
        private static ScriptureDetection PreferSpecific(ScriptureDetection older,ScriptureDetection newer)
        {
            if(newer==null)return older;
            if(older!=null && older.Book==newer.Book && (newer.Chapter==0 ||
                (newer.Chapter==older.Chapter && newer.Verse==0 && older.Verse>0)))return older;
            return newer;
        }
        public void Preview(ScriptureDetection d)
        {
            Provisional=d==null?null:d.Snapshot();
            if(d==null || object.ReferenceEquals(PreferSpecific(Context,d),Context) || (Confirmed!=null && Confirmed.ReferenceText==d.ReferenceText && Retained==null))return;
            Retained=d.Snapshot();Retained.RequiresReview=true;Retained.EvidenceKind="retained";
            Retained.Confidence=Math.Min(Retained.Confidence,0.65);
            Retained.Reason="Earlier speech hypothesis; final words have not confirmed this reference. Review before presenting.";
        }
        public void PreserveSelectedPassage(ScriptureDetection selected,ScriptureDetection incoming)
        {
            // A displayed quote may not yet belong to the spoken anchor. A
            // same-book call must not discard its numbers, or confirm them.
            if(selected==null || incoming==null || incoming.Book!=selected.Book || selected.Verse<=0 ||
                !(incoming.Chapter==0 || (incoming.Chapter==selected.Chapter && incoming.Verse==0)))return;
            ScriptureDetection context=Context;
            if(context!=null && context.Book==selected.Book && context.Verse>0)return;
            Preview(selected);
        }
        public void Confirm(ScriptureDetection d)
        {
            Provisional=null;if(d==null)return;
            Confirmed=PreferSpecific(Confirmed,d).Snapshot();
            if(object.ReferenceEquals(PreferSpecific(Retained,d),d))Retained=null;
        }
        public void Release() { Confirmed=Provisional=Retained=null; }
    }

    internal sealed class ScriptureDetector
    {
        public string LastExplanation = "Waiting for a book, chapter and verse";
        public string UnresolvedBook = string.Empty;
        public string UnresolvedTranslation { get { return lastTranslation; } }
        public bool UnresolvedTranslationExplicit { get { return lastTranslationExplicit; } }
        public int UnresolvedChapter { get { return UnresolvedBook==lastBook?lastChapter:0; } }
        public string UnresolvedSource = string.Empty;
        private DateTime unresolvedUtc = DateTime.MinValue;
        private sealed class BookAlias
        {
            public string Alias;
            public string Canonical;
        }

        private readonly List<BookAlias> books = new List<BookAlias>();
        private readonly Dictionary<string, string> translationAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private DateTime contextUpdatedUtc = DateTime.MinValue;
        private string pendingBook = string.Empty;
        private string pendingReference = string.Empty;
        private string pendingSourceText = string.Empty;
        private const double PendingContinuationSeconds = 15;
        private DateTime pendingUtc = DateTime.MinValue;
        private ScriptureDetection displayedContext;
        private ScriptureDetection correctionContext;
        private bool spokenAfterDisplay;
        private readonly HashSet<string> finalizedEvents = new HashSet<string>();
        private readonly Queue<string> finalizedOrder = new Queue<string>();

        public void SeedActiveReference(ScriptureDetection reference)
        {
            if(reference==null)return;
            lastBook=reference.Book;lastChapter=reference.Chapter;lastVerse=reference.Verse;
            lastVerseEnd=reference.VerseEnd;lastTranslation=reference.Translation;
            lastTranslationExplicit=reference.TranslationExplicit;contextUpdatedUtc=DateTime.UtcNow;
            displayedContext=null;spokenAfterDisplay=true;
            if(reference.Verse>0)correctionContext=reference.Snapshot();
            else
            {
                UnresolvedBook=reference.Book;UnresolvedSource=reference.SourceText??string.Empty;unresolvedUtc=DateTime.UtcNow;
                if(reference.Chapter==0){pendingBook=reference.Book;pendingUtc=DateTime.UtcNow;pendingSourceText=reference.SourceText;}
            }
        }

        public void SetDisplayedContext(ScriptureDetection reference, bool verified)
        {
            Reset();
            if (!verified || reference == null || !BibleCanon.IsValid(reference.Book,reference.Chapter,reference.Verse) ||
                (reference.VerseEnd != 0 && (reference.VerseEnd < reference.Verse || !BibleCanon.IsValid(reference.Book,reference.Chapter,reference.VerseEnd)))) return;
            displayedContext = new ScriptureDetection { Book=reference.Book, Chapter=reference.Chapter,
                Verse=reference.Verse, VerseEnd=reference.VerseEnd, Translation=reference.Translation, TranslationExplicit=reference.TranslationExplicit };
        }

        public ScriptureDetection FeedFinal(string text, string eventId)
        {
            if(string.IsNullOrEmpty(eventId)) return Feed(text);
            if(finalizedEvents.Contains(eventId)) { LastExplanation="Duplicate final transcript ignored"; return null; }
            finalizedEvents.Add(eventId); finalizedOrder.Enqueue(eventId);
            while(finalizedOrder.Count>128) finalizedEvents.Remove(finalizedOrder.Dequeue());
            return Feed(text);
        }

        public void Reset()
        {
            UnresolvedBook=UnresolvedSource=string.Empty;
            unresolvedUtc=DateTime.MinValue;
            lastBook = string.Empty;
            lastChapter = 0;
            lastVerse = 0;
            lastTranslation = string.Empty;
            lastTranslationExplicit=false;lastVerseEnd=0;
            contextUpdatedUtc = DateTime.MinValue;
            pendingBook = string.Empty;
            pendingReference = string.Empty;
            pendingSourceText = string.Empty;
            pendingUtc = DateTime.MinValue;
            displayedContext = null;
            correctionContext = null;
            spokenAfterDisplay = false;
        }

        private string lastBook = string.Empty;
        private int lastChapter = 0;
        private int lastVerse = 0;
        private string lastTranslation = string.Empty;
        private bool lastTranslationExplicit;
        private int lastVerseEnd;

        public ScriptureDetector()
        {
            AddBook("Genesis", "genesis", "gen");
            AddBook("Exodus", "exodus", "exo");
            AddBook("Leviticus", "leviticus", "lev");
            AddBook("Numbers", "numbers", "num");
            AddBook("Deuteronomy", "deuteronomy", "deut");
            AddBook("Joshua", "joshua", "josh");
            AddBook("Judges", "judges", "judg");
            AddBook("Ruth", "ruth");
            AddBook("1 Samuel", "1 samuel", "first samuel", "one samuel", "1st samuel");
            AddBook("2 Samuel", "2 samuel", "second samuel", "two samuel", "2nd samuel");
            AddBook("1 Kings", "1 kings", "first kings", "one kings", "1st kings");
            AddBook("2 Kings", "2 kings", "second kings", "two kings", "2nd kings");
            AddBook("1 Chronicles", "1 chronicles", "first chronicles", "one chronicles", "1st chronicles");
            AddBook("2 Chronicles", "2 chronicles", "second chronicles", "two chronicles", "2nd chronicles");
            AddBook("Ezra", "ezra");
            AddBook("Nehemiah", "nehemiah");
            AddBook("Esther", "esther");
            AddBook("Job", "job");
            AddBook("Psalms", "psalms", "psalm");
            AddBook("Proverbs", "proverbs", "proverb");
            AddBook("Ecclesiastes", "ecclesiastes");
            AddBook("Song of Solomon", "song of solomon", "song of songs");
            AddBook("Isaiah", "isaiah");
            AddBook("Jeremiah", "jeremiah");
            AddBook("Lamentations", "lamentations");
            AddBook("Ezekiel", "ezekiel");
            AddBook("Daniel", "daniel");
            AddBook("Hosea", "hosea");
            AddBook("Joel", "joel");
            AddBook("Amos", "amos");
            AddBook("Obadiah", "obadiah");
            AddBook("Jonah", "jonah");
            AddBook("Micah", "micah");
            AddBook("Nahum", "nahum");
            AddBook("Habakkuk", "habakkuk");
            AddBook("Zephaniah", "zephaniah");
            AddBook("Haggai", "haggai");
            AddBook("Zechariah", "zechariah");
            AddBook("Malachi", "malachi");
            AddBook("Matthew", "matthew", "matt");
            AddBook("Mark", "mark");
            AddBook("Luke", "luke");
            AddBook("John", "john");
            AddBook("Acts", "acts");
            AddBook("Romans", "romans", "roman");
            AddBook("1 Corinthians", "1 corinthians", "first corinthians", "one corinthians", "1st corinthians");
            AddBook("2 Corinthians", "2 corinthians", "second corinthians", "two corinthians", "2nd corinthians");
            AddBook("Galatians", "galatians");
            AddBook("Ephesians", "ephesians");
            AddBook("Philippians", "philippians");
            AddBook("Colossians", "colossians");
            AddBook("1 Thessalonians", "1 thessalonians", "first thessalonians", "one thessalonians", "1st thessalonians");
            AddBook("2 Thessalonians", "2 thessalonians", "second thessalonians", "two thessalonians", "2nd thessalonians");
            AddBook("1 Timothy", "1 timothy", "first timothy", "one timothy", "1st timothy");
            AddBook("2 Timothy", "2 timothy", "second timothy", "two timothy", "2nd timothy");
            AddBook("Titus", "titus");
            AddBook("Philemon", "philemon");
            AddBook("Hebrews", "hebrews");
            AddBook("James", "james");
            AddBook("1 Peter", "1 peter", "first peter", "one peter", "1st peter");
            AddBook("2 Peter", "2 peter", "second peter", "two peter", "2nd peter");
            AddBook("1 John", "1 john", "first john", "one john", "1st john");
            AddBook("2 John", "2 john", "second john", "two john", "2nd john");
            AddBook("3 John", "3 john", "third john", "three john", "3rd john");
            AddBook("Jude", "jude");
            AddBook("Revelation", "revelation", "revelations");

            books.Sort(delegate(BookAlias a, BookAlias b)
            {
                return b.Alias.Length.CompareTo(a.Alias.Length);
            });

            AddTranslation("MSG", "msg", "message", "the message", "message bible");
            AddTranslation("KJV", "kjv", "k j v", "king james", "king james version");
            AddTranslation("NIV", "niv", "n i v", "new international version");
            AddTranslation("NLT", "nlt", "n l t", "new living translation");
            AddTranslation("NKJV", "nkjv", "n k j v", "new king james", "new king james version");
            AddTranslation("ESV", "esv", "e s v", "english standard version");
            AddTranslation("AMP", "amp", "amplified", "amplified bible");
            AddTranslation("AMPC", "ampc", "amplified classic");
            AddTranslation("GNT", "gnt", "good news translation");
            AddTranslation("HCSB", "hcsb", "holman christian standard bible");
            AddTranslation("NASB", "nasb", "new american standard bible");
        }

        private void AddBook(string canonical, params string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                BookAlias item = new BookAlias();
                item.Alias = aliases[i].ToLowerInvariant();
                item.Canonical = canonical;
                books.Add(item);
            }
        }

        private void AddTranslation(string code, params string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
                translationAliases[aliases[i].ToLowerInvariant()] = code;
        }

        public List<ScriptureDetection> FindMentionedReferences(string text)
        {
            List<string> names=new List<string>();foreach(BookAlias book in books)names.Add(book.Alias);
            names.Sort(delegate(string a,string b){return b.Length.CompareTo(a.Length);});
            for(int i=0;i<names.Count;i++)names[i]=Regex.Escape(names[i]);
            MatchCollection mentions=Regex.Matches(text,@"\b(?:"+string.Join("|",names.ToArray())+@")\b",RegexOptions.IgnoreCase);
            List<ScriptureDetection> found=new List<ScriptureDetection>();HashSet<string> seen=new HashSet<string>();
            for(int i=0;i<mentions.Count;i++)
            {
                int end=i+1<mentions.Count?mentions[i+1].Index:text.Length;
                string span=text.Substring(mentions[i].Index,end-mentions[i].Index);
                if(i+1<mentions.Count)span=Regex.Replace(span,@"(?:[\s,;]+(?:and|also|then|or))+[\s,;]*$","",RegexOptions.IgnoreCase);
                ScriptureDetector parser=new ScriptureDetector();ScriptureDetection d=parser.Feed(span) ?? parser.Suggest(span);
                if(d!=null && seen.Add(d.ReferenceText)){d.RequiresReview=true;found.Add(d);}
            }
            return found;
        }

        public ScriptureDetection Feed(string finalTranscript)
        {
            if (string.IsNullOrWhiteSpace(finalTranscript))
                return null;

            string clean = Normalize(finalTranscript);
            if((DateTime.UtcNow-unresolvedUtc).TotalSeconds>20) UnresolvedBook=UnresolvedSource=string.Empty;
            string sourceText = finalTranscript;
            bool joinedPending = false;
            // A verse directive is not a chapter, including a repeated leading
            // number ("11, let's start from verse 11") after a book-only call.
            bool verseDirectiveWithoutChapter = lastChapter==0 &&
                Regex.IsMatch(clean,@"(?:^(?:verse|verses|ves)\s+|\b(?:go to|start from|back to|okay|ok)\s+(?:verse|verses|ves)\s+)") &&
                !Regex.IsMatch(clean,@"\bchapter\s+");
            LastExplanation = "No complete Bible reference in this phrase";

            // A recent named-book request can span conversational filler before
            // a chapter + verse call. Never borrow its book across a new book name.
            if(!string.IsNullOrEmpty(UnresolvedBook) && (DateTime.UtcNow-unresolvedUtc).TotalSeconds<=15 &&
                Regex.IsMatch(UnresolvedSource??string.Empty,@"\b(?:go to|turn to|open|book of)\s+",RegexOptions.IgnoreCase))
            {
                bool namesBook=false;
                foreach(BookAlias alias in books)if(LastWholePhraseIndex(clean,alias.Alias)>=0){namesBook=true;break;}
                Match continuation=Regex.Match(clean,@"\b(?:chapter\s+)?(?:\d{1,3}(?:st|nd|rd|th)?|first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)(?:\s+(?:one|two|three|four|five|six|seven|eight|nine))?\s+verses?\s+.+$");
                if(!namesBook && continuation.Success && !verseDirectiveWithoutChapter)
                {sourceText=UnresolvedSource+Environment.NewLine+finalTranscript;clean=UnresolvedBook+" "+continuation.Value;}
            }

            if(!string.IsNullOrEmpty(pendingReference) && (DateTime.UtcNow-pendingUtc).TotalSeconds<=PendingContinuationSeconds &&
                Regex.IsMatch(clean,@"^(?:verse[s]?\s+)?(?:\d|one\b|two\b|three\b|four\b|five\b|six\b|seven\b|eight\b|nine\b|ten\b|eleven\b|twelve\b|thirteen\b|fourteen\b|fifteen\b|sixteen\b|seventeen\b|eighteen\b|nineteen\b|twenty\b|thirty\b|forty\b|fifty\b|sixty\b|seventy\b|eighty\b|ninety\b)"))
            { clean=pendingReference+" "+clean; joinedPending=true; }
            pendingReference=string.Empty;

            if (!verseDirectiveWithoutChapter && !string.IsNullOrEmpty(pendingBook) &&
                (DateTime.UtcNow - pendingUtc).TotalSeconds <= PendingContinuationSeconds &&
                Regex.IsMatch(clean, @"^(?:chapter\s+)?(?:\d|one\b|two\b|three\b|four\b|five\b|six\b|seven\b|eight\b|nine\b|ten\b|eleven\b|twelve\b|thirteen\b|fourteen\b|fifteen\b|sixteen\b|seventeen\b|eighteen\b|nineteen\b|twenty\b|thirty\b|forty\b|fifty\b|sixty\b|seventy\b|eighty\b|ninety\b)"))
            { clean = pendingBook + " " + clean; joinedPending=true; }
            pendingBook = string.Empty;
            if(joinedPending && !string.IsNullOrEmpty(pendingSourceText)) sourceText=pendingSourceText+Environment.NewLine+finalTranscript;
            pendingSourceText=string.Empty;

            if (displayedContext != null && (!spokenAfterDisplay || (DateTime.UtcNow-contextUpdatedUtc).TotalSeconds>30))
            {
                lastBook=displayedContext.Book; lastChapter=displayedContext.Chapter;
                lastVerse=displayedContext.VerseEnd>0?displayedContext.VerseEnd:displayedContext.Verse;
                lastTranslation=displayedContext.Translation;lastTranslationExplicit=displayedContext.TranslationExplicit;
                lastVerseEnd=displayedContext.VerseEnd;contextUpdatedUtc=DateTime.UtcNow;
                spokenAfterDisplay=false;
            }
            else if (contextUpdatedUtc != DateTime.MinValue && (DateTime.UtcNow - contextUpdatedUtc).TotalSeconds > 30)
                Reset();
            ScriptureDetection detection = TryBookCorrection(clean) ?? Detect(clean);
            if(!string.IsNullOrEmpty(UnresolvedBook) && UnresolvedSource==clean) UnresolvedSource=sourceText;
            if(!string.IsNullOrEmpty(pendingBook) || !string.IsNullOrEmpty(pendingReference)) pendingSourceText=sourceText;
            if(detection==null)
            {
                Match clause=Regex.Match(finalTranscript,@"(?:^|[.!?:]\s+)(?:(?:please|now|and)\s+)?(?:next verse|previous verse|go back one verse)\b.*$",RegexOptions.IgnoreCase);
                if(clause.Success) detection=Detect(Normalize(clause.Value.TrimStart('.', '!', '?', ':', ' ')));
                // Streaming ASR may omit sentence punctuation. A trailing command
                // after a discourse cue can be reviewed without assuming a sentence
                // boundary (or granting automatic presentation permission).
                if(detection==null && !Regex.IsMatch(clean,@"\b(?:not|never|don t|dont)\b"))
                {
                    Match spokenClause=Regex.Match(clean,@"\b(?:okay|ok|now|problem)\s+(next verse|previous verse|go back one verse)\s*$");
                    if(spokenClause.Success)
                    {
                        detection=Detect(spokenClause.Groups[1].Value);
                        if(detection!=null){detection.EvidenceKind="contextual";detection.RequiresReview=true;detection.Reason="Possible verse instruction without a sentence boundary; review before presenting";}
                    }
                }
            }

            if (detection != null)
            {
                UnresolvedBook=UnresolvedSource=string.Empty;
                if (!BibleCanon.IsValid(detection.Book, detection.Chapter, detection.Verse) ||
                    (detection.VerseEnd != 0 && (detection.VerseEnd < detection.Verse ||
                    !BibleCanon.IsValid(detection.Book, detection.Chapter, detection.VerseEnd))))
                {
                    Reset();
                    LastExplanation = "Chapter or verse is outside this book's range";
                    return null;
                }
                LastExplanation = detection.Reason;
                detection.SourceText = sourceText;
                lastBook = detection.Book;
                lastChapter = detection.Chapter;
                lastVerse = detection.Verse; // Range mention starts discussion at its first verse.
                lastVerseEnd=detection.VerseEnd;
                if(detection.EvidenceKind == "explicit") spokenAfterDisplay = true;
                contextUpdatedUtc = DateTime.UtcNow;

                if(detection.EvidenceKind=="explicit" || detection.EvidenceKind=="correction" || detection.EvidenceKind=="navigation")
                    correctionContext=detection.Snapshot();

                lastTranslation = detection.Translation;
                lastTranslationExplicit=detection.TranslationExplicit;
            }

            return detection;
        }

        private ScriptureDetection TryBookCorrection(string text)
        {
            // Only an explicit correction followed solely by a known book (possibly
            // repeated) can inherit numbers. Ordinary book mentions start a new draft.
            Match marker=Regex.Match(text,@"\b(?:no|sorry|correction|i mean|rather)\s+(.+)$");
            if(!marker.Success)return null;
            string replacement=null;
            foreach(BookAlias book in books)
                if(Regex.IsMatch(marker.Groups[1].Value,@"^(?:"+Regex.Escape(book.Alias)+@"\s*)+$"))
                {replacement=book.Canonical;break;}
            if(replacement==null)return null;
            ScriptureDetection anchor=correctionContext;
            string prefix=text.Substring(0,marker.Index).Trim();
            if(prefix.Length>0)anchor=new ScriptureDetector().Feed(prefix);
            else if((DateTime.UtcNow-contextUpdatedUtc).TotalSeconds>30)return null;
            if(anchor==null || !BibleCanon.IsValid(replacement,anchor.Chapter,anchor.Verse) ||
                (anchor.VerseEnd>0 && !BibleCanon.IsValid(replacement,anchor.Chapter,anchor.VerseEnd)))return null;
            ScriptureDetection result=anchor.Snapshot();result.Book=replacement;
            result.EvidenceKind="correction";result.RequiresReview=true;result.Confidence=0.85;
            result.Reason="Book corrected from "+anchor.Book+"; retained the recently called chapter and verse. Confirm before presenting.";
            return result;
        }

        private ScriptureDetection Detect(string text)
        {
            string normalized = Normalize(text);

            BookAlias matchedBook = null;
            int bookIndex = -1;
            // Translation names can contain book names (King James).
            // Mask them without changing offsets used to extract the tail.
            string bookSearch = normalized;
            foreach (string translationAlias in translationAliases.Keys)
            {
                bookSearch = Regex.Replace(bookSearch,
                    @"\b" + Regex.Escape(translationAlias) + @"\b",
                    delegate(Match match) { return new string(' ', match.Length); });
            }

            for (int i = 0; i < books.Count; i++)
            {
                string alias = books[i].Alias;
                int idx = LastWholePhraseIndex(bookSearch, alias);

                // A shorter alias inside an already matched book (John in
                // First John) must not replace that complete book name.
                bool contained = matchedBook != null && idx >= bookIndex &&
                    idx + alias.Length <= bookIndex + matchedBook.Alias.Length;
                if (idx >= 0 && idx >= bookIndex && !contained)
                {
                    matchedBook = books[i];
                    bookIndex = idx;
                }
            }

            string translation;
            bool translationExplicit;
            ParseTranslation(normalized, out translation, out translationExplicit);

            // A translation-only directive changes the active passage, never its
            // numbers. Require a complete directive rather than a sermon mention
            // of "message" or a discussion of Bible versions.
            if(matchedBook==null && lastVerse>0 && BibleCanon.IsValid(lastBook,lastChapter,lastVerse))
            {
                string requested=RequestedTranslation(normalized);
                if(requested!=null)return new ScriptureDetection{Book=lastBook,Chapter=lastChapter,Verse=lastVerse,VerseEnd=lastVerseEnd,
                    Translation=requested,TranslationExplicit=true,RequiresReview=true,Confidence=0.90,
                    EvidenceKind="translation",SourceText=text,Reason="Requested translation for the active passage; review before presenting"};
            }

            if (matchedBook != null)
            {
                int afterBook = bookIndex + matchedBook.Alias.Length;
                string tail = normalized.Substring(afterBook).TrimStart(' ', ':');
                // Preserve numbers already transcribed in "Leviticus is. 6th verse 13".
                // Only a numeric chapter followed by an explicit verse marker qualifies.
                tail=Regex.Replace(tail,@"^is\s+(?=\d{1,3}(?:st|nd|rd|th)?\s+verses?\s+)","");
                tail=Regex.Replace(tail,@"^says\s+(?=(?:chapter\s+)?(?:\d|first\b|second\b|third\b|fourth\b|fifth\b|sixth\b|seventh\b|eighth\b|ninth\b|tenth\b|one\b|two\b|three\b|four\b|five\b|six\b|seven\b|eight\b|nine\b|ten\b))","");
                bool inferredSeparator = false;
                // Whisper sometimes prints spoken 'one one' as '1-1'.
                // Only a single pair directly following the book is eligible.
                Match dashPair = Regex.Match(tail, @"^(\d{1,3})\s*[-–]\s*(\d{1,3})(?=\s|$)");
                if (dashPair.Success && tail.IndexOf(':') < 0)
                {
                    tail = dashPair.Groups[1].Value + ":" + dashPair.Groups[2].Value + tail.Substring(dashPair.Length);
                    inferredSeparator = true;
                }
                Match compact = Regex.Match(tail, @"^(\d{3,6})(?=\s|$)");
                int compactChapter, compactVerse;
                if (compact.Success && BibleCanon.TryCompact(matchedBook.Canonical, compact.Groups[1].Value, out compactChapter, out compactVerse))
                {
                    tail = compactChapter + ":" + compactVerse + tail.Substring(compact.Length);
                    inferredSeparator = true;
                }

                int chapter;
                int verse;
                int verseEnd;
                bool hadChapterWord;
                bool hadVerseWord;
                bool hadColon;

                if (TryParseChapterVerse(
                    tail,
                    out chapter,
                    out verse,
                    out verseEnd,
                    out hadChapterWord,
                    out hadVerseWord,
                    out hadColon))
                {
                    ScriptureDetection d = new ScriptureDetection();
                    d.Book = matchedBook.Canonical;
                    d.Chapter = chapter;
                    d.Verse = verse;
                    d.VerseEnd = verseEnd;
                    d.RequiresReview = verseEnd > verse;
                    d.Translation = translation;
                    d.TranslationExplicit = translationExplicit;
                    d.SourceText = text;
                    Match correction = Regex.Match(tail, @"\b(?:sorry|correction|i mean)\s+verses?\s+(.+)$");
                    if(correction.Success)
                    {
                        int correctedChapter, correctedVerse, correctedEnd;
                        bool cw, vw, co;
                        if(!TryParseChapterVerse(chapter+" verse "+correction.Groups[1].Value,
                            out correctedChapter,out correctedVerse,out correctedEnd,out cw,out vw,out co)) return null;
                        d.Verse=correctedVerse; d.VerseEnd=correctedEnd; d.RequiresReview=true;
                    }

                    if (hadChapterWord && hadVerseWord)
                    {
                        d.Confidence = 0.97;
                        d.Reason = "book + explicit chapter + explicit verse";
                    }
                    else if (hadColon)
                    {
                        d.Confidence = 0.95;
                        d.Reason = "book + chapter:verse";
                    }
                    else
                    {
                        d.Confidence = 0.88;
                        d.Reason = "book + two numeric values";
                    }

                    if (translationExplicit)
                        d.Confidence = Math.Min(0.99, d.Confidence + 0.01);

                    if (inferredSeparator)
                    {
                        d.Confidence = 0.82;
                        d.Reason = "Interpreted number formatting — confirm before showing";
                    }

                    return d;
                }

                // Retain only an explicit, complete chapter-only call, never
                // unrelated numbers elsewhere in a sermon sentence.
                Reset();
                if(string.IsNullOrWhiteSpace(tail) || Regex.IsMatch(tail,@"^chapter\b") ||
                    (Regex.IsMatch(normalized.Substring(0,bookIndex),@"\b(?:go to|turn to|open|book of)\s*$") && !Regex.IsMatch(tail,@"\d")))
                {
                    UnresolvedBook=matchedBook.Canonical;
                    UnresolvedSource=text;
                    unresolvedUtc=DateTime.UtcNow;
                    LastExplanation="Book identified, but chapter or verse is unclear. No number correction has been assumed.";
                }
                if(Regex.IsMatch(tail,@"\b(?:verse|verses|and|to|through)$|-$"))
                {
                    pendingReference=matchedBook.Canonical+" "+tail;
                    pendingUtc=DateTime.UtcNow;
                    LastExplanation="Reference unfinished; waiting for the remaining verse numbers";
                }
                if (string.IsNullOrWhiteSpace(tail) || tail.Trim()=="chapter")
                {
                    pendingBook = matchedBook.Canonical;
                    pendingUtc = DateTime.UtcNow;
                    LastExplanation = "Book heard; waiting for chapter and verse";
                }
                Match chapterOnly = Regex.Match(tail.Trim(), @"^(?:chapter\s+)?(.+)$");
                int pendingChapter, used;
                if (!Regex.IsMatch(tail,@"\b(?:verse|verses|ves)\b") && chapterOnly.Success && TryParseNumberPhrase(
                    chapterOnly.Groups[1].Value, out pendingChapter, out used) &&
                    pendingChapter > 0 && pendingChapter <= BibleCanon.ChapterCount(matchedBook.Canonical))
                {
                    UnresolvedBook=matchedBook.Canonical;UnresolvedSource=text;unresolvedUtc=DateTime.UtcNow;
                    lastBook = matchedBook.Canonical;
                    lastChapter = pendingChapter;
                    lastTranslation = translation;
                    lastTranslationExplicit=translationExplicit;
                    contextUpdatedUtc = DateTime.UtcNow;
                    LastExplanation = "Chapter heard; waiting for the verse";
                }
                return null;
            }

            // Context continuation: "verse seventeen" after a recent book/chapter.
            if (!string.IsNullOrEmpty(lastBook) && lastChapter > 0)
            {
                Match verseMatch = Regex.Match(
                    normalized,
                    @"(?:^(?:(?:and|okay|ok)\s+)?verses?\s+|\b(?:go to|start from|back to|okay|ok)\s+(?:verses?|ves)\s+)(.+)");

                if (verseMatch.Success)
                {
                    int chapter, verse, verseEnd;
                    bool chapterWord, verseWord, colon;
                    if (TryParseChapterVerse(lastChapter + " verse " + verseMatch.Groups[1].Value,
                        out chapter, out verse, out verseEnd, out chapterWord, out verseWord, out colon))
                    {
                        ScriptureDetection d = new ScriptureDetection();
                        d.Book = lastBook;
                        d.Chapter = lastChapter;
                        d.Verse = verse;
                        d.VerseEnd = verseEnd;
                        d.EvidenceKind = "contextual";
                        d.RequiresReview = true;
                        d.Translation =
                            !string.IsNullOrEmpty(translation)
                                ? translation
                                : lastTranslation;
                        d.TranslationExplicit = translationExplicit || lastTranslationExplicit;
                        d.Confidence = 0.80;
                        d.SourceText = text;
                        d.Reason = Regex.IsMatch(normalized,@"\bves\b")?"Possible verse instruction: heard VES within the active chapter; review before presenting":"contextual verse continuation";
                        return d;
                    }
                }

                Match navigation = Regex.Match(normalized,
                    @"^(?:(?:please|now|and)\s+)?(?:(?:go|move)\s+to\s+(?:the\s+)?)?(next verse|previous verse|go back one verse)\b");
                bool bareNext = normalized == "next" && displayedContext != null && !spokenAfterDisplay;
                if ((navigation.Success || bareNext) && lastVerse > 0)
                {
                    string book; int chapter, verse;
                    int direction = bareNext || navigation.Groups[1].Value == "next verse" ? 1 : -1;
                    if (!BibleCanon.TryMove(lastBook,lastChapter,lastVerse,direction,out book,out chapter,out verse))
                    { LastExplanation = "No verse exists beyond this Bible boundary"; return null; }
                    return new ScriptureDetection { Book=book, Chapter=chapter, Verse=verse,
                        Translation=translationExplicit?translation:lastTranslation, TranslationExplicit=translationExplicit || lastTranslationExplicit,
                        Confidence=bareNext?0.65:0.80, SourceText=text,
                        RequiresReview=true, EvidenceKind="navigation",
                        Reason=displayedContext != null && !spokenAfterDisplay ? "Relative instruction from verified displayed scripture; confirm intent" : "Relative verse instruction from recent spoken context; verify the live passage" };
                }
            }

            return null;
        }

        public bool IsTranslationDirective(string text) { return RequestedTranslation(Normalize(text))!=null; }
        private string RequestedTranslation(string text)
        {
            foreach(KeyValuePair<string,string> pair in translationAliases)
            {
                string alias=Regex.Escape(pair.Key);
                string prefix=@"(?:(?:please|now|okay|ok)\s+)?(?:(?:can|could|would)\s+you\s+)?(?:use\s+(?:the\s+)?|(?:give|show)\s+(?:me|us)\s+(?:(?:that|it|this)\s+)?(?:in\s+)?(?:the\s+)?|(?:read|show)\s+(?:that|it|this)\s+in\s+(?:the\s+)?|(?:switch|change)\s+(?:(?:it|that)\s+)?to\s+(?:the\s+)?|in\s+(?:the\s+)?)";
                if(Regex.IsMatch(text,"^"+prefix+alias+@"(?:\s+(?:version|translation))?(?:\s+please)?$"))return pair.Value;
                // Unambiguous code-only calls are also common in live services.
                if(pair.Key==pair.Value.ToLowerInvariant() && text==pair.Key)return pair.Value;
            }
            return null;
        }

        private static string Normalize(string input)
        {
            if (input == null)
                return string.Empty;

            string s = input.ToLowerInvariant();
            s = s.Normalize(NormalizationForm.FormKC);
            s = Regex.Replace(s, @"\p{Cf}", "");
            s = Regex.Replace(s, @"(?<=\d)[：∶](?=\d)", ":");
            s = s.Replace("chapter number", "chapter");
            s = s.Replace("verse number", "verse");
            s = Regex.Replace(s, @"\b(chapter|verse)(?=\d)", "$1 ");
            s = Regex.Replace(s, @"[,\.;!\?\(\)\[\]""']", " ");
            s = Regex.Replace(s, @"\s+", " ");
            return s.Trim();
        }

        private static int LastWholePhraseIndex(
            string text,
            string phrase)
        {
            int idx = text.LastIndexOf(
                phrase,
                StringComparison.OrdinalIgnoreCase);

            while (idx >= 0)
            {
                bool leftOk =
                    idx == 0 ||
                    !char.IsLetterOrDigit(text[idx - 1]);

                int right = idx + phrase.Length;

                bool rightOk =
                    right >= text.Length ||
                    !char.IsLetterOrDigit(text[right]);

                if (leftOk && rightOk)
                    return idx;

                if (idx == 0)
                    break;

                idx = text.LastIndexOf(
                    phrase,
                    idx - 1,
                    StringComparison.OrdinalIgnoreCase);
            }

            return -1;
        }

        private void ParseTranslation(
            string text,
            out string code,
            out bool explicitMention)
        {
            code = string.Empty;
            explicitMention = false;

            string padded = " " + text + " ";

            List<KeyValuePair<string, string>> orderedAliases =
                new List<KeyValuePair<string, string>>(translationAliases);
            orderedAliases.Sort(delegate(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                return b.Key.Length.CompareTo(a.Key.Length);
            });
            foreach (KeyValuePair<string, string> pair in orderedAliases)
            {
                string alias = pair.Key;
                string pattern =
                    @"\b(in|from|using|use|version|translation)\s+(the\s+)?"
                    + Regex.Escape(alias)
                    + @"\b|\b"
                    + Regex.Escape(alias)
                    + @"\s+(translation|version)\b";

                if (Regex.IsMatch(padded, pattern, RegexOptions.IgnoreCase))
                {
                    code = pair.Value;
                    explicitMention = true;
                    return;
                }
            }

            // Exact short code token is also explicit.
            foreach (KeyValuePair<string, string> pair in translationAliases)
            {
                string alias = pair.Key;

                if (alias.Length <= 5 &&
                    Regex.IsMatch(
                        padded,
                        @"\b" + Regex.Escape(alias) + @"\b",
                        RegexOptions.IgnoreCase))
                {
                    code = pair.Value;
                    explicitMention = true;
                    return;
                }
            }
        }

        private static bool TryParseChapterVerse(
            string tail, out int chapter, out int verse, out int verseEnd,
            out bool hadChapterWord, out bool hadVerseWord, out bool hadColon)
        {
            chapter = verse = verseEnd = 0;
            string working = tail.Trim();
            hadChapterWord = Regex.IsMatch(working, @"^chapter\b");
            hadVerseWord = Regex.IsMatch(working, @"\bverses?\b");
            hadColon = working.IndexOf(':') >= 0;
            working = Regex.Replace(working, @"^chapter\s+", "");
            // Ordinals are interpreted only in the chapter slot before an explicit
            // verse cue, not as substitutions in ordinary transcript words.
            working = Regex.Replace(working,@"^(\d{1,3})(?:st|nd|rd|th)(?=\s+verses?\b)","$1");
            string[] ordinalChapters={"first","second","third","fourth","fifth","sixth","seventh","eighth","ninth","tenth"};
            for(int ordinal=0;ordinal<ordinalChapters.Length;ordinal++)
                working=Regex.Replace(working,"^"+ordinalChapters[ordinal]+@"(?=\s+verses?\b)",(ordinal+1).ToString());
            working = Regex.Replace(working, @"(?<=[a-z])-(?=[a-z])", " ");
            working = Regex.Replace(working, @"(?<=\d)[-–](?=\d)", " - ");
            working = working.Replace(":", " : ");
            string[] tokens = Regex.Split(working.Trim(), @"\s+");
            int used;
            if (!TryParseNumberTokens(tokens, 0, out chapter, out used) || chapter <= 0)
                return false;
            int position = used;
            if (position < tokens.Length && (tokens[position] == ":" || tokens[position] == "verse" || tokens[position] == "verses"))
                position++;
            if (!TryParseNumberTokens(tokens, position, out verse, out used) || verse <= 0)
                return false;
            position += used;
            if (position < tokens.Length &&
                (tokens[position] == "to" || tokens[position] == "through" || tokens[position] == "-" || tokens[position] == "and"))
            {
                string connector = tokens[position];
                int endPosition = position + 1;
                if(endPosition >= tokens.Length) return false;
                if (endPosition < tokens.Length && (tokens[endPosition] == "verse" || tokens[endPosition] == "verses")) endPosition++;
                int rangeEnd, rangeUsed;
                if (TryParseNumberTokens(tokens, endPosition, out rangeEnd, out rangeUsed))
                {
                    if (rangeEnd < verse || (connector == "and" && rangeEnd != verse + 1)) return false;
                    verseEnd = rangeEnd;
                    position = endPosition + rangeUsed;
                    if (position < tokens.Length && (tokens[position] == ":" || tokens[position] == "-" ||
                        tokens[position] == "to" || tokens[position] == "through")) return false;
                    int another, anotherUsed;
                    if (position < tokens.Length && tokens[position] == "and")
                    {
                        int additional=position+1;
                        if(additional>=tokens.Length || tokens[additional]=="verse" || tokens[additional]=="verses" ||
                            TryParseNumberTokens(tokens,additional,out another,out anotherUsed)) return false;
                    }
                }
                else if (connector == "-" || endPosition != position+1) return false;
            }
            int extra, extraUsed;
            if (TryParseNumberTokens(tokens, position, out extra, out extraUsed))
                return false;
            return true;
        }
        // Preview parsing must never advance or erase finalized context.
        public ScriptureDetection Preview(string text,bool includeIncomplete=false)
        {
            string book = lastBook, translation = lastTranslation, pending = pendingBook, explanation = LastExplanation, unfinished=pendingReference, pendingSource=pendingSourceText;
            string unclearBook=UnresolvedBook,unclearSource=UnresolvedSource;
            DateTime unclearTime=unresolvedUtc;
            int chapter = lastChapter, verse = lastVerse, verseEnd=lastVerseEnd;
            bool explicitTranslation=lastTranslationExplicit;
            ScriptureDetection displayed = displayedContext;
            ScriptureDetection correctionAnchor = correctionContext;
            bool spoke = spokenAfterDisplay;
            DateTime contextTime = contextUpdatedUtc, pendingTime = pendingUtc;
            try {
                ScriptureDetection result=Feed(text);
                if(result==null && includeIncomplete && !string.IsNullOrEmpty(UnresolvedBook) && (UnresolvedSource==text || UnresolvedSource.EndsWith(Environment.NewLine+text)))
                    result=new ScriptureDetection{Book=UnresolvedBook,Chapter=UnresolvedChapter,Verse=0,Translation=UnresolvedTranslation,TranslationExplicit=UnresolvedTranslationExplicit,SourceText=text,RequiresReview=true,EvidenceKind="draft"};
                return result;
            }
            finally
            {
                lastBook = book; lastChapter = chapter; lastVerse = verse; lastTranslation = translation;
                lastVerseEnd=verseEnd;lastTranslationExplicit=explicitTranslation;
                pendingBook = pending; contextUpdatedUtc = contextTime; pendingUtc = pendingTime;
                pendingReference = unfinished;
                pendingSourceText = pendingSource;
                UnresolvedBook=unclearBook;UnresolvedSource=unclearSource;unresolvedUtc=unclearTime;
                LastExplanation = explanation;
                displayedContext = displayed; spokenAfterDisplay = spoke;
                correctionContext = correctionAnchor;
            }
        }
        public ScriptureDetection Suggest(string text)
        {
            // Keep literal transcription intact. These are review-only
            // candidates for narrow phonetic confusions, never substitutions
            // eligible for automatic live or automatic preview.
            string normalized = Normalize(text);
            string[] heard = { "look", "tight us", "titles" };
            string[] books = { "Luke", "Titus", "Titus" };
            for (int i = 0; i < heard.Length; i++)
            {
                Match match = Regex.Match(normalized,
                    @"^(?:(?:turn|open) to (?:the book of )?)?" + Regex.Escape(heard[i]) + @"\s+(.+)$");
                if (!match.Success) continue;
                ScriptureDetection candidate = new ScriptureDetector().Feed(books[i] + " " + match.Groups[1].Value);
                if (candidate == null) continue;
                candidate.Confidence = 0.65;
                candidate.SourceText = text;
                candidate.Reason = "Possible " + books[i] + " — confirm the book manually";
                return candidate;
            }
            return null;
        }
        private static bool TryParseNumberPhrase(
            string phrase,
            out int value,
            out int consumed)
        {
            string normalized = Normalize(phrase);
            string[] tokens = normalized.Split(' ');
            return TryParseNumberTokens(
                tokens,
                0,
                out value,
                out consumed);
        }

        private static bool TryParseNumberTokens(
            string[] tokens, int start, out int value, out int consumed)
        {
            value = consumed = 0;
            if (tokens == null || start < 0 || start >= tokens.Length)
                return false;
            if (int.TryParse(tokens[start], out value))
            {
                consumed = 1;
                return value >= 0;
            }
            int first;
            if (!TrySmallNumber(tokens[start], out first))
                return false;
            value = first;
            consumed = 1;
            int position = start + 1;
            if (first >= 1 && first <= 9 && position < tokens.Length && tokens[position] == "hundred")
            {
                value *= 100;
                consumed++;
                position++;
                if (position < tokens.Length && tokens[position] == "and")
                {
                    consumed++;
                    position++;
                }
                int remainder;
                if (position < tokens.Length && TrySmallNumber(tokens[position], out remainder))
                {
                    value += remainder;
                    consumed++;
                    position++;
                    first = remainder;
                }
                else return true;
            }
            int unit;
            if (first >= 20 && first % 10 == 0 && position < tokens.Length &&
                TrySmallNumber(tokens[position], out unit) && unit > 0 && unit < 10)
            {
                value += unit;
                consumed++;
            }
            return true;
        }
        private static bool TrySmallNumber(
            string token,
            out int value)
        {
            value = 0;

            switch (token)
            {
                case "zero": case "oh": value = 0; return true;
                case "one": case "first": value = 1; return true;
                case "two": case "second": value = 2; return true;
                case "three": case "third": value = 3; return true;
                case "four": value = 4; return true;
                case "five": value = 5; return true;
                case "six": value = 6; return true;
                case "seven": value = 7; return true;
                case "eight": value = 8; return true;
                case "nine": value = 9; return true;
                case "ten": value = 10; return true;
                case "eleven": value = 11; return true;
                case "twelve": value = 12; return true;
                case "thirteen": value = 13; return true;
                case "fourteen": value = 14; return true;
                case "fifteen": value = 15; return true;
                case "sixteen": value = 16; return true;
                case "seventeen": value = 17; return true;
                case "eighteen": value = 18; return true;
                case "nineteen": value = 19; return true;
                case "twenty": value = 20; return true;
                case "thirty": value = 30; return true;
                case "forty": value = 40; return true;
                case "fifty": value = 50; return true;
                case "sixty": value = 60; return true;
                case "seventy": value = 70; return true;
                case "eighty": value = 80; return true;
                case "ninety": value = 90; return true;
                default: return false;
            }
        }
    }

    internal static class BibleCanon
    {
        // Chapter/verse counts derived from thiagobodruk/bible en_kjv.json.
        // Standard Protestant/KJV numbering; no scripture text is embedded.
        private static readonly Dictionary<string, int[]> Counts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Genesis", new int[] { 31,25,24,26,32,22,24,22,29,32,32,20,18,24,21,16,27,33,38,18,34,24,20,67,34,35,46,22,35,43,55,32,20,31,29,43,36,30,23,23,57,38,34,34,28,34,31,22,33,26 } },
            { "Exodus", new int[] { 22,25,22,31,23,30,25,32,35,29,10,51,22,31,27,36,16,27,25,26,36,31,33,18,40,37,21,43,46,38,18,35,23,35,35,38,29,31,43,38 } },
            { "Leviticus", new int[] { 17,16,17,35,19,30,38,36,24,20,47,8,59,57,33,34,16,30,37,27,24,33,44,23,55,46,34 } },
            { "Numbers", new int[] { 54,34,51,49,31,27,89,26,23,36,35,16,33,45,41,50,13,32,22,29,35,41,30,25,18,65,23,31,40,16,54,42,56,29,34,13 } },
            { "Deuteronomy", new int[] { 46,37,29,49,33,25,26,20,29,22,32,32,18,29,23,22,20,22,21,20,23,30,25,22,19,19,26,68,29,20,30,52,29,12 } },
            { "Joshua", new int[] { 18,24,17,24,15,27,26,35,27,43,23,24,33,15,63,10,18,28,51,9,45,34,16,33 } },
            { "Judges", new int[] { 36,23,31,24,31,40,25,35,57,18,40,15,25,20,20,31,13,31,30,48,25 } },
            { "Ruth", new int[] { 22,23,18,22 } },
            { "1 Samuel", new int[] { 28,36,21,22,12,21,17,22,27,27,15,25,23,52,35,23,58,30,24,42,15,23,29,22,44,25,12,25,11,31,13 } },
            { "2 Samuel", new int[] { 27,32,39,12,25,23,29,18,13,19,27,31,39,33,37,23,29,33,43,26,22,51,39,25 } },
            { "1 Kings", new int[] { 53,46,28,34,18,38,51,66,28,29,43,33,34,31,34,34,24,46,21,43,29,53 } },
            { "2 Kings", new int[] { 18,25,27,44,27,33,20,29,37,36,21,21,25,29,38,20,41,37,37,21,26,20,37,20,30 } },
            { "1 Chronicles", new int[] { 54,55,24,43,26,81,40,40,44,14,47,40,14,17,29,43,27,17,19,8,30,19,32,31,31,32,34,21,30 } },
            { "2 Chronicles", new int[] { 17,18,17,22,14,42,22,18,31,19,23,16,22,15,19,14,19,34,11,37,20,12,21,27,28,23,9,27,36,27,21,33,25,33,27,23 } },
            { "Ezra", new int[] { 11,70,13,24,17,22,28,36,15,44 } },
            { "Nehemiah", new int[] { 11,20,32,23,19,19,73,18,38,39,36,47,31 } },
            { "Esther", new int[] { 22,23,15,17,14,14,10,17,32,3 } },
            { "Job", new int[] { 22,13,26,21,27,30,21,22,35,22,20,25,28,22,35,22,16,21,29,29,34,30,17,25,6,14,23,28,25,31,40,22,33,37,16,33,24,41,30,24,34,17 } },
            { "Psalms", new int[] { 6,12,8,8,12,10,17,9,20,18,7,8,6,7,5,11,15,50,14,9,13,31,6,10,22,12,14,9,11,12,24,11,22,22,28,12,40,22,13,17,13,11,5,26,17,11,9,14,20,23,19,9,6,7,23,13,11,11,17,12,8,12,11,10,13,20,7,35,36,5,24,20,28,23,10,12,20,72,13,19,16,8,18,12,13,17,7,18,52,17,16,15,5,23,11,13,12,9,9,5,8,28,22,35,45,48,43,13,31,7,10,10,9,8,18,19,2,29,176,7,8,9,4,8,5,6,5,6,8,8,3,18,3,3,21,26,9,8,24,13,10,7,12,15,21,10,20,14,9,6 } },
            { "Proverbs", new int[] { 33,22,35,27,23,35,27,36,18,32,31,28,25,35,33,33,28,24,29,30,31,29,35,34,28,28,27,28,27,33,31 } },
            { "Ecclesiastes", new int[] { 18,26,22,16,20,12,29,17,18,20,10,14 } },
            { "Song of Solomon", new int[] { 17,17,11,16,16,13,13,14 } },
            { "Isaiah", new int[] { 31,22,26,6,30,13,25,22,21,34,16,6,22,32,9,14,14,7,25,6,17,25,18,23,12,21,13,29,24,33,9,20,24,17,10,22,38,22,8,31,29,25,28,28,25,13,15,22,26,11,23,15,12,17,13,12,21,14,21,22,11,12,19,12,25,24 } },
            { "Jeremiah", new int[] { 19,37,25,31,31,30,34,22,26,25,23,17,27,22,21,21,27,23,15,18,14,30,40,10,38,24,22,17,32,24,40,44,26,22,19,32,21,28,18,16,18,22,13,30,5,28,7,47,39,46,64,34 } },
            { "Lamentations", new int[] { 22,22,66,22,22 } },
            { "Ezekiel", new int[] { 28,10,27,17,17,14,27,18,11,22,25,28,23,23,8,63,24,32,14,49,32,31,49,27,17,21,36,26,21,26,18,32,33,31,15,38,28,23,29,49,26,20,27,31,25,24,23,35 } },
            { "Daniel", new int[] { 21,49,30,37,31,28,28,27,27,21,45,13 } },
            { "Hosea", new int[] { 11,23,5,19,15,11,16,14,17,15,12,14,16,9 } },
            { "Joel", new int[] { 20,32,21 } },
            { "Amos", new int[] { 15,16,15,13,27,14,17,14,15 } },
            { "Obadiah", new int[] { 21 } },
            { "Jonah", new int[] { 17,10,10,11 } },
            { "Micah", new int[] { 16,13,12,13,15,16,20 } },
            { "Nahum", new int[] { 15,13,19 } },
            { "Habakkuk", new int[] { 17,20,19 } },
            { "Zephaniah", new int[] { 18,15,20 } },
            { "Haggai", new int[] { 15,23 } },
            { "Zechariah", new int[] { 21,13,10,14,11,15,14,23,17,12,17,14,9,21 } },
            { "Malachi", new int[] { 14,17,18,6 } },
            { "Matthew", new int[] { 25,23,17,25,48,34,29,34,38,42,30,50,58,36,39,28,27,35,30,34,46,46,39,51,46,75,66,20 } },
            { "Mark", new int[] { 45,28,35,41,43,56,37,38,50,52,33,44,37,72,47,20 } },
            { "Luke", new int[] { 80,52,38,44,39,49,50,56,62,42,54,59,35,35,32,31,37,43,48,47,38,71,56,53 } },
            { "John", new int[] { 51,25,36,54,47,71,53,59,41,42,57,50,38,31,27,33,26,40,42,31,25 } },
            { "Acts", new int[] { 26,47,26,37,42,15,60,40,43,48,30,25,52,28,41,40,34,28,41,38,40,30,35,27,27,32,44,31 } },
            { "Romans", new int[] { 32,29,31,25,21,23,25,39,33,21,36,21,14,23,33,27 } },
            { "1 Corinthians", new int[] { 31,16,23,21,13,20,40,13,27,33,34,31,13,40,58,24 } },
            { "2 Corinthians", new int[] { 24,17,18,18,21,18,16,24,15,18,33,21,14 } },
            { "Galatians", new int[] { 24,21,29,31,26,18 } },
            { "Ephesians", new int[] { 23,22,21,32,33,24 } },
            { "Philippians", new int[] { 30,30,21,23 } },
            { "Colossians", new int[] { 29,23,25,18 } },
            { "1 Thessalonians", new int[] { 10,20,13,18,28 } },
            { "2 Thessalonians", new int[] { 12,17,18 } },
            { "1 Timothy", new int[] { 20,15,16,16,25,21 } },
            { "2 Timothy", new int[] { 18,26,17,22 } },
            { "Titus", new int[] { 16,15,15 } },
            { "Philemon", new int[] { 25 } },
            { "Hebrews", new int[] { 14,18,19,16,14,20,28,13,28,39,40,29,25 } },
            { "James", new int[] { 27,26,18,17,20 } },
            { "1 Peter", new int[] { 25,25,22,19,14 } },
            { "2 Peter", new int[] { 21,22,18 } },
            { "1 John", new int[] { 10,29,24,21,21 } },
            { "2 John", new int[] { 13 } },
            { "3 John", new int[] { 14 } },
            { "Jude", new int[] { 25 } },
            { "Revelation", new int[] { 20,29,22,11,14,17,17,13,21,11,19,17,18,20,8,21,18,24,21,15,27,21 } },
        };
        public static int ChapterCount(string book)
        {
            int[] counts;
            return Counts.TryGetValue(book, out counts) ? counts.Length : 0;
        }
        public static string[] BookNames { get { return (string[])BookOrder.Clone(); } }
        public static bool IsValid(string book, int chapter, int verse)
        {
            int[] counts;
            return Counts.TryGetValue(book, out counts) && chapter > 0 && chapter <= counts.Length &&
                verse > 0 && verse <= counts[chapter - 1];
        }
        private static readonly string[] BookOrder = ("Genesis|Exodus|Leviticus|Numbers|Deuteronomy|Joshua|Judges|Ruth|1 Samuel|2 Samuel|1 Kings|2 Kings|1 Chronicles|2 Chronicles|Ezra|Nehemiah|Esther|Job|Psalms|Proverbs|Ecclesiastes|Song of Solomon|Isaiah|Jeremiah|Lamentations|Ezekiel|Daniel|Hosea|Joel|Amos|Obadiah|Jonah|Micah|Nahum|Habakkuk|Zephaniah|Haggai|Zechariah|Malachi|Matthew|Mark|Luke|John|Acts|Romans|1 Corinthians|2 Corinthians|Galatians|Ephesians|Philippians|Colossians|1 Thessalonians|2 Thessalonians|1 Timothy|2 Timothy|Titus|Philemon|Hebrews|James|1 Peter|2 Peter|1 John|2 John|3 John|Jude|Revelation").Split('|');
        public static bool TryMove(string book, int chapter, int verse, int direction,
            out string nextBook, out int nextChapter, out int nextVerse)
        {
            nextBook=book; nextChapter=chapter; nextVerse=verse;
            if(!IsValid(book,chapter,verse) || (direction!=1 && direction!=-1)) return false;
            int index=Array.IndexOf(BookOrder,book);
            if(index<0) return false;
            nextVerse+=direction;
            if(nextVerse>Counts[book][chapter-1]) {
                nextVerse=1; nextChapter++;
                if(nextChapter>ChapterCount(book)) {
                    if(index+1>=BookOrder.Length) return false;
                    nextBook=BookOrder[index+1]; nextChapter=1;
                }
            } else if(nextVerse<1) {
                nextChapter--;
                if(nextChapter<1) {
                    if(index==0) return false;
                    nextBook=BookOrder[index-1]; nextChapter=ChapterCount(nextBook);
                }
                nextVerse=Counts[nextBook][nextChapter-1];
            }
            return true;
        }
        public static bool TryCompact(string book, string digits, out int chapter, out int verse)
        {
            chapter = verse = 0;
            int number;
            if (!int.TryParse(digits, out number) || number <= ChapterCount(book)) return false;
            int matches = 0;
            for (int split = 1; split < digits.Length; split++)
            {
                if (digits[split] == '0') continue;
                int c, v;
                if (int.TryParse(digits.Substring(0, split), out c) && int.TryParse(digits.Substring(split), out v) && IsValid(book, c, v))
                { chapter = c; verse = v; matches++; }
            }
            return matches == 1;
        }
    }
    internal sealed class SpeechPresentationPolicy
    {
        public static ScriptureDetection Resolve(ScriptureDetection input,ScriptureDetection previous,bool recent)
        {
            ScriptureDetection d=input.Snapshot();
            if((d.EvidenceKind=="explicit" || d.EvidenceKind=="contextual") && d.VerseEnd>d.Verse)
            {
                string range=d.ReferenceText;d.VerseEnd=0;
                // Corrections and number-recovery guesses still require review.
                if(d.EvidenceKind=="explicit" && d.Confidence>=0.88 && !Regex.IsMatch(d.SourceText??"",@"\b(sorry|correction|i mean)\b",RegexOptions.IgnoreCase))d.RequiresReview=false;
                d.Reason+="; announced "+range+"; start at the first verse";
            }
            if(d.EvidenceKind=="navigation" && recent && previous!=null && !previous.RequiresReview && previous.Confidence>=0.88 &&
                Regex.IsMatch(d.SourceText??"",@"\b(next verse|previous verse|go back one verse)\b",RegexOptions.IgnoreCase))
            {
                int direction=Regex.IsMatch(d.SourceText,@"\bnext verse\b",RegexOptions.IgnoreCase)?1:-1;
                string book;int chapter,verse;
                if(BibleCanon.TryMove(previous.Book,previous.Chapter,previous.Verse,direction,out book,out chapter,out verse) &&
                    d.Book==book && d.Chapter==chapter && d.Verse==verse)
                {d.RequiresReview=false;d.Confidence=0.90;if(!d.TranslationExplicit){d.Translation=previous.Translation;d.TranslationExplicit=previous.TranslationExplicit;}d.Reason="Clear verse instruction following the recent unambiguous reference";}
            }
            return d;
        }
        public static bool CanAutomate(ScriptureDetection d)
        {return d!=null && !d.RequiresReview && d.Confidence>=0.88 && (d.EvidenceKind=="explicit" || d.EvidenceKind=="navigation");}
    }
    internal sealed class ScriptureQuoteIndex
    {
        private sealed class Entry
        {
            public string Book, Text;
            public int Chapter, Verse;
            public string[] Words;
            public HashSet<string> Set;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string,List<int>> postings = new Dictionary<string,List<int>>();
        private static readonly HashSet<string> stop = new HashSet<string>(("a an the and or but of to in on at for from with by as is are was were be been being it its this that these those i me my mine we us our ours you your yours he him his she her they them their all not no so if then than there here who whom whose which what when where why how can could would should shall shalt will may might must do does did have has had having unto ye thou thee thy thine let said say says verse verses next previous chapter please now also other very just into out about again upon am indeed").Split(' '));
        public int Count { get { return entries.Count; } }
        public string GetVerseText(string book,int chapter,int verse,int end)
        {
            StringBuilder text=new StringBuilder();int last=end>verse?end:verse;
            foreach(Entry entry in entries)
            {
                if(entry.Book!=book || entry.Chapter!=chapter || entry.Verse<verse || entry.Verse>last)continue;
                if(text.Length>0)text.Append(" ");text.Append(entry.Text);
                if(text.Length>700)break;
            }
            return text.ToString();
        }
        private static string[] Tokens(string text)
        {
            List<string> words=new List<string>();
            foreach(Match m in Regex.Matches((text??string.Empty).ToLowerInvariant(),@"[a-z]+"))
            {
                string word=m.Value;
                if(!stop.Contains(word)) words.Add(word);
            }
            return words.ToArray();
        }
        public ScriptureQuoteIndex(string path)
        {
            foreach(string line in System.IO.File.ReadLines(path))
            {
                string[] fields=line.Split(new char[]{'\t'},4);
                int chapter,verse;
                if(fields.Length!=4 || !int.TryParse(fields[1],out chapter) || !int.TryParse(fields[2],out verse) || !BibleCanon.IsValid(fields[0],chapter,verse))
                    throw new System.IO.InvalidDataException("Invalid verse in quotation corpus.");
                Entry entry=new Entry{Book=fields[0],Chapter=chapter,Verse=verse,Text=fields[3],Words=Tokens(fields[3])};
                entry.Set=new HashSet<string>(entry.Words);
                int id=entries.Count; entries.Add(entry);
                foreach(string token in entry.Set)
                {
                    List<int> ids;
                    if(!postings.TryGetValue(token,out ids)){ids=new List<int>();postings.Add(token,ids);}
                    ids.Add(id);
                }
            }
            if(entries.Count<30000) throw new System.IO.InvalidDataException("Quotation corpus is incomplete.");
        }
        private double Weight(string word)
        {
            List<int> ids;
            return 1+Math.Log(1+entries.Count/(double)((postings.TryGetValue(word,out ids)?ids.Count:0)+1));
        }
        private static int OrderedMatches(string[] a, string[] b)
        {
            int[] previous=new int[b.Length+1];
            foreach(string word in a)
            {
                int[] current=new int[b.Length+1];
                for(int j=1;j<=b.Length;j++) current[j]=word==b[j-1]?previous[j-1]+1:Math.Max(previous[j],current[j-1]);
                previous=current;
            }
            return previous[b.Length];
        }
        public ScriptureDetection Find(string text)
        {return FindScoped(text,null,0);}
        public ScriptureDetection FindInChapter(string text,string book,int chapter)
        {return string.IsNullOrEmpty(book) || chapter<1?null:FindScoped(text,book,chapter);}
        private ScriptureDetection FindScoped(string text,string book,int chapter,List<ScriptureDetection> alternatives=null)
        {
            bool scoped=book!=null && chapter>0;
            string[] all=Tokens(text);
            if(all.Length<3 || all.Length>160) return null;
            Dictionary<int,double> bestScores=new Dictionary<int,double>();
            int[] sizes={4,8,16,all.Length};
            foreach(int wanted in sizes)
            {
                int size=Math.Min(wanted,all.Length);
                if(size<3) continue;
                for(int start=0;start+size<=all.Length;start+=Math.Max(1,size/2))
                {
                    string[] query=new string[size]; Array.Copy(all,start,query,0,size);
                    HashSet<string> unique=new HashSet<string>(query);
                    if(unique.Count<3) continue;
                    double total=0; Dictionary<int,double> sums=new Dictionary<int,double>();
                    foreach(string word in unique)
                    {
                        double weight=Weight(word);total+=weight;List<int> ids;
                        if(!postings.TryGetValue(word,out ids)) continue;
                        foreach(int id in ids){if(scoped && (entries[id].Book!=book || entries[id].Chapter!=chapter))continue;double value;sums.TryGetValue(id,out value);sums[id]=value+weight;}
                    }
                    List<KeyValuePair<int,double>> ranked=new List<KeyValuePair<int,double>>(sums);
                    ranked.Sort(delegate(KeyValuePair<int,double> a,KeyValuePair<int,double>b){return b.Value.CompareTo(a.Value);});
                    for(int i=0;i<Math.Min(30,ranked.Count);i++)
                    {
                        int id=ranked[i].Key; double coverage=ranked[i].Value/total;
                        if(coverage<0.82) continue;
                        Entry candidate=entries[id];int shared=0;double rarest=0;
                        foreach(string word in unique) if(candidate.Set.Contains(word)){shared++;rarest=Math.Max(rarest,Weight(word));}
                        string literal=Regex.Replace(text.ToLowerInvariant(),@"[^a-z]+"," ").Trim();
                        string verseLiteral=Regex.Replace(candidate.Text.ToLowerInvariant(),@"[^a-z]+"," ").Trim();
                        bool exactExcerpt=literal.Split(' ').Length>=8 &&
                            (" "+verseLiteral+" ").Contains(" "+literal+" ");
                        if(shared<3 || (shared<5 && rarest<7.0 && !exactExcerpt)) continue;
                        double order=OrderedMatches(query,candidate.Words)/(double)query.Length;
                        if(order<0.75) continue;
                        double score=0.7*coverage+0.2*order+0.1*Math.Min(1,shared/8.0);
                        double old;
                        if(!bestScores.TryGetValue(id,out old) || score>old) bestScores[id]=score;
                    }
                }
            }
            List<KeyValuePair<int,double>> results=new List<KeyValuePair<int,double>>(bestScores);
            results.Sort(delegate(KeyValuePair<int,double>a,KeyValuePair<int,double>b){return b.Value.CompareTo(a.Value);});
            if(results.Count==0 || results[0].Value<0.88) return null;
            if(results.Count>1 && results[0].Value-results[1].Value<(scoped?0.08:0.025))
            {
                // Parallel passages can share near-verbatim wording. Preserve a small
                // review-only shortlist instead of suppressing every strong match.
                if(alternatives!=null)foreach(KeyValuePair<int,double> result in results)
                {
                    if(alternatives.Count>=3 || result.Value<0.88 || results[0].Value-result.Value>=0.025)break;
                    Entry parallel=entries[result.Key];
                    alternatives.Add(new ScriptureDetection{Book=parallel.Book,Chapter=parallel.Chapter,Verse=parallel.Verse,
                        SourceText=text,MatchedText=parallel.Text,RequiresReview=true,EvidenceKind="quotation",
                        Confidence=0.65,MatchScore=result.Value,Reason="Shared wording in multiple passages — choose the intended reference"});
                }
                return null;
            }
            Entry match=entries[results[0].Key];
            return new ScriptureDetection{Book=match.Book,Chapter=match.Chapter,Verse=match.Verse,
                SourceText=text,MatchedText=match.Text,RequiresReview=true,EvidenceKind="quotation",
                Confidence=0.75,MatchScore=results[0].Value,Reason=scoped?"Possible quotation within the recently named chapter — review wording before presenting":"Possible quotation — matched local KJV wording; review before presenting"};
        }
        public List<ScriptureDetection> FindAll(string text)
        {
            List<ScriptureDetection> found=new List<ScriptureDetection>();HashSet<string> seen=new HashSet<string>();
            foreach(string phrase in SearchPhrases(text))
            {
                List<ScriptureDetection> alternatives=new List<ScriptureDetection>();
                ScriptureDetection d=FindScoped(phrase,null,0,alternatives);
                if(d!=null && seen.Add(d.ReferenceText))found.Add(d);
                foreach(ScriptureDetection alternative in alternatives)if(seen.Add(alternative.ReferenceText))found.Add(alternative);
            }
            return found;
        }
        public static List<string> SearchPhrases(string text)
        {
            List<string> phrases=new List<string>();HashSet<string> seen=new HashSet<string>();
            foreach(string sentence in Regex.Split(text??string.Empty,@"[.!?;\r\n]+"))
            {
                string clean=sentence.Trim();if(clean.Length==0)continue;
                string[] words=Regex.Split(clean,@"\s+");
                if(words.Length<=36 && seen.Add(clean))phrases.Add(clean);
                if(words.Length>16)for(int start=0;start<words.Length-4;start+=10)
                {string part=string.Join(" ",words,start,Math.Min(20,words.Length-start));if(seen.Add(part))phrases.Add(part);}
            }
            string whole=(text??string.Empty).Trim();
            if(whole.Length>0 && whole.Length<=1500 && seen.Add(whole))phrases.Add(whole);
            return phrases;
        }
        public ScriptureDetection SemanticCandidate(int id,string heard,double score)
        {
            if(id<0 || id>=entries.Count)return null;Entry e=entries[id];
            return new ScriptureDetection{Book=e.Book,Chapter=e.Chapter,Verse=e.Verse,SourceText=heard,MatchedText=e.Text,
                RequiresReview=true,EvidenceKind="paraphrase",Confidence=0.65,MatchScore=score,
                Reason="Possible paraphrase — offline meaning search and reranking; not a verified quotation"};
        }
    }

    internal sealed class OfflineSemanticSearch : IDisposable
    {
        private System.Diagnostics.Process worker;
        private DateTime retryAfter=DateTime.MinValue;
        public string Status="Not loaded";
        private string ReadLine(int timeout)
        {
            System.Threading.Tasks.Task<string> read=worker.StandardOutput.ReadLineAsync();
            if(!read.Wait(timeout))throw new TimeoutException("Offline search timed out");
            string line=read.Result;if(line==null)throw new System.IO.IOException("Offline search worker stopped");return line;
        }
        public List<ScriptureDetection> Find(ScriptureQuoteIndex index,string text)
        {
            if(DateTime.UtcNow<retryAfter)return new List<ScriptureDetection>();
            try{return FindCore(index,text);}
            catch(Exception)
            {
                Dispose();retryAfter=DateTime.UtcNow.AddSeconds(60);
                Status="Lexical search ready; semantic search unavailable (retry in 1 min)";
                return new List<ScriptureDetection>();
            }
        }
        private List<ScriptureDetection> FindCore(ScriptureQuoteIndex index,string text)
        {
            List<ScriptureDetection> results=new List<ScriptureDetection>();
            string root=AppDomain.CurrentDomain.BaseDirectory;
            string exe=System.IO.Path.Combine(root,"search-runtime","Gazely.Search.exe"),models=System.IO.Path.Combine(root,"models","scripture-search");
            if(!System.IO.File.Exists(exe) || !System.IO.File.Exists(System.IO.Path.Combine(models,"kjv-vectors.bin")) ||
                (System.IO.File.Exists(System.IO.Path.Combine(root,"corpus","web-search.tsv")) && !System.IO.File.Exists(System.IO.Path.Combine(models,"web-vectors.bin"))))
            {Status="Lexical search ready; semantic package not installed";return results;}
            if(worker==null || worker.HasExited)
            {
                Dispose();worker=new System.Diagnostics.Process();
                worker.StartInfo=new System.Diagnostics.ProcessStartInfo(exe,"\""+models+"\" \""+System.IO.Path.Combine(root,"corpus","kjv.tsv")+"\"")
                {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                worker.ErrorDataReceived+=delegate(object sender,System.Diagnostics.DataReceivedEventArgs e){};
                worker.Start();worker.BeginErrorReadLine();if(ReadLine(30000)!="READY")throw new System.IO.IOException("Offline search failed to initialize");
            }
            worker.StandardInput.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));worker.StandardInput.Flush();
            string response=ReadLine(10000);if(response=="ERROR")throw new System.IO.IOException("Semantic search rejected query");
            foreach(string record in response.Split(';'))
            {
                string[] fields=record.Split(',');int id;double score;
                if(fields.Length==3 && int.TryParse(fields[0],out id) && double.TryParse(fields[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out score))
                {ScriptureDetection d=index.SemanticCandidate(id,text,score);if(d!=null)results.Add(d);}
            }
            Status="Offline semantic search ready";return results;
        }
        public void Dispose(){if(worker!=null){try{if(!worker.HasExited)worker.Kill();}catch{}worker.Dispose();worker=null;}}
    }

    internal sealed class QuotationContext
    {
        private readonly Queue<string> phrases = new Queue<string>();
        private readonly Queue<DateTime> times = new Queue<DateTime>();
        private DateTime last = DateTime.MinValue;
        public string RecentPair { get {string[] items=phrases.ToArray();return string.Join(" ",items,Math.Max(0,items.Length-2),Math.Min(2,items.Length));} }
        public void Reset() { phrases.Clear();times.Clear(); last = DateTime.MinValue; }
        public string Append(string text, DateTime time)
        {
            if(time-last>TimeSpan.FromSeconds(15) || time<last) {phrases.Clear();times.Clear();}
            last=time;
            string[] words=Regex.Split(text.Trim(),@"\s+");
            phrases.Enqueue(words.Length>120?string.Join(" ",words,words.Length-120,120):text);times.Enqueue(time);
            while(phrases.Count>12 || (phrases.Count>1 && (time-times.Peek()>TimeSpan.FromSeconds(30) || string.Join(" ",phrases.ToArray()).Split(' ').Length>120))) {phrases.Dequeue();times.Dequeue();}
            return string.Join(" ",phrases.ToArray());
        }
    }

    internal sealed class SpeechChunk
    {
        public byte[] Pcm;
        public bool IsFinal;
        public long UtteranceId;
        public long LastVoiceTicks;
        public long QueuedTicks;
        public int Session;
        public int TailSilenceMs;
        public long AudioStartSample, AudioEndSample;
        public bool IsRefinement;
        public long ReferenceRevision;
        public string ReferenceAnchor;
        public bool CorrectionRequested;
    }

    // PCM16 mono 16 kHz. Captured audio remains in memory only; bounded to 640 KB.
    internal sealed class RecentSpeechAudio
    {
        private readonly byte[] bytes=new byte[20*32000];
        private long total;
        public void Reset(){lock(this){Array.Clear(bytes,0,bytes.Length);total=0;}}
        public long EndSample {get{lock(this)return total/2;}}
        public void Append(byte[] pcm)
        {
            if(pcm==null || pcm.Length%2!=0)throw new ArgumentException("Expected PCM16 samples");
            lock(this){for(int offset=0;offset<pcm.Length;){int pos=(int)(total%bytes.Length);int count=Math.Min(pcm.Length-offset,bytes.Length-pos);Buffer.BlockCopy(pcm,offset,bytes,pos,count);total+=count;offset+=count;}}
        }
        public SpeechChunk Snapshot(int seconds)
        {
            lock(this){int count=(int)Math.Min(total,Math.Min(16,Math.Max(1,seconds))*32000L);byte[] pcm=new byte[count];long start=total-count;
                for(int offset=0;offset<count;){int pos=(int)((start+offset)%bytes.Length);int n=Math.Min(count-offset,bytes.Length-pos);Buffer.BlockCopy(bytes,pos,pcm,offset,n);offset+=n;}
                return new SpeechChunk{Pcm=pcm,AudioStartSample=start/2,AudioEndSample=total/2,IsRefinement=true};}
        }
    }

    internal sealed class ReferenceRefinement
    {
        private string anchor;
        private int session, attempts;
        private long revision;
        private double lastAttempt=-100000, requestedAt, costPerAudioMs;
        private SpeechChunk pending;
        public int Requested {get;private set;}
        public int Admitted {get;private set;}
        public int Expired {get;private set;}
        public int Deferred {get;private set;}
        public void Reset(){lock(this){anchor=null;pending=null;attempts=0;revision++;lastAttempt=-100000;costPerAudioMs=0;}}
        public void Observe(double audioMs,double inferenceMs)
        {if(audioMs>0)lock(this){double cost=inferenceMs/audioMs;costPerAudioMs=costPerAudioMs==0?cost:Math.Max(cost,costPerAudioMs*0.8);}}
        public void UpdateIntent(string value,int currentSession)
        {lock(this){if(anchor==value && session==currentSession)return;anchor=value;session=currentSession;pending=null;attempts=0;lastAttempt=-100000;revision++;}}
        public bool Request(RecentSpeechAudio audio,double now,double completedAt=-1,bool correction=false)
        {
            lock(this){if(pending!=null && correction)pending.CorrectionRequested=true;
                if(string.IsNullOrEmpty(anchor) || pending!=null || attempts>=2 || now-lastAttempt<3000)return false;
                SpeechChunk chunk=audio.Snapshot(16);if(chunk.Pcm.Length<32000)return false;
                chunk.Session=session;chunk.ReferenceRevision=revision;chunk.ReferenceAnchor=anchor;
                chunk.CorrectionRequested=correction;
                chunk.QueuedTicks=System.Diagnostics.Stopwatch.GetTimestamp();chunk.LastVoiceTicks=chunk.QueuedTicks;
                if(completedAt>=0)chunk.LastVoiceTicks=(long)(completedAt*System.Diagnostics.Stopwatch.Frequency/1000.0);
                pending=chunk;requestedAt=completedAt>=0?completedAt:now;Requested++;return true;}
        }
        public SpeechChunk Take(double now)
        {
            lock(this){if(pending==null)return null;
                // A serial decoder cannot preempt an inference already running.
                // Admit only work estimated below 800 ms, leaving live headroom.
                double cost=pending.Pcm.Length/32.0*costPerAudioMs;
                if(now-requestedAt>4000){pending=null;Expired++;return null;}
                if(costPerAudioMs<=0 || cost>800 || now-requestedAt+cost>4000){Deferred++;return null;}
                SpeechChunk result=pending;pending=null;attempts++;Admitted++;lastAttempt=now;return result;}
        }
        public bool IsCurrent(SpeechChunk chunk)
        {lock(this)return chunk.Session==session && chunk.ReferenceRevision==revision && chunk.ReferenceAnchor==anchor;}
    }

    internal static class SpeechVocabulary
    {
        // The real-sermon benchmark reproduced invented book names from vocabulary
        // prompting. Keep literal speech unbiased; scripture interpretation is separate.
        public const string Prompt = "";

        public static bool IsRunawayRepetition(string text)
        {
            // Only reject long, near-total loops. Ordinary emphasis ("holy, holy,
            // holy") and occasional repeated phrases are retained verbatim.
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] words = text.ToLowerInvariant().Split(
                new char[] { ' ', '\r', '\n', '\t', ',', '.', '!', '?', ':', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 40) return false;
            for (int width = 1; width <= 6; width++)
                for (int start = 0; start + width * 8 <= words.Length; start++)
                {
                    int end = start + width;
                    while (end < words.Length && words[end] == words[start + (end - start) % width]) end++;
                    int completeWords = ((end - start) / width) * width;
                    if (completeWords >= width * 8 && completeWords >= words.Length * 0.8)
                        return true;
                }
            return false;
        }

        public static byte[] NormalizeAudio(byte[] pcm)
        {
            if (pcm == null || pcm.Length < 2) return pcm;
            double sum = 0;
            int peak = 0;
            for (int i = 0; i + 1 < pcm.Length; i += 2)
            {
                short sample = (short)(pcm[i] | pcm[i + 1] << 8);
                sum += (double)sample * sample;
                peak = Math.Max(peak, Math.Abs((int)sample));
            }
            double rms = Math.Sqrt(sum / (pcm.Length / 2));
            // Bounded gain, applied only after speech gating. Never clip or
            // amplify near-silence into a transcription candidate.
            if (rms < 32 || peak == 0) return pcm;
            double gain = Math.Min(4.0, Math.Min(6500.0 / rms, 30000.0 / peak));
            if (gain <= 1.05) return pcm;
            byte[] result = new byte[pcm.Length];
            for (int i = 0; i + 1 < pcm.Length; i += 2)
            {
                short sample = (short)(pcm[i] | pcm[i + 1] << 8);
                short adjusted = (short)Math.Round(sample * gain);
                result[i] = (byte)(adjusted & 255);
                result[i + 1] = (byte)((adjusted >> 8) & 255);
            }
            return result;
        }
    }

    internal sealed class UtteranceSegmenter
    {
        public readonly RecentSpeechAudio RecentAudio=new RecentSpeechAudio();
        public event Action<SpeechChunk> ChunkReady;
        public event Action<bool, double> SpeechStateChanged;
        private readonly Queue<byte[]> preRoll = new Queue<byte[]>();
        private MemoryStream current;
        private int preRollBytes, silenceMs, speechMs, elapsedMs, sincePartialMs;
        private bool lastUiSpeech;
        private long utteranceId, lastVoiceTicks;
        private double noiseFloorDb = -65;
        public double ThresholdDb = -50;
        public int SilenceToFinalizeMs = 400;
        public int MinSpeechMs = 160;
        public int MaxUtteranceMs = 6500;
        public int PartialIntervalMs = 1200;
        public bool EnablePartials = true;

        public void Reset()
        {
            lock (this)
            {
                RecentAudio.Reset();
                preRoll.Clear(); preRollBytes = 0;
                if (current != null) current.Dispose();
                current = null;
                silenceMs = speechMs = elapsedMs = sincePartialMs = 0;
                lastUiSpeech = false;
                noiseFloorDb = -65;
                // Do not reuse identifiers when sessions change.
                utteranceId++;
            }
        }

        public void Process(byte[] pcm)
        {
            if (pcm == null || pcm.Length < 2) return;
            int chunkMs = (int)Math.Round(pcm.Length / 32.0);
            double db = CalculateRmsDb(pcm);
            lock (this)
            {
                RecentAudio.Append(pcm);
                // Track background only while idle; hysteresis retains quieter
                // syllables once speech starts. The meter still shows raw audio.
                double startThreshold = Math.Max(ThresholdDb, Math.Min(-35, noiseFloorDb + 9));
                bool speech = db >= startThreshold - (current == null ? 0 : 6);
                if (current == null)
                {
                    preRoll.Enqueue((byte[])pcm.Clone()); preRollBytes += pcm.Length;
                    while (preRollBytes > 12800 && preRoll.Count > 1)
                        preRollBytes -= preRoll.Dequeue().Length; // 400 ms
                    if (!speech)
                    {
                        noiseFloorDb = 0.97 * noiseFloorDb + 0.03 * db;
                        return;
                    }
                    current = new MemoryStream();
                    foreach (byte[] pre in preRoll) current.Write(pre, 0, pre.Length);
                    preRoll.Clear(); preRollBytes = 0;
                    utteranceId++;
                    speechMs = elapsedMs = sincePartialMs = chunkMs;
                    silenceMs = 0;
                    lastVoiceTicks = Stopwatch.GetTimestamp();
                }
                else
                {
                    current.Write(pcm, 0, pcm.Length);
                    elapsedMs += chunkMs;
                    sincePartialMs += chunkMs;
                    if (speech)
                    {
                        speechMs += chunkMs; silenceMs = 0;
                        lastVoiceTicks = Stopwatch.GetTimestamp();
                    }
                    else silenceMs += chunkMs;
                }
                if (speech != lastUiSpeech)
                {
                    lastUiSpeech = speech;
                    if (SpeechStateChanged != null) SpeechStateChanged(speech, db);
                }
                bool ended = silenceMs >= SilenceToFinalizeMs;
                bool capped = elapsedMs >= MaxUtteranceMs;
                if (ended || capped)
                {
                    if (speechMs >= MinSpeechMs) Emit(true);
                    byte[] carry = capped && !ended ? current.ToArray() : null;
                    current.Dispose(); current = null;
                    speechMs = silenceMs = elapsedMs = sincePartialMs = 0;
                    if (carry != null)
                    {
                        // Overlap at a forced cut so a book at the boundary is
                        // available to the next window too.
                        int count = Math.Min(32000, carry.Length);
                        byte[] overlap = new byte[count];
                        Buffer.BlockCopy(carry, carry.Length - count, overlap, 0, count);
                        current = new MemoryStream(); current.Write(overlap, 0, overlap.Length);
                        utteranceId++;
                    }
                }
                else if (EnablePartials && speechMs >= 650 && sincePartialMs >= PartialIntervalMs)
                {
                    Emit(false); sincePartialMs = 0;
                }
            }
        }

        private void Emit(bool final)
        {
            if (ChunkReady == null) return;
            SpeechChunk chunk = new SpeechChunk();
            chunk.Pcm = current.ToArray(); chunk.IsFinal = final;
            chunk.UtteranceId = utteranceId; chunk.LastVoiceTicks = lastVoiceTicks;
            chunk.QueuedTicks = Stopwatch.GetTimestamp();
            chunk.TailSilenceMs = silenceMs;
            chunk.AudioEndSample=RecentAudio.EndSample;
            chunk.AudioStartSample=chunk.AudioEndSample-chunk.Pcm.Length/2;
            ChunkReady(chunk);
        }

        private static double CalculateRmsDb(byte[] pcm)
        {
            double sum = 0;
            for (int i = 0; i + 1 < pcm.Length; i += 2)
            {
                double sample = (short)(pcm[i] | pcm[i + 1] << 8) / 32768.0;
                sum += sample * sample;
            }
            double rms = Math.Sqrt(sum / (pcm.Length / 2));
            return rms > 0.000001 ? 20 * Math.Log10(rms) : -90;
        }
    }
    internal sealed class WhisperServerEngine : IDisposable
    {
        // Available to the replay harness. Keep off: current VAD settings worsened
        // real-sermon WER despite rejecting synthetic noise successfully.
        public bool EnableNeuralVad = false;
        public bool NormalizeInput = true;
        public int BeamSizeOverride = 0;
        private Process process;
        private int port;
        private int beamSize = 1;
        private Action<string> logger;

        public bool IsRunning
        {
            get
            {
                return process != null &&
                    !process.HasExited;
            }
        }

        public string FindServerExe()
        {
            string root = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "whisper");

            if (!Directory.Exists(root))
                return string.Empty;

            string[] files = Directory.GetFiles(
                root,
                "whisper-server.exe",
                SearchOption.AllDirectories);

            return files.Length > 0
                ? files[0]
                : string.Empty;
        }

        public string GetModelPath(string modelName)
        {
            string file;

            if (string.Equals(
                modelName,
                "tiny.en",
                StringComparison.OrdinalIgnoreCase))
            {
                file = "ggml-tiny.en.bin";
            }
            else if (string.Equals(
                modelName,
                "small.en",
                StringComparison.OrdinalIgnoreCase))
            {
                file = "ggml-small.en.bin";
                // Accept the measured compact candidate without requiring a second,
                // full-precision download. Existing full models retain priority.
                string compact = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "models", "ggml-small.en-q5_1.bin");
                if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "models", file)) && File.Exists(compact)) return compact;
            }
            else if(string.Equals(modelName,"distil-small.en",StringComparison.OrdinalIgnoreCase))
            {
                file="ggml-distil-small.en.bin";
            }
            else
            {
                file = "ggml-base.en.bin";
            }

            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "models",
                file);
        }

        public string GetVadModelPath()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "models",
                "ggml-silero-v6.2.0.bin");
        }

        public void Start(
            string modelName,
            Action<string> log)
        {
            Stop();

            logger = log;
            // Each session owns a private port rather than adopting an old server.
            TcpListener reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            beamSize = BeamSizeOverride>0?BeamSizeOverride:modelName == "tiny.en" ? 1 : 3;

            string exe = FindServerExe();
            string model = GetModelPath(modelName);
            string vad = GetVadModelPath();

            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                throw new FileNotFoundException(
                    "Local speech AI is not installed yet. Run INSTALL_FAST_SPEECH_AI.bat first.");

            if (!File.Exists(model))
                throw new FileNotFoundException(
                    "Speed / accuracy was not found: " +
                    model +
                    ". Run the matching Whisper setup script first.");

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            int threads =
                Math.Min(
                    8,
                    Math.Max(
                        4,
                        Environment.ProcessorCount / 2));

            StringBuilder args = new StringBuilder();
            args.Append("--host 127.0.0.1 ");
            args.Append("--port " + port + " ");
            args.Append("-m \"" + model + "\" ");
            args.Append("-l en ");
            args.Append("-nt ");
            args.Append("-t " + threads + " ");
            args.Append("-bo 1 -bs " + beamSize + " ");
            args.Append("-nf ");

            // Energy endpointing controls UI responsiveness; neural VAD can additionally
            // reject non-speech inside each window before the decoder sees it.
            if (EnableNeuralVad)
            {
                if (!File.Exists(vad)) throw new FileNotFoundException("Neural speech filter model is missing. Run speech setup again.", vad);
                args.Append("--vad -vm \"" + vad + "\" -vt 0.5 -vspd 160 -vsd 400 -vp 200 -vo 0.1 ");
            }
            psi.Arguments = args.ToString();

            process = new Process();
            process.StartInfo = psi;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += delegate(
                object sender,
                DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Log("whisper: " + e.Data);
            };

            process.ErrorDataReceived += delegate(
                object sender,
                DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Log("whisper: " + e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            DateTime deadline = DateTime.UtcNow.AddSeconds(45);

            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException(
                        "whisper-server exited before it became ready.");

                if (CanConnect())
                {
                    Log("Local Whisper server ready on 127.0.0.1:" + port);
                    return;
                }

                Application.DoEvents();
                Thread.Sleep(200);
            }

            throw new TimeoutException(
                "Speed / accuracy/server did not become ready within 45 seconds.");
        }

        private bool CanConnect()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/health");
                request.Proxy = null;
                request.Timeout = 500;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    return reader.ReadToEnd().Contains("\"ok\"") && !process.HasExited;
            }
            catch { return false; }
        }
        public string Transcribe(
            byte[] pcm16Mono,
            string prompt)
        {
            if (!IsRunning)
                throw new InvalidOperationException(
                    "Local Whisper server is not running.");

            byte[] wav = BuildWav(
                NormalizeInput?SpeechVocabulary.NormalizeAudio(pcm16Mono):pcm16Mono,
                16000,
                1,
                16);

            string boundary =
                "----EWWhisper" +
                DateTime.UtcNow.Ticks.ToString("X");

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" +
                    port +
                    "/inference");

            request.Method = "POST";
            request.Proxy = null;
            request.ServicePoint.Expect100Continue = false;
            request.ContentType =
                "multipart/form-data; boundary=" +
                boundary;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            request.KeepAlive = true;

            using (Stream requestStream =
                request.GetRequestStream())
            {
                WriteFilePart(
                    requestStream,
                    boundary,
                    "file",
                    "utterance.wav",
                    "audio/wav",
                    wav);

                WriteTextPart(
                    requestStream,
                    boundary,
                    "response_format",
                    "json");

                WriteTextPart(
                    requestStream,
                    boundary,
                    "temperature",
                    "0.0");

                WriteTextPart(
                    requestStream,
                    boundary,
                    "temperature_inc",
                    "0.0");

                WriteTextPart(
                    requestStream,
                    boundary,
                    "prompt",
                    prompt ?? string.Empty);

                WriteTextPart(requestStream, boundary, "language", "en");
                WriteTextPart(requestStream, boundary, "beam_size", beamSize.ToString());
                WriteTextPart(requestStream, boundary, "suppress_non_speech", "true");

                byte[] end = Encoding.UTF8.GetBytes(
                    "--" + boundary + "--\r\n");

                requestStream.Write(
                    end,
                    0,
                    end.Length);
            }

            using (HttpWebResponse response =
                (HttpWebResponse)request.GetResponse())
            using (StreamReader reader =
                new StreamReader(
                    response.GetResponseStream(),
                    Encoding.UTF8))
            {
                string body = reader.ReadToEnd();
                string text = ExtractJsonText(body);
                if (SpeechVocabulary.IsRunawayRepetition(text))
                {
                    Log("Rejected runaway repetition from recognition; no transcript or scripture action produced.");
                    return string.Empty;
                }
                return text;
            }
        }

        private static void WriteTextPart(
            Stream stream,
            string boundary,
            string name,
            string value)
        {
            string text =
                "--" + boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"" +
                name +
                "\"\r\n\r\n" +
                value +
                "\r\n";

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteFilePart(
            Stream stream,
            string boundary,
            string name,
            string fileName,
            string contentType,
            byte[] data)
        {
            string header =
                "--" + boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"" +
                name +
                "\"; filename=\"" +
                fileName +
                "\"\r\n" +
                "Content-Type: " +
                contentType +
                "\r\n\r\n";

            byte[] h = Encoding.UTF8.GetBytes(header);
            stream.Write(h, 0, h.Length);
            stream.Write(data, 0, data.Length);

            byte[] newline = Encoding.UTF8.GetBytes("\r\n");
            stream.Write(newline, 0, newline.Length);
        }

        private static byte[] BuildWav(
            byte[] pcm,
            int sampleRate,
            short channels,
            short bits)
        {
            if (pcm == null)
                pcm = new byte[0];

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int byteRate =
                    sampleRate *
                    channels *
                    (bits / 8);

                short blockAlign =
                    (short)(channels * (bits / 8));

                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + pcm.Length);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write(channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write(bits);
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(pcm.Length);
                bw.Write(pcm);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static string ExtractJsonText(
            string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            Match m = Regex.Match(
                json,
                "\"text\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
                RegexOptions.Singleline);

            if (!m.Success)
                return json.Trim();

            return JsonUnescape(m.Groups[1].Value).Trim();
        }

        private static string JsonUnescape(
            string s)
        {
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c != '\\' || i + 1 >= s.Length)
                {
                    output.Append(c);
                    continue;
                }

                char n = s[++i];

                switch (n)
                {
                    case '"': output.Append('"'); break;
                    case '\\': output.Append('\\'); break;
                    case '/': output.Append('/'); break;
                    case 'b': output.Append('\b'); break;
                    case 'f': output.Append('\f'); break;
                    case 'n': output.Append('\n'); break;
                    case 'r': output.Append('\r'); break;
                    case 't': output.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            string hex =
                                s.Substring(i + 1, 4);

                            int code;

                            if (int.TryParse(
                                hex,
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out code))
                            {
                                output.Append((char)code);
                                i += 4;
                            }
                        }
                        break;
                    default:
                        output.Append(n);
                        break;
                }
            }

            return output.ToString();
        }

        public void Stop()
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(1500);
                    }
                }
                catch { }

                try { process.Dispose(); }
                catch { }

                process = null;
            }
        }

        private void Log(string message)
        {
            Action<string> l = logger;
            if (l != null)
                l(message);
        }

        public void Dispose()
        {
            Stop();
        }
    }

    // Continuous PCM transport: the selected native engine owns decoding/caches.
    internal sealed class StreamingSpeechEngine : IDisposable
    {
        private Process worker;
        private Thread writer,reader;
        private readonly Queue<byte[]> pending=new Queue<byte[]>();
        private readonly AutoResetEvent wake=new AutoResetEvent(false);
        private readonly ManualResetEvent ready=new ManualResetEvent(false);
        private volatile bool ending,aborted,finished;
        private string failure;
        private int queuedBytes;
        private int disposed;
        public Action<string,SpeechChunk,bool,double> Transcript;
        public Action<string> Error;
        private long origin;
        private int session;
        public string Name { get; private set; }
        public int NativeProcessId { get; private set; }
        public int WorkerProcessId { get; private set; }
        private long receivedSamples,processedSamples;
        public Action<long,long,int,double> Progress;
        public double BacklogSeconds {get{return Math.Max(0,(Interlocked.Read(ref receivedSamples)-Interlocked.Read(ref processedSamples))/16000.0);}}
        public void Start(string size,int generation,Action<string> log)
        {
            Name=size=="nemotron"?"Nemotron":"Moonshine";
            session=generation;origin=Stopwatch.GetTimestamp();
            string app=AppDomain.CurrentDomain.BaseDirectory,root=Path.GetFullPath(Path.Combine(app,".."));
            string python=Path.Combine(root,"benchmarks","tools","moonshine-env","Scripts","python.exe");
            string model=Path.Combine(root,"benchmarks","tools","moonshine-models","download.moonshine.ai","model",size+"-streaming-en","quantized_26_08_21");
            string manifest=Path.Combine(root,"benchmarks","moonshine-"+size+"-manifest.json");
            string script=Path.Combine(app,"speech-runtime","moonshine_worker.py");
            if(size=="nemotron"){
                python=Path.Combine(root,"benchmarks","tools","parakeet-env","Scripts","python.exe");
                model=Path.Combine(root,"benchmarks","tools","streaming-candidates");
                manifest=Path.Combine(root,"benchmarks","streaming-candidates-manifest.json");
                script=Path.Combine(app,"speech-runtime","nemotron_worker.py");
            }
            if(!File.Exists(python)||!File.Exists(manifest)||!File.Exists(script)||!Directory.Exists(model))throw new FileNotFoundException(Name+" evaluation runtime/model is not installed in this checkout.");
            worker=new Process();worker.StartInfo=new ProcessStartInfo(python,"-u \""+script+"\" \""+model+"\" \""+manifest+"\"")
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            worker.StartInfo.EnvironmentVariables["MOONSHINE_ORT_SINGLE_THREAD"]="0";
            worker.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)log(Name+": "+e.Data);};
            worker.Start();worker.BeginErrorReadLine();
            reader=new Thread(Read);reader.IsBackground=true;reader.Start();
            if(!ready.WaitOne(60000)||failure!=null){Dispose();throw new InvalidOperationException(failure??Name+" startup timed out");}
            origin=Stopwatch.GetTimestamp();
            writer=new Thread(Write);writer.IsBackground=true;writer.Start();
        }
        private void Fail(string message)
        {
            if(aborted || Interlocked.CompareExchange(ref failure,message,null)!=null)return;ready.Set();
            if(Error!=null)Error(message);
        }
        private void Read()
        {
            try{
                string line;while((line=worker.StandardOutput.ReadLine())!=null){
                    string[] parts=line.Split('\t');
                    if(parts[0]=="READY"){ready.Set();continue;}
                    if(parts[0]=="DONE"){finished=true;return;}
                    if(parts[0]=="SERVER" && parts.Length==2){NativeProcessId=int.Parse(parts[1]);continue;}
                    if(parts[0]=="WORKER" && parts.Length==2){WorkerProcessId=int.Parse(parts[1]);continue;}
                    if(parts[0]=="PROGRESS" && (parts.Length==2 || parts.Length==3)){
                        Interlocked.Exchange(ref processedSamples,long.Parse(parts[1]));int queued;lock(pending)queued=queuedBytes;
                        if(Progress!=null)Progress(Interlocked.Read(ref receivedSamples),Interlocked.Read(ref processedSamples),queued,
                            parts.Length==3?double.Parse(parts[2],System.Globalization.CultureInfo.InvariantCulture):-1);
                        continue;
                    }
                    if(parts[0]=="ERROR"){Fail(Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));return;}
                    if(parts.Length!=5 || (parts[0]!="PARTIAL" && parts[0]!="FINAL"))throw new IOException("Invalid "+Name+" response");
                    long id=long.Parse(parts[1]),start=long.Parse(parts[2]),end=long.Parse(parts[3]);
                    if(Name=="Moonshine")Interlocked.Exchange(ref processedSamples,end);
                    long lastVoice=origin+(long)(end/16000.0*Stopwatch.Frequency);
                    var chunk=new SpeechChunk{Pcm=new byte[0],Session=session,UtteranceId=id,IsFinal=parts[0]=="FINAL",AudioStartSample=start,AudioEndSample=end,LastVoiceTicks=lastVoice,QueuedTicks=lastVoice};
                    if(Transcript!=null)Transcript(Encoding.UTF8.GetString(Convert.FromBase64String(parts[4])),chunk,chunk.IsFinal,Math.Max(0,(Stopwatch.GetTimestamp()-lastVoice)*1000.0/Stopwatch.Frequency));
                }
                if(!aborted && !finished)Fail(Name+" stopped before flushing the stream");
            }catch(Exception ex){Fail(ex.Message);}
        }
        public void AddAudio(byte[] pcm)
        {
            bool overflow=false;
            lock(pending){if(ending||aborted||failure!=null)return;if(queuedBytes+pcm.Length>640000)overflow=true;else{pending.Enqueue((byte[])pcm.Clone());queuedBytes+=pcm.Length;}}
            Interlocked.Add(ref receivedSamples,pcm.Length/2);
            if(overflow){Fail(Name+" fell over 20 seconds behind. Listening stopped; choose a lighter speech engine.");return;}
            wake.Set();
        }
        private void Write()
        {
            try{
                var output=new BinaryWriter(worker.StandardInput.BaseStream);
                while(!aborted){byte[] pcm=null;lock(pending){if(pending.Count>0){pcm=pending.Dequeue();queuedBytes-=pcm.Length;}else if(ending)break;}
                    if(pcm==null){wake.WaitOne(100);continue;}
                    // Transport packets are not utterances: keep one native stream open.
                    for(int offset=0;offset<pcm.Length;offset+=3200){int count=Math.Min(3200,pcm.Length-offset);output.Write(count);output.Write(pcm,offset,count);output.Flush();}
                }
                if(!aborted){output.Write(0);output.Flush();}
            }catch(Exception ex){Fail(ex.Message);}
        }
        public void Finish()
        {
            ending=true;wake.Set();
            if(reader!=null && !reader.Join(30000))throw new TimeoutException(Name+" flush timed out; remaining audio was not confirmed");
            if(failure!=null)throw new IOException(failure);
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            aborted=true;ending=true;wake.Set();
            if(worker!=null){try{if(!worker.HasExited)worker.Kill();}catch{} }
            if(writer!=null && writer!=Thread.CurrentThread)writer.Join(2000);
            if(reader!=null && reader!=Thread.CurrentThread)reader.Join(2000);
            if(worker!=null){worker.Dispose();worker=null;}
            if((writer==null || !writer.IsAlive) && (reader==null || !reader.IsAlive)){wake.Close();ready.Close();}
        }
    }

    internal sealed class AudioLevelMeter : Control
    {
        private double rmsDb = -90.0;
        private double peakDb = -90.0;

        public AudioLevelMeter()
        {
            DoubleBuffered = true;
            Height = 36;
            MinimumSize = new Size(180, 30);
        }

        public void SetLevels(
            double rmsDb,
            double peakDb)
        {
            this.rmsDb = rmsDb;
            this.peakDb = peakDb;
            Invalidate();
        }

        private static double DbToUnit(double db)
        {
            double clamped = Math.Max(-60.0, Math.Min(0.0, db));
            return (clamped + 60.0) / 60.0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle area = ClientRectangle;
            e.Graphics.Clear(Color.FromArgb(28, 30, 34));

            int segments = Math.Max(8,Math.Min(32,area.Width/6));
            int gap = 2;
            int segmentWidth =
                Math.Max(
                    2,
                    (area.Width - ((segments - 1) * gap)) / segments);

            double level = DbToUnit(rmsDb);
            int active =
                (int)Math.Round(level * segments);

            for (int i = 0; i < segments; i++)
            {
                int x = i * (segmentWidth + gap);
                Rectangle r = new Rectangle(
                    x,
                    4,
                    Math.Max(1, segmentWidth),
                    Math.Max(4, area.Height - 8));

                Color color;

                double p = (i + 1) / (double)segments;

                if (p < 0.72)
                    color = Color.FromArgb(184, 243, 74);
                else if (p < 0.90)
                    color = Color.FromArgb(230, 174, 45);
                else
                    color = Color.FromArgb(221, 72, 72);

                if (i >= active)
                    color = Color.FromArgb(58, 61, 66);

                using (SolidBrush brush = new SolidBrush(color))
                    e.Graphics.FillRectangle(brush, r);
            }

            double peak = DbToUnit(peakDb);
            int peakX =
                Math.Max(
                    0,
                    Math.Min(
                        area.Width - 2,
                        (int)(peak * area.Width)));

            using (Pen pen = new Pen(Color.White, 2))
            {
                e.Graphics.DrawLine(
                    pen,
                    peakX,
                    1,
                    peakX,
                    area.Height - 2);
            }
        }
    }

    internal sealed partial class MainForm : Form
    {
        private readonly EasyWorshipAutomation automation;
        private readonly PresentationCoordinator destination;
        private readonly string integrationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gazely","integrations.txt");
        private CheckBox easyWorshipEnabled;
        private Label integrationStatus;
        private Label presentationStatus;
        private bool presentationRunning = false;
        private readonly string mapPath;

        private readonly Label connectionLabel;
        private readonly TextBox scriptureTextBox;
        private readonly ComboBox versionCombo;
        private readonly CheckBox changeVersionCheck;
        private readonly Label versionIndexLabel;
        private readonly Button previewButton;
        private readonly Button liveButton;
        private readonly Button testVersionButton;
        private readonly Button refreshButton;
        private readonly Button resyncVersionButton;
        private readonly Label versionStateLabel;
        private readonly TextBox logTextBox;

        private readonly TextBox mapEditor;
        private readonly Button saveMapButton;
        private readonly Button reloadMapButton;

        private readonly Dictionary<string, int> versionMap;
        private readonly Dictionary<string, string> aliases;

        private readonly System.Windows.Forms.Timer connectionTimer;

        // Audio / speech-to-text UI and state.
        private ComboBox audioDeviceCombo;
        private ComboBox recognizerCombo;
        private ComboBox speechEngineCombo;
        private ComboBox whisperModelCombo;
        private ComboBox vadSensitivityCombo;
        private Button refreshAudioButton;
        private Button startListeningButton;
        private Button stopListeningButton;
        private Button clearTranscriptButton;
        private Label audioStatusLabel;
        private Label audioSourceActiveLabel;
        private Label audioDbLabel;
        private Label signalQualityLabel;
        private Label recognizerStatusLabel;
        private Label hypothesisLabel;
        private Label confidenceLabel;
        private Label whisperStatusLabel;
        private Label vadStatusLabel;
        private Label latencyLabel;
        private Label detectedReferenceLabel;
        private Label detectedVersionLabel;
        private Label detectedConfidenceLabel;
        private Label detectedReasonLabel;
        private Label detectedSourceLabel;
        private TextBox transcriptTextBox;
        private readonly LiveTranscriptBuffer liveTranscript=new LiveTranscriptBuffer();
        private void RenderTranscript(string key,string text,bool final)
        {
            liveTranscript.Update(key,text,final,DateTime.Now.ToString("HH:mm:ss"));
            string next=liveTranscript.Text, previous=transcriptTextBox.Text;
            int common=0;while(common<next.Length && common<previous.Length && next[common]==previous[common])common++;
            bool follow=transcriptTextBox.SelectionLength==0 && transcriptTextBox.SelectionStart>=previous.Length;
            int selection=transcriptTextBox.SelectionStart,length=transcriptTextBox.SelectionLength;
            transcriptTextBox.Select(common,previous.Length-common);
            transcriptTextBox.SelectedText=next.Substring(common);
            if(follow){transcriptTextBox.Select(next.Length,0);transcriptTextBox.ScrollToCaret();}
            else transcriptTextBox.Select(Math.Min(selection,next.Length),Math.Min(length,Math.Max(0,next.Length-selection)));
        }
        private AudioLevelMeter audioMeter;
        private Button previewDetectedButton;
        private Button liveDetectedButton;
        private CheckBox autoPreviewCheck;
        private CheckBox autoLiveCheck;

        private WaveInRecorder audioRecorder;
        private BlockingAudioStream speechStream;
        private LiveSpeechRecognizer speechRecognizer;
        private WhisperServerEngine whisperEngine = new WhisperServerEngine();
        private StreamingSpeechEngine streamingEngine;
        private volatile bool useNativeStreaming;
        private bool drainingStreaming,closingApplication;
        private readonly UtteranceSegmenter utteranceSegmenter = new UtteranceSegmenter();
        private readonly ReferenceRefinement referenceRefinement=new ReferenceRefinement();
        private readonly ScriptureDetector scriptureDetector = new ScriptureDetector();
        private volatile ScriptureQuoteIndex quotationIndex = null;
        private volatile ChapterPronunciationRecovery chapterRecovery = null;
        private long interpretationRevision = 0;
        private readonly ToolTip evidenceTip = new ToolTip();
        private readonly QuotationContext quotationContext = new QuotationContext();
        private string lastQuotationReference = string.Empty;
        private readonly List<SpeechChunk> whisperQueue = new List<SpeechChunk>();
        private bool whisperWorkerRunning;
        private volatile int listeningSession;
        private volatile bool useLocalAi;
        private bool startingListening;
        private CheckBox earlyResultsCheck;
        private readonly CheckBox refinementCheck=new GazelyToggle{Name="RefinementCheck",Text="Recheck unclear references (experimental)",Checked=false};
        private long lastFinalUtteranceId;
        private readonly string diagnosticPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripture-diagnostics.log");
        private ScriptureDetection lastDetection;
        private string lastAutoSignature = string.Empty;
        private DateTime lastAutoActionUtc = DateTime.MinValue;
        private volatile bool audioListening;
        private DateTime lastMeterUiUpdate = DateTime.MinValue;

        public MainForm()
        {
            Text = "Gazely — Evaluation";
            Width = 1020;
            Height = 780;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 700);

            mapPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "version-map.txt");

            versionMap = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            aliases = BuildAliases();

            automation = new EasyWorshipAutomation();
            automation.Logger = AddLog;
            destination = new PresentationCoordinator(new EasyWorship7Adapter(automation));
            try { destination.SetEnabled(IntegrationPreferences.Load(integrationPath)); }
            catch { destination.SetEnabled(false); }

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;

            TabPage controlTab = new TabPage("Controller");
            TabPage audioTab = new TabPage("Audio / Speech");
            TabPage mapTab = new TabPage("Bible Version Map");
            TabPage integrationsTab = new TabPage("Settings / Integrations");

            tabs.TabPages.Add(controlTab);
            tabs.TabPages.Add(audioTab);
            tabs.TabPages.Add(mapTab);
            tabs.TabPages.Add(integrationsTab);
            BuildIntegrationsTab(integrationsTab);

            Controls.Add(tabs);

            Label title = new Label();
            title.Text = "EasyWorship Scripture Controller";
            title.Font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            title.AutoSize = true;
            title.Left = 22;
            title.Top = 22;
            controlTab.Controls.Add(title);

            Label safety = new Label();
            safety.Text =
                "UI automation only. This MVP does not modify EasyWorship files, databases, registry, or plugins.";
            safety.AutoSize = true;
            safety.Left = 24;
            safety.Top = 58;
            safety.ForeColor = GazelyTheme.Muted;
            controlTab.Controls.Add(safety);

            connectionLabel = new Label();
            connectionLabel.AutoSize = true;
            connectionLabel.Left = 24;
            connectionLabel.Top = 92;
            connectionLabel.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            controlTab.Controls.Add(connectionLabel);

            refreshButton = new GazelyButton();
            refreshButton.Text = "Refresh connection";
            refreshButton.Left = 220;
            refreshButton.Top = 85;
            refreshButton.Width = 135;
            refreshButton.Click += delegate { RefreshConnection(); };
            controlTab.Controls.Add(refreshButton);

            resyncVersionButton = new GazelyButton();
            resyncVersionButton.Text = "Reset version tracking";
            resyncVersionButton.Left = 370;
            resyncVersionButton.Top = 85;
            resyncVersionButton.Width = 155;
            resyncVersionButton.Click += delegate
            {
                ResetVersionTracking();
            };
            controlTab.Controls.Add(resyncVersionButton);

            versionStateLabel = new Label();
            versionStateLabel.AutoSize = true;
            versionStateLabel.Left = 540;
            versionStateLabel.Top = 92;
            versionStateLabel.ForeColor = GazelyTheme.Muted;
            controlTab.Controls.Add(versionStateLabel);

            Label scriptureLabel = new Label();
            scriptureLabel.Text = "Scripture";
            scriptureLabel.AutoSize = true;
            scriptureLabel.Left = 24;
            scriptureLabel.Top = 135;
            controlTab.Controls.Add(scriptureLabel);

            scriptureTextBox = new TextBox();
            scriptureTextBox.Left = 24;
            scriptureTextBox.Top = 157;
            scriptureTextBox.Width = 320;
            scriptureTextBox.Font = new Font(Font.FontFamily, 12);
            scriptureTextBox.Text = "John 3:16";
            controlTab.Controls.Add(scriptureTextBox);

            Label versionLabel = new Label();
            versionLabel.Text = "Bible version";
            versionLabel.AutoSize = true;
            versionLabel.Left = 390;
            versionLabel.Top = 135;
            controlTab.Controls.Add(versionLabel);

            versionCombo = new ComboBox();
            versionCombo.Left = 390;
            versionCombo.Top = 157;
            versionCombo.Width = 180;
            versionCombo.DropDownStyle = ComboBoxStyle.DropDown;
            versionCombo.SelectedIndexChanged += delegate { UpdateVersionIndexLabel(); };
            versionCombo.TextChanged += delegate { UpdateVersionIndexLabel(); };
            controlTab.Controls.Add(versionCombo);

            versionIndexLabel = new Label();
            versionIndexLabel.AutoSize = true;
            versionIndexLabel.Left = 580;
            versionIndexLabel.Top = 162;
            versionIndexLabel.ForeColor = GazelyTheme.Muted;
            controlTab.Controls.Add(versionIndexLabel);

            changeVersionCheck = new GazelyToggle();
            changeVersionCheck.Text = "Change/select Bible version before scripture";
            changeVersionCheck.Checked = true;
            changeVersionCheck.AutoSize = true;
            changeVersionCheck.Left = 24;
            changeVersionCheck.Top = 202;
            controlTab.Controls.Add(changeVersionCheck);

            testVersionButton = new GazelyButton();
            testVersionButton.Text = "Test Version Only";
            testVersionButton.Left = 24;
            testVersionButton.Top = 240;
            testVersionButton.Width = 150;
            testVersionButton.Height = 34;
            testVersionButton.Click += delegate { RunVersionOnly(); };
            controlTab.Controls.Add(testVersionButton);

            previewButton = new GazelyButton();
            previewButton.Text = "PREVIEW ONLY";
            previewButton.Left = 190;
            previewButton.Top = 240;
            previewButton.Width = 150;
            previewButton.Height = 34;
            previewButton.Click += delegate { RunScripture(false); };
            controlTab.Controls.Add(previewButton);

            liveButton = new GazelyButton();
            liveButton.Text = "SEND LIVE";
            liveButton.Left = 356;
            liveButton.Top = 240;
            liveButton.Width = 170;
            liveButton.Height = 34;
            liveButton.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            liveButton.Click += delegate { RunScripture(true); };
            controlTab.Controls.Add(liveButton);

            Label logLabel = new Label();
            logLabel.Text = "Status / log";
            logLabel.AutoSize = true;
            logLabel.Left = 24;
            logLabel.Top = 300;
            controlTab.Controls.Add(logLabel);

            logTextBox = new TextBox();
            logTextBox.Left = 24;
            logTextBox.Top = 323;
            logTextBox.Width = 920;
            logTextBox.Height = 220;
            logTextBox.Multiline = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.ReadOnly = true;
            logTextBox.Font = new Font("Consolas", 9);
            logTextBox.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;
            controlTab.Controls.Add(logTextBox);

            // Version map tab
            Label mapHelp = new Label();
            mapHelp.Text =
                "One line per installed Bible: CODE=INDEX. Index 0 is the top EasyWorship Bible row.\r\n" +
                "This map is LOCAL TO THIS CHURCH/PC. Save after calibration or after Bibles are added/removed.";
            mapHelp.AutoSize = true;
            mapHelp.Left = 20;
            mapHelp.Top = 20;
            mapTab.Controls.Add(mapHelp);

            mapEditor = new TextBox();
            mapEditor.Left = 20;
            mapEditor.Top = 70;
            mapEditor.Width = 920;
            mapEditor.Height = 430;
            mapEditor.Multiline = true;
            mapEditor.ScrollBars = ScrollBars.Both;
            mapEditor.AcceptsReturn = true;
            mapEditor.AcceptsTab = false;
            mapEditor.WordWrap = false;
            mapEditor.Font = new Font("Consolas", 10);
            mapEditor.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;
            mapTab.Controls.Add(mapEditor);

            saveMapButton = new GazelyButton();
            saveMapButton.Text = "Save + Reload Map";
            saveMapButton.Left = 20;
            saveMapButton.Top = 515;
            saveMapButton.Width = 155;
            saveMapButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            saveMapButton.Click += delegate { SaveMapFromEditor(); };
            mapTab.Controls.Add(saveMapButton);

            reloadMapButton = new GazelyButton();
            reloadMapButton.Text = "Reload from File";
            reloadMapButton.Left = 190;
            reloadMapButton.Top = 515;
            reloadMapButton.Width = 135;
            reloadMapButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            reloadMapButton.Click += delegate { LoadVersionMap(); };
            mapTab.Controls.Add(reloadMapButton);

            BuildAudioTab(audioTab);
            BuildGazelyInterface(tabs,controlTab,audioTab,mapTab,integrationsTab);
            RestoreWorkspacePreferences();

            EnsureDefaultMapFile();
            LoadVersionMap();
            RefreshConnection();

            connectionTimer = new System.Windows.Forms.Timer();
            connectionTimer.Interval = 2000;
            connectionTimer.Tick += delegate { RefreshConnection(); };
            connectionTimer.Start();

            FormClosing += delegate
            {
                closingApplication=true;
                if(miniOverlay!=null && !miniOverlay.IsDisposed)miniOverlay.Shutdown();
                SaveWorkspacePreferences();
                StopListening();
                whisperEngine.Stop();
                lock(quotationJobs){quotationJobs.Clear();}
                semanticSearch.Dispose();
                evidenceTip.Dispose();
            };

            Shown += delegate
            {
                string overlayEnabled;
                if(WorkspacePreferences.Load().TryGetValue("overlay.enabled",out overlayEnabled) && overlayEnabled=="True")BeginInvoke((MethodInvoker)delegate{OpenMiniOverlay();});
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        quotationIndex = new ScriptureQuoteIndex(Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,"corpus","kjv.tsv"));
                        AddLog("Offline quotation index ready: " + quotationIndex.Count + " verses. Suggestions require review.");
                        BeginInvoke((MethodInvoker)delegate{if(candidateSearchStatus!=null)candidateSearchStatus.Text="Quotation search ready";});
                    }
                    catch(Exception ex) { AddLog("Quotation search unavailable: " + ex.Message); }
                    try
                    {
                        chapterRecovery=new ChapterPronunciationRecovery();
                        AddLog("Offline chapter pronunciation candidates ready. All require review.");
                    }
                    catch(Exception ex) { AddLog("Chapter pronunciation recovery unavailable: "+ex.Message); }
                });
            };

            UpdateVersionStateLabel();

            AddLog("Gazely Phase 2 evaluation ready — unbiased local transcription, repetition protection; neural filter remains experimental and off.");
            AddLog("Version-list direct focus enabled.");
            AddLog("Version tracking: assume the configured KJV startup default on first attachment; unchanged versions leave the list untouched.");
            AddLog("If an operator manually changes the Bible version in EasyWorship, click 'Reset version tracking' before the next automated version change.");
            AddLog("Recommended first test: Test Version Only -> Preview Only -> SEND LIVE.");
        }

        private void BuildIntegrationsTab(TabPage tab)
        {
            tab.AutoScroll=true;
            FlowLayoutPanel layout=new FlowLayoutPanel();
            layout.Dock=DockStyle.Fill;layout.FlowDirection=FlowDirection.TopDown;
            layout.WrapContents=false;layout.AutoScroll=true;layout.Padding=new Padding(24);
            tab.Controls.Add(layout);
            Label heading=new Label();heading.Text="Presentation integrations";
            heading.Font=new Font("Segoe UI",20,FontStyle.Bold);heading.AutoSize=true;
            heading.Margin=new Padding(0,0,0,16);layout.Controls.Add(heading);
            Label intro=new Label();intro.Text="Choose where Gazely prepares and presents content. Enabling an integration never turns on Automatic Live.";
            intro.AutoSize=true;intro.MaximumSize=new Size(740,0);intro.Margin=new Padding(0,0,0,24);layout.Controls.Add(intro);
            easyWorshipEnabled=new GazelyToggle();easyWorshipEnabled.Text="EasyWorship 7 — bundled integration";
            easyWorshipEnabled.AutoSize=true;easyWorshipEnabled.Checked=destination.Enabled;
            layout.Controls.Add(easyWorshipEnabled);
            integrationStatus=new Label();integrationStatus.AutoSize=true;integrationStatus.MaximumSize=new Size(740,0);
            integrationStatus.Margin=new Padding(0,12,0,16);layout.Controls.Add(integrationStatus);
            Label setup=new Label();setup.AutoSize=true;setup.MaximumSize=new Size(740,0);
            setup.Text="Works locally with EasyWorship 7 on this PC. No adapter download is required. Open EasyWorship and check this PC's Bible Version Map before presenting.\r\n\r\nActive destination: EasyWorship 7. Scripture and same-chapter ranges are supported. Live-screen readback is not yet available; verify the output in EasyWorship.";
            layout.Controls.Add(setup);
            presentationStatus=new Label();presentationStatus.AutoSize=true;presentationStatus.MaximumSize=new Size(740,0);
            presentationStatus.Text="No presentation command sent this session.";presentationStatus.Margin=new Padding(0,24,0,12);
            layout.Controls.Add(presentationStatus);
            Button cancel=new GazelyButton();cancel.Text="Cancel pending presentation";cancel.AutoSize=true;
            cancel.Click+=delegate { destination.Cancel(); if(autoLiveCheck!=null) autoLiveCheck.Checked=false; if(autoPreviewCheck!=null) autoPreviewCheck.Checked=false; presentationStatus.Text="Cancellation requested. Check the output if a command was already sent."; };
            layout.Controls.Add(cancel);
            Label planned=new Label();planned.AutoSize=true;planned.MaximumSize=new Size(740,0);planned.Margin=new Padding(0,28,0,0);
            planned.Text="Planned integrations\r\nEasyWorship 2009 · ProPresenter · FreeShow · BibleShow\r\nThese adapters are not available to enable yet. Each will use its supported API or automation through the same intelligence layer.";
            layout.Controls.Add(planned);
            easyWorshipEnabled.CheckedChanged+=delegate
            {
                bool enabled=easyWorshipEnabled.Checked;
                destination.SetEnabled(enabled);
                if(autoLiveCheck!=null) autoLiveCheck.Checked=false;
                if(autoPreviewCheck!=null) autoPreviewCheck.Checked=false;
                scriptureDetector.SetDisplayedContext(null,false);
                try { IntegrationPreferences.Save(integrationPath,enabled); }
                catch(Exception ex) { AddLog("Could not save integration preference: "+ex.Message); integrationStatus.Text="Preference changed for this session, but could not be saved."; return; }
                RefreshConnection();
            };
        }

        private void SubmitPresentation(ScriptureRequest request,bool changeVersion,string version,int index,bool goLive)
        {
            if(presentationRunning) { AddLog("A presentation command is already running."); return; }
            if(!destination.Enabled) { presentationStatus.Text="Enable EasyWorship 7 in Settings / Integrations first."; AddLog(presentationStatus.Text); return; }
            PresentationIntent intent=new PresentationIntent(Guid.NewGuid().ToString("N"),request,goLive,changeVersion,version,index);
            presentationRunning=true;SetBusy(true);
            // Let the existing adapter reach its controls even when the overlay
            // was positioned above one of EasyWorship's automation targets.
            if(miniOverlay!=null && !miniOverlay.IsDisposed)miniOverlay.Hide();
            presentationStatus.Text=(goLive?"Sending ":"Preparing ")+request.ReferenceText+"…";
            Thread worker=new Thread(delegate()
            {
                PresentationResult result=destination.Execute(intent);
                AddLog(result.Detail);
                try
                {
                    if(IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        presentationRunning=false;SetBusy(false);
                        presentationStatus.Text=result.Detail;
                        RecordCandidateAttempt(request,version,result);
                        UpdateVersionStateLabel();RefreshConnection();
                    });
                }
                catch(InvalidOperationException) { }
            });
            worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
        }

        private void BuildAudioTab(TabPage audioTab)
        {
            audioTab.AutoScroll = true;

            Label title = new Label();
            title.Text = "Listen & Detect Scriptures";
            title.Font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            title.AutoSize = true;
            title.Left = 22;
            title.Top = 18;
            audioTab.Controls.Add(title);

            Label mode = new Label();
            mode.Text =
                "Choose what to listen to, then the app will hear speech and detect Bible references. Your automation preference is remembered.";
            mode.AutoSize = true;
            mode.Left = 24;
            mode.Top = 53;
            mode.ForeColor = GazelyTheme.Warning;
            mode.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
            audioTab.Controls.Add(mode);

            Label sourceLabel = new Label();
            sourceLabel.Text = "What should I listen to?";
            sourceLabel.AutoSize = true;
            sourceLabel.Left = 24;
            sourceLabel.Top = 88;
            audioTab.Controls.Add(sourceLabel);

            audioDeviceCombo = new ComboBox();
            audioDeviceCombo.Left = 24;
            audioDeviceCombo.Top = 108;
            audioDeviceCombo.Width = 330;
            audioDeviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            audioTab.Controls.Add(audioDeviceCombo);

            refreshAudioButton = new GazelyButton();
            refreshAudioButton.Text = "Refresh";
            refreshAudioButton.Left = 365;
            refreshAudioButton.Top = 106;
            refreshAudioButton.Width = 120;
            refreshAudioButton.Click += delegate { PopulateAudioDevices(); };
            audioTab.Controls.Add(refreshAudioButton);

            Label engineLabel = new Label();
            engineLabel.Text = "Speech recognition";
            engineLabel.AutoSize = true;
            engineLabel.Left = 510;
            engineLabel.Top = 88;
            audioTab.Controls.Add(engineLabel);

            speechEngineCombo = new ComboBox();
            speechEngineCombo.Left = 510;
            speechEngineCombo.Top = 108;
            speechEngineCombo.Width = 205;
            speechEngineCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            speechEngineCombo.Items.Add("Local AI (recommended)");
            speechEngineCombo.Items.Add("Windows speech (backup)");
            speechEngineCombo.Items.Add("Moonshine Medium (experimental)");
            speechEngineCombo.Items.Add("Moonshine Small (experimental)");
            speechEngineCombo.Items.Add("Nemotron English (experimental)");
            speechEngineCombo.SelectedIndex = 0;
            speechEngineCombo.SelectedIndexChanged += delegate
            {
                UpdateEngineVisibility();
            };
            audioTab.Controls.Add(speechEngineCombo);

            Label modelLabel = new Label();
            modelLabel.Text = "Speed / accuracy";
            modelLabel.AutoSize = true;
            modelLabel.Left = 730;
            modelLabel.Top = 88;
            audioTab.Controls.Add(modelLabel);

            whisperModelCombo = new ComboBox();
            whisperModelCombo.Left = 730;
            whisperModelCombo.Top = 108;
            whisperModelCombo.Width = 185;
            whisperModelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            whisperModelCombo.Items.Add("Fastest");
            whisperModelCombo.Items.Add("Balanced");
            whisperModelCombo.Items.Add("More accurate");
            whisperModelCombo.SelectedIndex = File.Exists(whisperEngine.GetModelPath("base.en")) ? 1 : 0;
            audioTab.Controls.Add(whisperModelCombo);

            recognizerCombo = new ComboBox();
            recognizerCombo.Left = 730;
            recognizerCombo.Top = 108;
            recognizerCombo.Width = 250;
            recognizerCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            recognizerCombo.Visible = false;
            audioTab.Controls.Add(recognizerCombo);

            Label vadLabel = new Label();
            vadLabel.Text = "Voice pickup";
            vadLabel.AutoSize = true;
            vadLabel.Left = 24;
            vadLabel.Top = 151;
            audioTab.Controls.Add(vadLabel);

            vadSensitivityCombo = new ComboBox();
            vadSensitivityCombo.Left = 24;
            vadSensitivityCombo.Top = 171;
            vadSensitivityCombo.Width = 190;
            vadSensitivityCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            vadSensitivityCombo.Items.Add("Pick up quiet speech");
            vadSensitivityCombo.Items.Add("Normal");
            vadSensitivityCombo.Items.Add("Ignore more room noise");
            vadSensitivityCombo.SelectedIndex = 1;
            vadSensitivityCombo.SelectedIndexChanged += delegate
            {
                ApplyVadSensitivity();
            };
            audioTab.Controls.Add(vadSensitivityCombo);

            startListeningButton = new GazelyButton();
            startListeningButton.Text = "START LISTENING";
            startListeningButton.Left = 235;
            startListeningButton.Top = 164;
            startListeningButton.Width = 160;
            startListeningButton.Height = 38;
            startListeningButton.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
            startListeningButton.Click += delegate { StartListening(); };
            audioTab.Controls.Add(startListeningButton);

            stopListeningButton = new GazelyButton();
            stopListeningButton.Text = "STOP";
            stopListeningButton.Left = 405;
            stopListeningButton.Top = 164;
            stopListeningButton.Width = 90;
            stopListeningButton.Height = 38;
            stopListeningButton.Enabled = false;
            stopListeningButton.Click += delegate { StopListening(); };
            audioTab.Controls.Add(stopListeningButton);

            audioStatusLabel = MakeBadge("STOPPED", 515, 169, 130, Color.FromArgb(85, 89, 96));
            audioTab.Controls.Add(audioStatusLabel);

            vadStatusLabel = MakeBadge("NO VOICE", 655, 169, 130, Color.FromArgb(85, 89, 96));
            audioTab.Controls.Add(vadStatusLabel);

            whisperStatusLabel = MakeBadge("AI IDLE", 795, 169, 165, Color.FromArgb(85, 89, 96));
            audioTab.Controls.Add(whisperStatusLabel);

            Label activeSourceCaption = new Label();
            activeSourceCaption.Text = "Listening to:";
            activeSourceCaption.AutoSize = true;
            activeSourceCaption.Left = 24;
            activeSourceCaption.Top = 220;
            audioTab.Controls.Add(activeSourceCaption);

            audioSourceActiveLabel = new Label();
            audioSourceActiveLabel.Text = "—";
            audioSourceActiveLabel.AutoEllipsis = true;
            audioSourceActiveLabel.Left = 110;
            audioSourceActiveLabel.Top = 220;
            audioSourceActiveLabel.Width = 620;
            audioTab.Controls.Add(audioSourceActiveLabel);

            latencyLabel = new Label();
            latencyLabel.Text = "Response time: —";
            latencyLabel.AutoSize = true;
            latencyLabel.Left = 790;
            latencyLabel.Top = 220;
            audioTab.Controls.Add(latencyLabel);

            audioMeter = new AudioLevelMeter();
            audioMeter.Left = 24;
            audioMeter.Top = 246;
            audioMeter.Width = 700;
            audioMeter.Height = 38;
            audioTab.Controls.Add(audioMeter);

            audioDbLabel = new Label();
            audioDbLabel.Text = "RMS: -∞ dBFS   Peak: -∞ dBFS";
            audioDbLabel.AutoSize = true;
            audioDbLabel.Left = 24;
            audioDbLabel.Top = 291;
            audioTab.Controls.Add(audioDbLabel);

            signalQualityLabel = MakeBadge("NO SIGNAL", 380, 288, 150, Color.FromArgb(85, 89, 96));
            audioTab.Controls.Add(signalQualityLabel);

            confidenceLabel = new Label();
            confidenceLabel.Text = "Last result: —";
            confidenceLabel.AutoSize = true;
            confidenceLabel.Left = 555;
            confidenceLabel.Top = 293;
            audioTab.Controls.Add(confidenceLabel);

            recognizerStatusLabel = new Label();
            recognizerStatusLabel.Text = "Speech recognition: idle";
            recognizerStatusLabel.AutoSize = true;
            recognizerStatusLabel.Left = 24;
            recognizerStatusLabel.Top = 322;
            recognizerStatusLabel.ForeColor = GazelyTheme.Muted;
            audioTab.Controls.Add(recognizerStatusLabel);

            earlyResultsCheck = new GazelyToggle();
            earlyResultsCheck.Text = "Show scripture candidates while speaking";
            earlyResultsCheck.SetBounds(615, 318, 330, 25);
            earlyResultsCheck.Checked = true;
            audioTab.Controls.Add(earlyResultsCheck);

            hypothesisLabel = new Label();
            hypothesisLabel.Text = "Live text: —";
            hypothesisLabel.Left = 24;
            hypothesisLabel.Top = 346;
            hypothesisLabel.Width = 920;
            hypothesisLabel.Height = 22;
            hypothesisLabel.AutoEllipsis = true;
            hypothesisLabel.ForeColor = GazelyTheme.Muted;
            audioTab.Controls.Add(hypothesisLabel);

            Label transcriptLabel = new Label();
            transcriptLabel.Text = "What I heard";
            transcriptLabel.AutoSize = true;
            transcriptLabel.Left = 24;
            transcriptLabel.Top = 377;
            audioTab.Controls.Add(transcriptLabel);

            clearTranscriptButton = new GazelyButton();
            clearTranscriptButton.Text = "Clear";
            clearTranscriptButton.Left = 870;
            clearTranscriptButton.Top = 371;
            clearTranscriptButton.Width = 70;
            clearTranscriptButton.Click += delegate
            {
                liveTranscript.Clear();transcriptTextBox.Clear();
                hypothesisLabel.Text = "Live text: —";
                confidenceLabel.Text = "Last result: —";
                ClearDetectionUi();
            };
            audioTab.Controls.Add(clearTranscriptButton);

            transcriptTextBox = new TextBox();
            transcriptTextBox.Left = 24;
            transcriptTextBox.Top = 400;
            transcriptTextBox.Width = 916;
            transcriptTextBox.Height = 95;
            transcriptTextBox.Multiline = true;
            transcriptTextBox.ScrollBars = ScrollBars.Vertical;
            transcriptTextBox.ReadOnly = true;
            transcriptTextBox.Font = new Font("Segoe UI", 10);
            audioTab.Controls.Add(transcriptTextBox);

            GroupBox detectedGroup = new GroupBox();
            detectedGroup.Text = "DETECTED SCRIPTURE";
            detectedGroup.Left = 24;
            detectedGroup.Top = 515;
            detectedGroup.Width = 916;
            detectedGroup.Height = 165;
            audioTab.Controls.Add(detectedGroup);

            detectedReferenceLabel = new Label();
            detectedReferenceLabel.Text = "No scripture detected yet";
            detectedReferenceLabel.Left = 18;
            detectedReferenceLabel.Top = 28;
            detectedReferenceLabel.Width = 320;
            detectedReferenceLabel.Height = 32;
            detectedReferenceLabel.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
            detectedGroup.Controls.Add(detectedReferenceLabel);

            detectedVersionLabel = new Label();
            detectedVersionLabel.Text = "Version: —";
            detectedVersionLabel.Left = 18;
            detectedVersionLabel.Top = 66;
            detectedVersionLabel.Width = 190;
            detectedGroup.Controls.Add(detectedVersionLabel);

            detectedConfidenceLabel = new Label();
            detectedConfidenceLabel.Text = "Confidence: —";
            detectedConfidenceLabel.Left = 220;
            detectedConfidenceLabel.Top = 66;
            detectedConfidenceLabel.Width = 180;
            detectedGroup.Controls.Add(detectedConfidenceLabel);

            detectedReasonLabel = new Label();
            detectedReasonLabel.Text = "How it matched: —";
            detectedReasonLabel.Left = 18;
            detectedReasonLabel.Top = 91;
            detectedReasonLabel.Width = 430;
            detectedGroup.Controls.Add(detectedReasonLabel);

            detectedSourceLabel = new Label();
            detectedSourceLabel.Text = "Heard: —";
            detectedSourceLabel.Left = 18;
            detectedSourceLabel.Top = 116;
            detectedSourceLabel.Width = 500;
            detectedSourceLabel.Height = 34;
            detectedSourceLabel.AutoEllipsis = true;
            detectedSourceLabel.ForeColor = GazelyTheme.Muted;
            detectedGroup.Controls.Add(detectedSourceLabel);

            previewDetectedButton = new GazelyButton();
            previewDetectedButton.Text = "PREVIEW IN EASYWORSHIP";
            previewDetectedButton.Left = 535;
            previewDetectedButton.Top = 30;
            previewDetectedButton.Width = 160;
            previewDetectedButton.Enabled = false;
            previewDetectedButton.Click += delegate { RunDetected(false); };
            detectedGroup.Controls.Add(previewDetectedButton);

            liveDetectedButton = new GazelyButton();
            liveDetectedButton.Text = "SHOW LIVE";
            liveDetectedButton.Left = 705;
            liveDetectedButton.Top = 30;
            liveDetectedButton.Width = 180;
            liveDetectedButton.Enabled = false;
            liveDetectedButton.Click += delegate { RunDetected(true); };
            detectedGroup.Controls.Add(liveDetectedButton);

            autoPreviewCheck = new GazelyToggle();
            autoPreviewCheck.Text = "Automatically prepare clear scripture detections";
            autoPreviewCheck.Left = 535;
            autoPreviewCheck.Top = 78;
            autoPreviewCheck.Width = 270;
            detectedGroup.Controls.Add(autoPreviewCheck);

            autoLiveCheck = new GazelyToggle();
            autoLiveCheck.Text = "AUTOMATIC LIVE MODE";
            autoLiveCheck.Left = 535;
            autoLiveCheck.Top = 106;
            autoLiveCheck.Width = 180;
            autoLiveCheck.ForeColor = GazelyTheme.Error;
            autoLiveCheck.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
            autoLiveCheck.CheckedChanged += delegate
            {
                if(restoringPreferences)return;
                if (autoLiveCheck.Checked)
                {
                    DialogResult result = MessageBox.Show(
                        this,
                        "Automatic Live mode can put a clearly detected scripture on screen without asking you first.\r\n\r\n" +
                        "Keep this OFF until you trust the detections during real sermon audio. Turn it on now?",
                        "Automatic Live mode",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        autoLiveCheck.Checked = false;
                        return;
                    }

                    autoPreviewCheck.Checked = false;
                }
            };
            detectedGroup.Controls.Add(autoLiveCheck);

            PopulateAudioDevices();
            PopulateRecognizers();
            ApplyVadSensitivity();

            utteranceSegmenter.ChunkReady += OnSpeechChunk;
            utteranceSegmenter.SpeechStateChanged += OnVadSpeechStateChanged;

            UpdateEngineVisibility();
            UpdateAudioControls();
            ClearDetectionUi();
        }

        private Label MakeBadge(
            string text,
            int left,
            int top,
            int width,
            Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Left = left;
            label.Top = top;
            label.Width = width;
            label.Height = 26;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.BackColor = color;
            label.ForeColor = Color.White;
            label.Font = new Font(Font.FontFamily, 8, FontStyle.Bold);
            return label;
        }

        private void UpdateEngineVisibility()
        {
            if (speechEngineCombo == null)
                return;

            bool whisper =
                speechEngineCombo.SelectedIndex <= 0;

            whisperModelCombo.Visible = whisper;
            recognizerCombo.Visible = speechEngineCombo.SelectedIndex==1;
            if(autoLiveCheck!=null)autoLiveCheck.Enabled=speechEngineCombo.SelectedIndex<2;
            if(autoPreviewCheck!=null)autoPreviewCheck.Enabled=speechEngineCombo.SelectedIndex<2;

            if (!audioListening)
            {
                whisperStatusLabel.Text =
                    whisper
                        ? "AI READY?"
                        : speechEngineCombo.SelectedIndex==4 ? "NEMOTRON" : speechEngineCombo.SelectedIndex>=2 ? "MOONSHINE" : "WINDOWS STT";
                whisperStatusLabel.BackColor =
                    Color.FromArgb(85, 89, 96);
            }
        }

        private void ApplyVadSensitivity()
        {
            if (vadSensitivityCombo == null)
                return;

            if (vadSensitivityCombo.SelectedIndex == 0)
                utteranceSegmenter.ThresholdDb = -56.0;
            else if (vadSensitivityCombo.SelectedIndex == 2)
                utteranceSegmenter.ThresholdDb = -42.0;
            else
                utteranceSegmenter.ThresholdDb = -50.0;
        }

        private void ClearDetectionUi()
        {
            referenceRefinement.Reset();
            activeReference.Release();quoteSwitchRevision=-1;quoteScopeBook=null;quoteScopeChapter=0;
            operatorDraft=false;SetDraft(string.Empty,0,0,0);UpdateVerseExcerpt(null);
            lastChapterRecovery=null;lastChapterRecoverySource=string.Empty;
            ++interpretationRevision;
            quotationContext.Reset();
            lastQuotationReference = string.Empty;
            lastDetection = null;
            scriptureDetector.Reset();
            lastAutoSignature = string.Empty;
            lastAutoActionUtc = DateTime.MinValue;
            RefreshPassageAlternatives();

            if (detectedReferenceLabel == null)
                return;

            detectedReferenceLabel.Text =
                "No scripture detected yet";
            detectedReferenceLabel.ForeColor =
                GazelyTheme.Muted;
            detectedVersionLabel.Text =
                "Version: —";
            detectedConfidenceLabel.Text =
                "Confidence: —";
            detectedReasonLabel.Text =
                "How it matched: —";
            detectedSourceLabel.Text =
                "Heard: —";
            previewDetectedButton.Enabled = false;
            liveDetectedButton.Enabled = false;
        }

        private void PopulateAudioDevices()
        {
            if (audioListening)
                return;

            string previous =
                audioDeviceCombo.SelectedItem == null
                    ? string.Empty
                    : audioDeviceCombo.SelectedItem.ToString();

            audioDeviceCombo.Items.Clear();

            try
            {
                List<AudioDeviceInfo> devices =
                    WaveInRecorder.GetDevices();

                foreach (AudioDeviceInfo device in devices)
                    audioDeviceCombo.Items.Add(device);

                if (audioDeviceCombo.Items.Count > 0)
                {
                    int select = 0;

                    if (!string.IsNullOrEmpty(previous))
                    {
                        for (int i = 0;
                             i < audioDeviceCombo.Items.Count;
                             i++)
                        {
                            if (string.Equals(
                                audioDeviceCombo.Items[i].ToString(),
                                previous,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                select = i;
                                break;
                            }
                        }
                    }

                    audioDeviceCombo.SelectedIndex = select;
                }
            }
            catch (Exception ex)
            {
                AddLog(
                    "Audio device enumeration error: " +
                    ex.Message);
            }
        }

        private void PopulateRecognizers()
        {
            if (audioListening)
                return;

            recognizerCombo.Items.Clear();

            try
            {
                foreach (RecognizerInfo info in
                    SpeechRecognitionEngine.InstalledRecognizers())
                {
                    RecognizerItem item = new RecognizerItem();
                    item.Id = info.Id;
                    item.Description = info.Description;
                    item.Culture =
                        info.Culture == null
                            ? string.Empty
                            : info.Culture.Name;

                    recognizerCombo.Items.Add(item);
                }

                if (recognizerCombo.Items.Count > 0)
                    recognizerCombo.SelectedIndex = 0;
                else
                    recognizerStatusLabel.Text =
                        "Recognizer: no offline Windows speech recognizer installed";
            }
            catch (Exception ex)
            {
                recognizerStatusLabel.Text =
                    "Recognizer enumeration failed: " +
                    ex.Message;
            }
        }

        private void StartListening()
        {
            if (audioListening || startingListening)
                return;

            AudioDeviceInfo device =
                audioDeviceCombo.SelectedItem as AudioDeviceInfo;

            if (device == null)
            {
                MessageBox.Show(
                    this,
                    "Select an audio input source first.",
                    "Audio source required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            bool useWhisper =
                speechEngineCombo.SelectedIndex <= 0;
            useNativeStreaming=speechEngineCombo.SelectedIndex>=2;

            try
            {
                startingListening = true;
                listeningSession++;
                lastFinalUtteranceId = 0;
                useLocalAi = useWhisper;
                UpdateAudioControls();
                ApplyVadSensitivity();
                utteranceSegmenter.Reset();
                ClearDetectionUi();
                utteranceSegmenter.EnablePartials = earlyResultsCheck.Checked;
                utteranceSegmenter.PartialIntervalMs = 1200;

                if (useWhisper)
                {
                    string profile =
                        whisperModelCombo.SelectedItem == null
                            ? "Fastest"
                            : whisperModelCombo.SelectedItem.ToString();

                    string model =
                        profile == "More accurate"
                            ? "small.en"
                            : profile == "Balanced"
                                ? "base.en"
                                : "tiny.en";

                    // Tune endpointing with the selected speed profile.
                    if (profile == "Fastest")
                    {
                        utteranceSegmenter.SilenceToFinalizeMs = 360;
                        utteranceSegmenter.MinSpeechMs = 160;
                    }
                    else if (profile == "Balanced")
                    {
                        utteranceSegmenter.SilenceToFinalizeMs = 400;
                        utteranceSegmenter.MinSpeechMs = 180;
                    }
                    else
                    {
                        utteranceSegmenter.SilenceToFinalizeMs = 440;
                        utteranceSegmenter.MinSpeechMs = 220;
                    }

                    SetWhisperStatus(
                        "AI STARTING",
                        Color.FromArgb(198, 135, 31));

                    recognizerStatusLabel.Text =
                        "Engine: loading local speech model...";

                    AddLog(
                        "Starting local speech model: " +
                        model);

                    whisperEngine = new WhisperServerEngine();
                    whisperEngine.Start(
                        model,
                        AddLog);

                    SetWhisperStatus(
                        "AI READY",
                        Color.FromArgb(35, 145, 82));
                }
                else if(useNativeStreaming)
                {
                    int generation=listeningSession;
                    streamingEngine=new StreamingSpeechEngine();
                    string streamingName=speechEngineCombo.SelectedIndex==4?"Nemotron":"Moonshine";
                    long lastProgressLog=0;
                    streamingEngine.Progress=delegate(long received,long acknowledged,int queued,double turnaround){
                        long now=Stopwatch.GetTimestamp();if((now-lastProgressLog)<5*Stopwatch.Frequency)return;lastProgressLog=now;
                        AddLog(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0} stream: received={1:0.0}s acknowledged={2:0.0}s backlog={3:0.0}ms queue={4:0.0}ms ack roundtrip={5:0.0}ms",
                            streamingName,received/16000.0,acknowledged/16000.0,Math.Max(0,received-acknowledged)/16.0,queued/32.0,turnaround));
                    };
                    streamingEngine.Transcript=delegate(string text,SpeechChunk chunk,bool final,double delay){
                        if(final)OnFinalTranscript(text,1.0,streamingName,delay,generation,chunk);
                        else OnPartialTranscript(text,chunk,delay);
                    };
                    streamingEngine.Error=delegate(string message){
                        AddLog(streamingName+" error: "+message);
                        if(!IsDisposed && IsHandleCreated)BeginInvoke((MethodInvoker)delegate{
                            if(generation!=listeningSession)return;
                            drainingStreaming=true;StopListening();drainingStreaming=false;
                            recognizerStatusLabel.Text=streamingName+" stopped: "+message;SetWhisperStatus("AI ERROR",Color.FromArgb(190,48,48));
                        });
                    };
                    streamingEngine.Start(speechEngineCombo.SelectedIndex==4?"nemotron":speechEngineCombo.SelectedIndex==3?"small":"medium",generation,AddLog);
                    SetWhisperStatus("AI READY",Color.FromArgb(35,145,82));
                }
                else
                {
                    speechStream =
                        new BlockingAudioStream();
                }

                audioRecorder =
                    new WaveInRecorder(
                        device.Id,
                        16000,
                        1);

                int recorderSession = listeningSession;
                audioRecorder.DataAvailable += delegate(byte[] data)
                {
                    if (recorderSession == listeningSession) OnAudioData(data);
                };

                audioRecorder.LevelChanged +=
                    OnAudioLevel;

                audioListening = true;
                audioRecorder.Start();

                audioSourceActiveLabel.Text =
                    device.Name +
                    "  •  16 kHz / mono / 16-bit";

                SetAudioStatus(
                    "LISTENING",
                    Color.FromArgb(35, 145, 82));

                AddLog(
                    "Audio source started: " +
                    device.Name);

                if (!useWhisper && !useNativeStreaming)
                {
                    RecognizerItem recognizer =
                        recognizerCombo.SelectedItem as RecognizerItem;

                    if (recognizer == null)
                        throw new InvalidOperationException(
                            "No Windows speech recognizer is available.");

                    Thread.Sleep(180);

                    speechRecognizer =
                        new LiveSpeechRecognizer();

                    speechRecognizer.FinalRecognized +=
                        OnSpeechRecognized;

                    speechRecognizer.Hypothesized +=
                        OnSpeechHypothesized;

                    speechRecognizer.StateChanged +=
                        OnRecognizerStateChanged;

                    speechRecognizer.Error +=
                        OnRecognizerError;

                    speechRecognizer.Start(
                        recognizer.Id,
                        speechStream);

                    recognizerStatusLabel.Text =
                        "Speech recognition: Windows backup — " +
                        recognizer.ToString();

                    SetWhisperStatus(
                        "WINDOWS STT",
                        Color.FromArgb(70, 100, 150));
                }
                else
                {
                    recognizerStatusLabel.Text =
                        useNativeStreaming?streamingEngine.Name+" native streaming — experimental; manual Present only":"Speech recognition: Local AI ready";
                    hypothesisLabel.Text =
                        "Live text: waiting for someone to speak...";
                }

                AddLog(
                    "Audio listening active: " +
                    device.Name);

                UpdateAudioControls();
            }
            catch (Exception ex)
            {
                StopListening();

                MessageBox.Show(
                    this,
                    "Could not start audio/speech:\r\n\r\n" +
                    ex.GetType().Name +
                    ": " +
                    ex.Message,
                    "Audio / Speech error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                AddLog(
                    "Audio/speech start error: " +
                    ex.GetType().Name +
                    ": " +
                    ex.Message);
            }
            finally
            {
                startingListening = false;
                UpdateAudioControls();
            }
        }

        private void StopListening()
        {
            if(useNativeStreaming && audioListening && streamingEngine!=null && !drainingStreaming && !closingApplication)
            {
                drainingStreaming=true;startingListening=true;
                if(audioRecorder!=null)audioRecorder.Stop();
                UpdateAudioControls();stopListeningButton.Enabled=false;
                recognizerStatusLabel.Text="Finishing streaming transcript…";
                StreamingSpeechEngine engine=streamingEngine;
                ThreadPool.QueueUserWorkItem(delegate{
                    string error=null;try{engine.Finish();}catch(Exception ex){error=ex.Message;AddLog("Streaming flush: "+error);}
                    if(!IsDisposed && IsHandleCreated)BeginInvoke((MethodInvoker)delegate{
                        StopListening();drainingStreaming=false;startingListening=false;UpdateAudioControls();
                        if(error!=null)recognizerStatusLabel.Text=error;
                    });
                });return;
            }
            destination.Cancel();
            bool wasListening = audioListening;
            audioListening = false;
            liveTranscript.DiscardPartial();transcriptTextBox.Text=liveTranscript.Text;
            referenceRefinement.Reset();activeReference.Release();quoteSwitchRevision=-1;
            quoteScopeBook=null;quoteScopeChapter=0;lastSpeechDetectionUtc=DateTime.MinValue;
            listeningSession++;
            if (audioRecorder != null)
            {
                try
                {
                    audioRecorder.DataAvailable -=
                        OnAudioData;
                    audioRecorder.LevelChanged -=
                        OnAudioLevel;
                    audioRecorder.Stop();
                    audioRecorder.Dispose();
                }
                catch { }

                audioRecorder = null;
            }

            utteranceSegmenter.Reset();

            if (speechStream != null)
            {
                try { speechStream.Complete(); }
                catch { }
            }

            if (speechRecognizer != null)
            {
                try
                {
                    speechRecognizer.FinalRecognized -=
                        OnSpeechRecognized;
                    speechRecognizer.Hypothesized -=
                        OnSpeechHypothesized;
                    speechRecognizer.StateChanged -=
                        OnRecognizerStateChanged;
                    speechRecognizer.Error -=
                        OnRecognizerError;

                    speechRecognizer.Stop();
                    speechRecognizer.Dispose();
                }
                catch { }

                speechRecognizer = null;
            }

            if (speechStream != null)
            {
                try { speechStream.Dispose(); }
                catch { }

                speechStream = null;
            }

            whisperEngine.Stop();
            if(streamingEngine!=null){streamingEngine.Dispose();streamingEngine=null;}

            lock (whisperQueue)
            {
                whisperQueue.Clear();
                whisperWorkerRunning = false;
            }


            if (!IsDisposed && audioStatusLabel != null)
            {
                SetAudioStatus(
                    "STOPPED",
                    Color.FromArgb(85, 89, 96));

                SetWhisperStatus(
                    "AI IDLE",
                    Color.FromArgb(85, 89, 96));

                SetVadStatus(
                    false);

                audioSourceActiveLabel.Text = "—";
                recognizerStatusLabel.Text =
                    "Speech recognition: idle";
                hypothesisLabel.Text =
                    "Live text: —";
                latencyLabel.Text =
                    "Response time: —";

                audioMeter.SetLevels(-90, -90);
                if(workspaceAudioMeter!=null) workspaceAudioMeter.SetLevels(-90,-90);
                audioDbLabel.Text =
                    "RMS: -∞ dBFS   Peak: -∞ dBFS";

                SetSignalQuality(
                    "NO SIGNAL",
                    Color.FromArgb(85, 89, 96));

                UpdateAudioControls();
            }

            if (wasListening)
                AddLog("Audio listening stopped.");
            if(useNativeStreaming && !closingApplication)recognizerStatusLabel.Text="Streaming experiment — manual Present only; choose Local AI to restore automatic presentation.";
        }

        private void OnAudioData(byte[] data)
        {
            if (!audioListening)
                return;

            if(useNativeStreaming)
            {
                if(streamingEngine!=null)streamingEngine.AddAudio(data);
            }
            else if (useLocalAi)
            {
                utteranceSegmenter.Process(data);
            }
            else
            {
                BlockingAudioStream stream =
                    speechStream;

                if (stream != null)
                    stream.WriteChunk(data);
            }
        }

        private void OnSpeechChunk(SpeechChunk chunk)
        {
            if (!audioListening) return;
            chunk.Session = listeningSession;
            lock (whisperQueue)
            {
                // Final work supersedes snapshots of the same utterance.
                // Only one newest partial can wait, and finals have priority.
                whisperQueue.RemoveAll(delegate(SpeechChunk queued)
                {
                    return !queued.IsFinal || queued.Session != chunk.Session;
                });
                if (!chunk.IsFinal && whisperQueue.Count > 0) return;
                whisperQueue.Add(chunk);
                while (whisperQueue.Count > 2)
                {
                    whisperQueue.RemoveAt(0);
                    AddLog("Speech backlog: dropped oldest queued audio to stay current.");
                }
                if (whisperWorkerRunning) return;
                whisperWorkerRunning = true;
            }
            int session = chunk.Session;
            WhisperServerEngine engine = whisperEngine;
            ThreadPool.QueueUserWorkItem(delegate { WhisperWorkerLoop(session, engine); });
        }

        private void WhisperWorkerLoop(int session, WhisperServerEngine engine)
        {
            SpeechChunk cachedChunk = null;
            string cachedText = null;
            while (session == listeningSession && audioListening)
            {
                SpeechChunk chunk;
                lock (whisperQueue)
                {
                    if (session != listeningSession) return;
                    if (whisperQueue.Count == 0)
                    {
                        chunk=referenceRefinement.Take(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);
                        if(chunk==null){whisperWorkerRunning = false;return;}
                    }
                    else {chunk = whisperQueue[0]; whisperQueue.RemoveAt(0);}
                }
                try
                {
                    long started = Stopwatch.GetTimestamp();
                    if(chunk.IsRefinement)OnRefinementStarted(chunk);
                    bool reusable = chunk.IsFinal && cachedChunk != null &&
                        cachedChunk.UtteranceId == chunk.UtteranceId &&
                        cachedChunk.LastVoiceTicks == chunk.LastVoiceTicks && cachedChunk.TailSilenceMs >= 80;
                    string text = reusable ? cachedText : engine.Transcribe(chunk.Pcm, SpeechVocabulary.Prompt);
                    long finished = Stopwatch.GetTimestamp();
                    double inferenceMs = (finished - started) * 1000.0 / Stopwatch.Frequency;
                    if (session != listeningSession || !audioListening) return;
                    if(!reusable)referenceRefinement.Observe(chunk.Pcm.Length/32.0,inferenceMs);
                    if(chunk.IsRefinement){OnRefinedTranscript(text,chunk,inferenceMs);continue;}
                    if (!reusable) lock (utteranceSegmenter)
                    {
                        // Back off snapshots on slower hardware rather than
                        // creating an ever-growing inference backlog.
                        utteranceSegmenter.PartialIntervalMs = (int)Math.Max(1200, Math.Min(6000, inferenceMs * 2));
                    }
                    if (!chunk.IsFinal) { cachedChunk = chunk; cachedText = text; }
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (chunk.IsFinal)
                            OnFinalTranscript(text.Trim(), 1.0, "Local Whisper", inferenceMs, session, chunk);
                        else
                            OnPartialTranscript(text.Trim(), chunk, inferenceMs);
                    }
                    else if (chunk.IsFinal)
                        AddLog("No speech text returned for utterance " + chunk.UtteranceId);
                }
                catch (Exception ex)
                {
                    if (session != listeningSession) return;
                    SetWhisperStatusSafe("AI ERROR", Color.FromArgb(190, 48, 48));
                    AddLog("Whisper inference error: " + ex.Message);
                }
            }
        }

        private void RequestReferenceRefinement(string text,SpeechChunk sourceChunk=null)
        {
            ScriptureDetection active=activeReference.Current;
            string anchor=operatorDraft || active==null?null:active.ReferenceText;
            referenceRefinement.UpdateIntent(anchor,listeningSession);
            if(!refinementCheck.Checked || !useLocalAi || !audioListening || active==null || operatorDraft)return;
            bool correction=Regex.IsMatch(text??string.Empty,@"\b(?:no|sorry|correction|i mean)\b",RegexOptions.IgnoreCase);
            bool uncertain=active.Chapter==0 || active.Verse==0 || active.RequiresReview || correction;
            if(!uncertain)return;
            double now=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;
            if(!referenceRefinement.Request(utteranceSegmenter.RecentAudio,now,sourceChunk==null?-1:sourceChunk.LastVoiceTicks*1000.0/Stopwatch.Frequency,correction))return;
            AddLog("Audio refinement requested for "+anchor+"; runs only within idle decoding budget.");
            int session=listeningSession;WhisperServerEngine engine=whisperEngine;
            lock(whisperQueue){if(whisperWorkerRunning)return;whisperWorkerRunning=true;}
            ThreadPool.QueueUserWorkItem(delegate{WhisperWorkerLoop(session,engine);});
        }

        private void OnRefinementStarted(SpeechChunk chunk)
        {
            if(IsDisposed || !IsHandleCreated)return;
            BeginInvoke((MethodInvoker)delegate{if(!operatorDraft && referenceRefinement.IsCurrent(chunk))detectedReasonLabel.Text="Checking reference…";});
        }

        private void OnRefinedTranscript(string text,SpeechChunk chunk,double inferenceMs)
        {
            if(IsDisposed || !IsHandleCreated)return;
            BeginInvoke((MethodInvoker)delegate{
                if(!audioListening || chunk.Session!=listeningSession || operatorDraft ||
                    !referenceRefinement.IsCurrent(chunk) || activeReference.Current==null ||
                    activeReference.Current.ReferenceText!=chunk.ReferenceAnchor)return;
                RenderActiveReference();
                if((Stopwatch.GetTimestamp()-chunk.LastVoiceTicks)*1000.0/Stopwatch.Frequency>4000){AddLog("Audio refinement exceeded four-second budget; result discarded.");return;}
                if(string.IsNullOrWhiteSpace(text))return;
                ScriptureDetection active=activeReference.Current;
                // Parse the richer audio independently; do not turn a borrowed
                // chapter into apparent fresh acoustic evidence.
                var parser=new ScriptureDetector();
                // A book correction may intentionally inherit existing numbers,
                // but it stays review-required; ordinary recovery starts unseeded.
                if(chunk.CorrectionRequested && active.Chapter>0 && active.Verse>0)parser.SeedActiveReference(active);
                ScriptureDetection d=parser.Feed(text);
                bool corrected=d!=null && chunk.CorrectionRequested && d.EvidenceKind=="correction";
                if(d==null || (!corrected && d.Book!=active.Book) || d.Chapter<=0 || d.Verse<=0)return;
                if(active.Chapter>0 && active.Verse>0 && !active.RequiresReview && !corrected)return;
                d.RequiresReview=true;d.EvidenceKind="audio-refinement";d.Confidence=Math.Min(d.Confidence,0.80);
                d.Reason="Rechecked surrounding audio; review before presenting";
                RecordSuggestion(d,false,chunk.Session+":refinement:"+chunk.ReferenceRevision+":"+chunk.AudioEndSample);
                activeReference.Confirm(d);scriptureDetector.SeedActiveReference(d);
                selectingHistory=true;try{ShowDetection(d);}finally{selectingHistory=false;}
                AddLog("Audio refinement: "+d.ReferenceText+"; samples="+chunk.AudioStartSample+"-"+chunk.AudioEndSample+"; inference_ms="+inferenceMs.ToString("F0"));
            });
        }

        private void OnPartialTranscript(string text, SpeechChunk chunk, double inferenceMs)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!audioListening || chunk.Session != listeningSession ||
                        chunk.UtteranceId <= lastFinalUtteranceId) return;
                    RenderTranscript(chunk.Session+":"+chunk.UtteranceId,text,false);
                    try
                    {
                        scriptureDetector.SeedActiveReference(activeReference.Context);
                        ScriptureDetection candidate = scriptureDetector.Preview(text,true);
                        // Native streaming revises numbers within a line. Anchor the
                        // book immediately, but let final text supply numeric context.
                        if(useNativeStreaming && candidate!=null){candidate=candidate.Snapshot();candidate.Chapter=0;candidate.Verse=0;candidate.VerseEnd=0;candidate.RequiresReview=true;}
                        bool wasProvisional=activeReference.Provisional!=null;
                        activeReference.PreserveSelectedPassage(lastDetection,candidate);
                        activeReference.Preview(candidate);
                        if(candidate!=null && candidate.Verse>0 && !operatorDraft)
                            RecordSuggestion(candidate,true,chunk.Session+":"+chunk.UtteranceId);
                        if(!operatorDraft)RenderActiveReference(wasProvisional);
                        RequestReferenceRefinement(text,chunk);
                        RefreshPassageAlternatives();
                    }
                    catch (Exception ex) { AddLog("Partial parser error: " + ex.Message); }
                });
            }
            catch (InvalidOperationException) { }
        }
        private void OnVadSpeechStateChanged(
            bool speech,
            double rmsDb)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    SetVadStatus(speech);


                });
            }
            catch { }
        }

        private void OnAudioLevel(
            double rmsDb,
            double peakDb)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            DateTime now = DateTime.UtcNow;

            if ((now - lastMeterUiUpdate).TotalMilliseconds < 70)
                return;

            lastMeterUiUpdate = now;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!audioListening)
                        return;

                    audioMeter.SetLevels(
                        rmsDb,
                        peakDb);
                    overlayRms=rmsDb;overlayPeak=peakDb;
                    if(workspaceAudioMeter!=null) workspaceAudioMeter.SetLevels(rmsDb,peakDb);

                    audioDbLabel.Text = string.Format(
                        "RMS: {0:0.0} dBFS   Peak: {1:0.0} dBFS",
                        rmsDb,
                        peakDb);

                    if (peakDb > -1.0)
                    {
                        SetSignalQuality(
                            "CLIPPING",
                            Color.FromArgb(190, 48, 48));
                    }
                    else if (rmsDb > -10.0)
                    {
                        SetSignalQuality(
                            "HOT",
                            Color.FromArgb(205, 99, 38));
                    }
                    else if (rmsDb > -28.0)
                    {
                        SetSignalQuality(
                            "GOOD SIGNAL",
                            Color.FromArgb(35, 145, 82));
                    }
                    else if (rmsDb > -45.0)
                    {
                        SetSignalQuality(
                            "LOW SIGNAL",
                            Color.FromArgb(198, 135, 31));
                    }
                    else
                    {
                        SetSignalQuality(
                            "NO / VERY LOW",
                            Color.FromArgb(85, 89, 96));
                    }
                });
            }
            catch { }
        }

        private void OnSpeechRecognized(
            string text,
            double confidence)
        {
            OnFinalTranscript(
                text,
                confidence,
                "Windows Speech",
                0);
        }

        private void OnFinalTranscript(
            string text,
            double confidence,
            string engine,
            double latencyMs,
            int session = -1,
            SpeechChunk chunk = null)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (session < 0) session = listeningSession;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!audioListening || session != listeningSession ||
                        (chunk != null && chunk.UtteranceId <= lastFinalUtteranceId))
                        return;
                    string clean =
                        string.IsNullOrWhiteSpace(text)
                            ? string.Empty
                            : text.Trim();

                    if (clean.Length == 0){
                        if(chunk!=null){RenderTranscript(session+":"+chunk.UtteranceId,"",true);lastFinalUtteranceId=chunk.UtteranceId;}
                        return;
                    }

                    long revision = ++interpretationRevision;
                    historyEventKey=chunk==null?session+":final:"+revision:session+":"+chunk.UtteranceId;
                    FinishHistoryCandidate(historyEventKey);

                    RenderTranscript(chunk==null?session+":windows":session+":"+chunk.UtteranceId,clean,true);

                    if (engine == "Windows Speech")
                    {
                        confidenceLabel.Text = string.Format(
                            "Last STT confidence: {0:0}%",
                            confidence * 100.0);
                    }
                    else
                    {
                        confidenceLabel.Text =
                            "Last result: Local AI";
                    }

                    if (latencyMs > 0)
                    {
                        latencyLabel.Text = string.Format(
                            "Response time: {0:0} ms",
                            latencyMs);
                    }

                    hypothesisLabel.Text =
                        "Live text: —";

                    // This callback runs later on the UI thread; exceptions
                    // here cannot be caught by the outer BeginInvoke catch.
                    try
                    {
                    long parseStarted = Stopwatch.GetTimestamp();
                    scriptureDetector.SeedActiveReference(scriptureDetector.IsTranslationDirective(clean) && lastDetection!=null?lastDetection:activeReference.Context);
                    ScriptureDetection detection = chunk == null ? scriptureDetector.Feed(clean) :
                        scriptureDetector.FeedFinal(clean,session+":"+chunk.UtteranceId);
                    bool finalRevisesProvisional=activeReference.Provisional!=null;
                    activeReference.Confirm(null);
                    if(detection!=null)activeReference.Confirm(detection);
                    else if(!string.IsNullOrEmpty(scriptureDetector.UnresolvedBook) && (scriptureDetector.UnresolvedSource==clean || scriptureDetector.UnresolvedSource.EndsWith(Environment.NewLine+clean)))
                    {
                        ScriptureDetection draft=new ScriptureDetection{Book=scriptureDetector.UnresolvedBook,Chapter=scriptureDetector.UnresolvedChapter,Verse=0,Translation=scriptureDetector.UnresolvedTranslation,TranslationExplicit=scriptureDetector.UnresolvedTranslationExplicit,SourceText=clean,RequiresReview=true,EvidenceKind="draft"};
                        activeReference.PreserveSelectedPassage(lastDetection,draft);
                        activeReference.Confirm(draft);
                    }
                    quoteSwitchRevision=Regex.IsMatch(clean,@"\b(?:elsewhere\s+(?:the\s+)?(?:scripture|bible)\s+says|another\s+(?:scripture|passage)\s+says|in\s+another\s+place\s+(?:it\s+)?says)\b",RegexOptions.IgnoreCase)?revision:-1;
                    quoteSwitchText=clean;
                    if (detection == null) detection = scriptureDetector.Suggest(clean);
                    if(detection!=null && (detection.EvidenceKind=="explicit" || detection.EvidenceKind=="correction" || detection.EvidenceKind=="contextual" || detection.EvidenceKind=="navigation"))
                    {quoteScopeBook=detection.Book;quoteScopeChapter=detection.Chapter;quoteScopeUtc=DateTime.UtcNow;}
                    else if(scriptureDetector.UnresolvedSource==clean || scriptureDetector.UnresolvedSource.EndsWith(Environment.NewLine+clean))
                    {quoteScopeBook=scriptureDetector.UnresolvedBook;quoteScopeChapter=scriptureDetector.UnresolvedChapter;quoteScopeUtc=DateTime.UtcNow;}
                    double parseMs = (Stopwatch.GetTimestamp() - parseStarted) * 1000.0 / Stopwatch.Frequency;
                    if (chunk != null)
                    {
                        lastFinalUtteranceId = chunk.UtteranceId;
                        double readyMs = (Stopwatch.GetTimestamp() - chunk.LastVoiceTicks) * 1000.0 / Stopwatch.Frequency;
                        double endpointMs = (chunk.QueuedTicks - chunk.LastVoiceTicks) * 1000.0 / Stopwatch.Frequency;
                        double audioSeconds = chunk.Pcm.Length / 32000.0;
                        latencyLabel.Text = string.Format("Ready: {0:0} ms", readyMs);
                        AddLog(string.Format("Speech timing: audio={0:0.00}s endpoint={1:0}ms inference={2:0}ms parse={3:0.0}ms ready={4:0}ms", audioSeconds, endpointMs, latencyMs, parseMs, readyMs));
                    }
                    AddLog("Transcript: " + clean);
                    if(operatorDraft)
                    {
                        RefreshPassageAlternatives();
                        if(detection!=null)RecordSuggestion(detection,false,historyEventKey);
                        FindQuotation(clean,quotationContext.Append(clean,DateTime.UtcNow),session,revision);
                        return;
                    }
                        if (detection != null)
                        {
                            quotationContext.Reset();
                            lastQuotationReference = string.Empty;
                            detection=SpeechPresentationPolicy.Resolve(detection,lastDetection,(DateTime.UtcNow-lastSpeechDetectionUtc).TotalSeconds<=120);
                            activeReference.Confirm(detection);
                            lastSpeechDetectionUtc=DateTime.UtcNow;
                            ShowDetection(detection);
                        }
                    else if (!string.IsNullOrEmpty(scriptureDetector.UnresolvedBook) &&
                        (lastDetection==null || scriptureDetector.UnresolvedSource==clean))
                    {
                        lastDetection=null;
                        UpdateVerseExcerpt(null);SetDraft(scriptureDetector.UnresolvedBook,scriptureDetector.UnresolvedChapter,0,0);
                        TrackWaitingReference(scriptureDetector.UnresolvedSource);
                        detectedReferenceLabel.Text=scriptureDetector.UnresolvedBook+(scriptureDetector.UnresolvedChapter>0?" "+scriptureDetector.UnresolvedChapter+":…":"");
                        detectedReferenceLabel.ForeColor=GazelyTheme.Warning;
                        detectedConfidenceLabel.Text="Listening for chapter and verse…";
                        detectedReasonLabel.Text="Continue speaking, or fill in the reference below.";
                        detectedSourceLabel.Text="Heard: "+scriptureDetector.UnresolvedSource;
                        evidenceTip.SetToolTip(detectedSourceLabel,detectedSourceLabel.Text);
                        detectedVersionLabel.Text="Version: not resolved";
                        previewDetectedButton.Enabled=liveDetectedButton.Enabled=false;
                        RecordUnresolvedReference(scriptureDetector.UnresolvedBook,scriptureDetector.UnresolvedSource);
                        if(lastChapterRecoverySource==scriptureDetector.UnresolvedSource && lastChapterRecovery!=null)
                            DisplayChapterRecovery(lastChapterRecovery,scriptureDetector.UnresolvedSource,session);
                        else RequestChapterRecovery(scriptureDetector.UnresolvedBook,scriptureDetector.UnresolvedSource,session,revision);
                    }
                    else if (lastDetection == null)
                    {
                        detectedReferenceLabel.Text = "No scripture detected";
                        detectedReferenceLabel.ForeColor = GazelyTheme.Muted;
                        detectedSourceLabel.Text = "Heard: " + clean;
                        detectedReasonLabel.Text = scriptureDetector.LastExplanation;
                        detectedVersionLabel.Text = "Version: —";
                        detectedConfidenceLabel.Text = "Confidence: —";
                        previewDetectedButton.Enabled = liveDetectedButton.Enabled = false;
                    }
                    if (detection == null) AddLog("Parser: " + scriptureDetector.LastExplanation);
                    if(detection==null)RenderActiveReference(finalRevisesProvisional);
                    RequestReferenceRefinement(clean,chunk);
                    FindQuotation(clean, quotationContext.Append(clean,DateTime.UtcNow), session, revision);
                    }
                    catch (Exception ex)
                    {
                        ClearDetectionUi();
                        detectedReasonLabel.Text = "Could not interpret this phrase. Please repeat the reference.";
                        AddLog("Scripture parser error: " + ex.ToString());
                    }
                });
            }
            catch { }
        }

        private readonly Queue<Action> quotationJobs=new Queue<Action>();
        private bool quotationWorker;
        private readonly OfflineSemanticSearch semanticSearch=new OfflineSemanticSearch();
        private int candidateEpoch;
        private DateTime lastSpeechDetectionUtc=DateTime.MinValue;
        private string quoteScopeBook;
        private int quoteScopeChapter;
        private DateTime quoteScopeUtc=DateTime.MinValue;
        private readonly ActiveReferenceState activeReference=new ActiveReferenceState();
        private long quoteSwitchRevision=-1;
        private string quoteSwitchText=string.Empty;
        private bool IsClearQuoteSwitch(ScriptureDetection d)
        {return d.EvidenceKind=="quotation" && d.MatchScore>=0.94 && d.SourceText==quoteSwitchText && !(d.Reason??"").StartsWith("Shared wording");}
        private static string ActiveReferenceText(ScriptureDetection d)
        {return d.Chapter==0?d.Book:d.Verse==0?d.Book+" "+d.Chapter+":…":d.ReferenceText;}
        private void RenderActiveReference(bool clearEmpty=false)
        {
            if(operatorDraft)return;
            ScriptureDetection d=activeReference.Current;
            if(d==null)
            {
                if(clearEmpty){lastDetection=null;SetDraft(string.Empty,0,0,0);UpdateVerseExcerpt(null);detectedReferenceLabel.Text="Waiting for a reference";detectedConfidenceLabel.Text="Searching";detectedReasonLabel.Text="Live words may revise while you speak.";previewDetectedButton.Enabled=liveDetectedButton.Enabled=false;}
                RefreshPassageAlternatives();
                return;
            }
            bool provisional=object.ReferenceEquals(d,activeReference.Provisional);
            bool retained=d.EvidenceKind=="retained";
            SetDraft(d.Book,d.Chapter,d.Verse,d.VerseEnd);UpdateVerseExcerpt(d.Verse>0?d:null);
            lastDetection=d.Verse>0 && !provisional?d.Snapshot():null;
            detectedReferenceLabel.Text=ActiveReferenceText(d)+((provisional || retained)?" ?":"");
            detectedConfidenceLabel.Text=retained?"Unconfirmed — review reference":provisional?"Provisional — still listening":activeReference.Stage+(d.RequiresReview && d.Verse>0?" — review required":"");
            detectedReferenceLabel.ForeColor=(provisional || retained)?GazelyTheme.Warning:GazelyTheme.Accent;
            detectedReasonLabel.Text=retained?d.Reason:d.Verse==0?"Waiting for "+(d.Chapter==0?"chapter and verse":"verse")+". Quote suggestions stay in Detections.":"Following this scripture. Quote suggestions stay in Detections.";
            detectedSourceLabel.Text="Heard: "+d.SourceText;
            draftStatus.Text="Active scripture held • Release scripture to allow quote selection.";
            previewDetectedButton.Enabled=liveDetectedButton.Enabled=lastDetection!=null;
            RefreshPassageAlternatives();
        }
        private void PublishQuotations(List<ScriptureDetection> results,int session,long revision,int epoch)
        {
            if(results.Count==0 || IsDisposed || !IsHandleCreated)return;
            BeginInvoke((MethodInvoker)delegate{
                if(!audioListening || session!=listeningSession || epoch!=candidateEpoch)return;
                foreach(ScriptureDetection result in results)
                    RecordSuggestion(result,false,session+":candidate:"+epoch+":"+result.ReferenceText);
                if(revision==interpretationRevision){foreach(ScriptureDetection result in results)RememberQuoteAlternative(result);RefreshPassageAlternatives();}
                // Older valid evidence belongs in the panel but must not steal current selection.
                bool clearSwitch=quoteSwitchRevision==revision && results.Exists(IsClearQuoteSwitch);
                if(revision!=interpretationRevision || operatorDraft || (ProtectSpokenSelection() && !clearSwitch))return;
                ScriptureDetection first=results[0];
                if(clearSwitch){first=results.Find(IsClearQuoteSwitch);activeReference.Confirm(first);quoteScopeBook=first.Book;quoteScopeChapter=first.Chapter;quoteScopeUtc=DateTime.UtcNow;quoteSwitchRevision=-1;}
                else if((DateTime.UtcNow-quoteScopeUtc).TotalSeconds<=90 && !string.IsNullOrEmpty(quoteScopeBook))
                {first=results.Find(delegate(ScriptureDetection d){return d.Book==quoteScopeBook && (quoteScopeChapter==0 || d.Chapter==quoteScopeChapter);});if(first==null)return;}
                if(first.ReferenceText==lastQuotationReference)return;
                lastQuotationReference=first.ReferenceText;selectingHistory=true;
                try{ShowDetection(first);}finally{selectingHistory=false;}
            });
        }
        private bool ProtectSpokenSelection()
        {
            return activeReference.IsActive;
        }

        private void FindQuotation(string text, string context, int session, long revision)
        {
            int epoch=candidateEpoch;
            string semanticContext=quotationContext.RecentPair;
            if(string.IsNullOrEmpty(semanticContext))semanticContext=context;
            ScriptureDetection active=activeReference.Context;
            string scopeBook=active!=null?active.Book:(DateTime.UtcNow-quoteScopeUtc).TotalSeconds<=90?quoteScopeBook:null;
            int scopeChapter=active!=null?active.Chapter:quoteScopeChapter;
            ScriptureQuoteIndex index = quotationIndex;
            if (index == null) return;
            Action job=delegate
            {
                try
                {
                    if(IsDisposed || session!=listeningSession)return;
                    List<ScriptureDetection> matches=index.FindAll(text);
                    Match transition=Regex.Match(text,@"\b(?:elsewhere\s+(?:the\s+)?(?:scripture|bible)\s+says|another\s+(?:scripture|passage)\s+says|in\s+another\s+place\s+(?:it\s+)?says)\b\s*[:,]?\s*(.+)$",RegexOptions.IgnoreCase);
                    if(transition.Success)foreach(ScriptureDetection moved in index.FindAll(transition.Groups[1].Value))
                    {moved.SourceText=text;matches.Insert(0,moved);}
                    bool currentScopeEvidence=matches.Exists(delegate(ScriptureDetection d){return d.Book==scopeBook && d.Chapter==scopeChapter;});
                    HashSet<string> seen=new HashSet<string>();foreach(ScriptureDetection d in matches)seen.Add(d.ReferenceText);
                    ScriptureDetection scoped=index.FindInChapter(text,scopeBook,scopeChapter);
                    currentScopeEvidence|=scoped!=null;
                    if(scoped==null)scoped=index.FindInChapter(context,scopeBook,scopeChapter);
                    if(scoped!=null && seen.Add(scoped.ReferenceText))matches.Insert(0,scoped);
                    if(context!=text)foreach(ScriptureDetection d in index.FindAll(context))if(seen.Add(d.ReferenceText))matches.Add(d);
                    // Parse each canonical book span so several explicit calls in one utterance survive.
                    foreach(ScriptureDetection d in new ScriptureDetector().FindMentionedReferences(text))if(seen.Add(d.ReferenceText))matches.Add(d);
                    PublishQuotations(matches,session,revision,epoch);
                    List<string> phrases=ScriptureQuoteIndex.SearchPhrases(text);
                    bool hasEvidence=matches.Count>0;
                    bool hasScopeEvidence=currentScopeEvidence;
                    foreach(string phrase in phrases)
                    {
                        if(IsDisposed || session!=listeningSession || epoch!=candidateEpoch)return;
                        if(phrase.Split(' ').Length<5)continue;
                        List<ScriptureDetection> semantic=semanticSearch.Find(index,phrase);
                        hasEvidence|=semantic.Count>0;
                        hasScopeEvidence|=semantic.Exists(delegate(ScriptureDetection d){return d.Book==scopeBook && d.Chapter==scopeChapter;});
                        semantic.RemoveAll(delegate(ScriptureDetection d){return !seen.Add(d.ReferenceText);});
                        PublishQuotations(semantic,session,revision,epoch);
                    }
                    // Context can dilute a good match. Retry only an otherwise empty lookup.
                    if((!hasEvidence || (!string.IsNullOrEmpty(scopeBook) && scopeChapter>0 && !hasScopeEvidence)) && context!=text && !IsDisposed && session==listeningSession && epoch==candidateEpoch)
                    {
                        string[] words=Regex.Split(semanticContext.Trim(),@"\s+");
                        string joined=string.Join(" ",words,Math.Max(0,words.Length-40),Math.Min(40,words.Length));
                        if(!phrases.Contains(joined))PublishQuotations(semanticSearch.Find(index,joined),session,revision,epoch);
                    }
                    if(!IsDisposed && IsHandleCreated)BeginInvoke((MethodInvoker)delegate{if(candidateSearchStatus!=null)candidateSearchStatus.Text=semanticSearch.Status;});
                }
                catch(Exception ex) { semanticSearch.Dispose();AddLog("Quotation lookup failed: " + ex.Message); }
            };
            lock(quotationJobs)
            {
                quotationJobs.Enqueue(job);if(quotationWorker)return;quotationWorker=true;
            }
            ThreadPool.QueueUserWorkItem(delegate{
                try{while(true){Action next;lock(quotationJobs){if(quotationJobs.Count==0){quotationWorker=false;return;}next=quotationJobs.Dequeue();}next();}}
                finally{if(IsDisposed)semanticSearch.Dispose();}
            });
        }

        private void ShowDetection(
            ScriptureDetection detection)
        {
            if(!selectingHistory)RecordSuggestion(detection,false,historyEventKey);
            if(operatorDraft)return;
            UpdateVerseExcerpt(detection);
            SetDraft(detection.Book,detection.Chapter,detection.Verse,detection.VerseEnd);
            lastDetection = detection;
            RefreshPassageAlternatives();

            detectedReferenceLabel.Text =
                detection.ReferenceText;
            detectedReferenceLabel.ForeColor =
                detection.Confidence >= 0.90
                    ? GazelyTheme.Accent
                    : GazelyTheme.Warning;

            string version =
                detection.TranslationExplicit &&
                !string.IsNullOrEmpty(detection.Translation)
                    ? detection.Translation
                    : "KJV (default)";

            detectedVersionLabel.Text =
                "Version: " + version;

            string confidenceBand =
                detection.Confidence >= 0.90
                    ? "HIGH"
                    : detection.Confidence >= 0.80
                        ? "MEDIUM"
                        : "LOW";

            if(detection.EvidenceKind=="paraphrase")confidenceBand="POSSIBLE PARAPHRASE";
            else if(detection.EvidenceKind=="quotation")confidenceBand="QUOTATION MATCH";
            detectedConfidenceLabel.Text =
                string.Format(
                    "Match strength: {0}{1}",
                    confidenceBand,
                    detection.RequiresReview ? " — review required" : "");

            detectedReasonLabel.Text =
                "How it matched: " + detection.Reason;

            detectedSourceLabel.Text =
                "Heard: " + detection.SourceText;
            string evidence = "Heard: " + detection.SourceText;
            if(!string.IsNullOrEmpty(detection.MatchedText))
            {
                evidence += Environment.NewLine + "KJV evidence: " + detection.MatchedText;
                detectedSourceLabel.Text = evidence;
            }
            evidenceTip.SetToolTip(detectedSourceLabel, evidence);

            previewDetectedButton.Enabled = true;
            liveDetectedButton.Enabled = true;

            AddLog(
                "Detected scripture: " +
                detection.ReferenceText +
                (detection.TranslationExplicit
                    ? " [" + detection.Translation + "]"
                    : string.Empty) +
                " — " +
                confidenceBand);

            if (!selectingHistory && SpeechPresentationPolicy.CanAutomate(detection))
                MaybeAutoRunDetection(detection);
        }

        private void MaybeAutoRunDetection(
            ScriptureDetection detection)
        {
            if(useNativeStreaming)return; // Experimental speech results require manual presentation.
            if(presentationRunning || !destination.Enabled) return;
            if (!autoPreviewCheck.Checked &&
                !autoLiveCheck.Checked)
                return;

            DateTime now =
                DateTime.UtcNow;

            if (string.Equals(
                    lastAutoSignature,
                    detection.Signature,
                    StringComparison.OrdinalIgnoreCase) &&
                (now - lastAutoActionUtc)
                    .TotalSeconds < 12)
            {
                return;
            }

            lastAutoSignature =
                detection.Signature;
            lastAutoActionUtc =
                now;

            if (autoLiveCheck.Checked)
                RunDetected(true);
            else if (autoPreviewCheck.Checked)
                RunDetected(false);
        }

        private void ResetVersionTracking()
        {
            automation.ResetVersionState();
            UpdateVersionStateLabel();
            workspacePresentationStatus.Text="Version tracking reset. The next send will reselect the requested Bible version.";
        }

        private void RunDetected(bool goLive)
        {
            if (lastDetection == null)
                return;

            ScriptureDetection d =
                lastDetection;

            ScriptureRequest request =
                new ScriptureRequest();

            request.Book = d.Book;
            request.Chapter = d.Chapter;
            request.Verse = d.Verse;
            request.VerseEnd = d.VerseEnd;

            string version =
                d.TranslationExplicit &&
                !string.IsNullOrEmpty(d.Translation)
                    ? d.Translation
                    : "KJV";

            int index;
            bool hasVersion =
                versionMap.TryGetValue(
                    version,
                    out index);

            if (!hasVersion)
            {
                MessageBox.Show(
                    this,
                    "Detected/default Bible version '" +
                    version +
                    "' is not mapped on this PC.\r\n\r\n" +
                    "Update the Bible Version Map before sending this detection.",
                    "Bible version not mapped",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AddLog(
                    (goLive
                        ? "Detected GO LIVE: "
                        : "Detected PREVIEW: ") +
                    d.ReferenceText +
                    " [" +
                    version +
                    "]");

                SubmitPresentation(
                    request,
                    true,
                    version,
                    index,
                    goLive);

                UpdateVersionStateLabel();
            }
            catch (Exception ex)
            {
                AddLog(
                    "Detected scripture automation error: " +
                    ex.Message);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Automation error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnSpeechHypothesized(
            string text)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if(!audioListening)return;
                    RenderTranscript(listeningSession+":windows",text??string.Empty,false);
                    scriptureDetector.SeedActiveReference(activeReference.Context);
                    bool wasProvisional=activeReference.Provisional!=null;
                    ScriptureDetection candidate=scriptureDetector.Preview(text??string.Empty,true);
                    activeReference.PreserveSelectedPassage(lastDetection,candidate);
                    activeReference.Preview(candidate);
                    if(!operatorDraft)RenderActiveReference(wasProvisional);
                });
            }
            catch { }
        }

        private void OnRecognizerStateChanged(
            string state)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    recognizerStatusLabel.Text =
                        "Speech recognition: " + state;
                });
            }
            catch { }
        }

        private void OnRecognizerError(
            string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    recognizerStatusLabel.Text =
                        "Speech recognition problem: " + message;

                    AddLog(
                        "Speech recognizer error: " +
                        message);
                });
            }
            catch { }
        }

        private void SetVadStatus(
            bool speech)
        {
            if (vadStatusLabel == null)
                return;

            vadStatusLabel.Text =
                speech
                    ? "VOICE DETECTED"
                    : "NO VOICE";

            vadStatusLabel.BackColor =
                speech
                    ? Color.FromArgb(35, 145, 82)
                    : Color.FromArgb(85, 89, 96);
        }

        private void SetWhisperStatus(
            string text,
            Color color)
        {
            if (whisperStatusLabel == null)
                return;

            whisperStatusLabel.Text = text;
            whisperStatusLabel.BackColor = color;
        }

        private void SetWhisperStatusSafe(
            string text,
            Color color)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    SetWhisperStatus(
                        text,
                        color);
                });
            }
            catch { }
        }

        private void SetAudioStatus(
            string text,
            Color color)
        {
            audioStatusLabel.Text = text;
            audioStatusLabel.BackColor = color;
        }

        private void SetSignalQuality(
            string text,
            Color color)
        {
            signalQualityLabel.Text = text;
            signalQualityLabel.BackColor = color;
        }

        private void UpdateAudioControls()
        {
            if (audioDeviceCombo == null || IsDisposed)
                return;

            bool configurable = !audioListening && !startingListening;
            bool experimental=speechEngineCombo.SelectedIndex>=2;
            autoLiveCheck.Enabled=!experimental;autoPreviewCheck.Enabled=!experimental;
            if (earlyResultsCheck != null) earlyResultsCheck.Enabled = configurable;
            refinementCheck.Enabled=configurable;

            audioDeviceCombo.Enabled =
                configurable;

            recognizerCombo.Enabled =
                configurable;

            speechEngineCombo.Enabled =
                configurable;

            whisperModelCombo.Enabled =
                configurable;

            vadSensitivityCombo.Enabled =
                configurable;

            refreshAudioButton.Enabled =
                configurable;

            startListeningButton.Enabled =
                configurable &&
                audioDeviceCombo.Items.Count > 0;

            stopListeningButton.Enabled =
                audioListening;
        }

        private Dictionary<string, string> BuildAliases()
        {
            Dictionary<string, string> d =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            d["MSG"] = "MSG";
            d["MESSAGE"] = "MSG";
            d["THE MESSAGE"] = "MSG";
            d["THE MESSAGE BIBLE"] = "MSG";

            d["KJV"] = "KJV";
            d["KING JAMES"] = "KJV";
            d["KING JAMES VERSION"] = "KJV";

            d["NIV"] = "NIV";
            d["NEW INTERNATIONAL VERSION"] = "NIV";

            d["NLT"] = "NLT";
            d["NEW LIVING TRANSLATION"] = "NLT";

            d["NKJV"] = "NKJV";
            d["NEW KING JAMES VERSION"] = "NKJV";

            d["ESV"] = "ESV";
            d["ENGLISH STANDARD VERSION"] = "ESV";

            d["AMP"] = "AMP";
            d["AMPLIFIED"] = "AMP";
            d["AMPLIFIED BIBLE"] = "AMP";

            return d;
        }

        private void EnsureDefaultMapFile()
        {
            if (File.Exists(mapPath))
                return;

            string defaultMap =
                "# EasyWorship Bible version map\r\n" +
                "# LOCAL to this PC/church. Do NOT treat these indexes as global.\r\n" +
                "# Confirmed on the tested LIGHT NATION_1 installation:\r\n" +
                "AMP=0\r\n" +
                "AMPC=1\r\n" +
                "ASV=2\r\n" +
                "BBE=3\r\n" +
                "BSB=4\r\n" +
                "ESV=5\r\n" +
                "GNT=6\r\n" +
                "HAU=7\r\n" +
                "HCSB=8\r\n" +
                "KJV=9\r\n" +
                "LBLA=10\r\n" +
                "MSG=11\r\n";

            File.WriteAllText(mapPath, defaultMap);
        }

        private void LoadVersionMap()
        {
            versionMap.Clear();

            string text = string.Empty;
            try
            {
                text = File.ReadAllText(mapPath);
            }
            catch (Exception ex)
            {
                AddLog("Could not read version map: " + ex.Message);
                return;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int i;

            for (i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                int equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;

                string code = line.Substring(0, equals).Trim().ToUpperInvariant();
                string number = line.Substring(equals + 1).Trim();

                int index;
                if (code.Length > 0 && int.TryParse(number, out index) && index >= 0)
                    versionMap[code] = index;
            }

            int startupKjvIndex;
            automation.ConfigureVersionMap(versionMap.TryGetValue("KJV",out startupKjvIndex)?startupKjvIndex:-1,text);
            List<VersionEntry> ordered = new List<VersionEntry>();

            foreach (KeyValuePair<string, int> pair in versionMap)
            {
                VersionEntry entry = new VersionEntry();
                entry.Code = pair.Key;
                entry.Index = pair.Value;
                ordered.Add(entry);
            }

            ordered.Sort(delegate(VersionEntry a, VersionEntry b)
            {
                return a.Index.CompareTo(b.Index);
            });

            string previous = versionCombo.Text;

            versionCombo.Items.Clear();
            for (i = 0; i < ordered.Count; i++)
                versionCombo.Items.Add(ordered[i].Code);

            if (versionMap.ContainsKey("KJV"))
                versionCombo.Text = "KJV";
            else if (previous.Length > 0)
                versionCombo.Text = previous;
            else if (versionCombo.Items.Count > 0)
                versionCombo.SelectedIndex = 0;

            mapEditor.Text = Regex.Replace(text,@"\r?\n","\r\n");
            mapEditor.SelectionStart=0;mapEditor.SelectionLength=0;
            UpdateVersionIndexLabel();
            AddLog(string.Format("Loaded {0} Bible version mappings.", versionMap.Count));
        }

        private void SaveMapFromEditor()
        {
            try
            {
                File.WriteAllText(mapPath, mapEditor.Text);
                AddLog("Version map saved.");
                LoadVersionMap();
                MessageBox.Show(
                    this,
                    "Version map saved and reloaded.",
                    "EasyWorship Controller",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Could not save version map:\r\n\r\n" + ex.Message,
                    "EasyWorship Controller",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateVersionStateLabel()
        {
            string state = automation.GetVersionStateSummary();
            versionStateLabel.Text = "tracked: " + state;
        }

        private void RefreshConnection()
        {
            IntegrationState state = destination.Refresh();
            bool connected = state == IntegrationState.Ready;

            if (connected)
            {
                connectionLabel.Text = "EasyWorship 7: CONNECTED";
                connectionLabel.ForeColor = GazelyTheme.Accent;
            }
            else
            {
                connectionLabel.Text = "EasyWorship 7: " + state.ToString().ToUpperInvariant();
                connectionLabel.ForeColor = GazelyTheme.Error;
            }
            if(integrationStatus!=null) integrationStatus.Text = "Connection: " + state + ". Live-screen readback unavailable.";
        }

        private string NormalizeVersion(string input)
        {
            if (input == null)
                return string.Empty;

            string normalized = Regex.Replace(
                input.Trim().ToUpperInvariant(),
                @"\s+",
                " ");

            string canonical;
            if (aliases.TryGetValue(normalized, out canonical))
                return canonical;

            return normalized;
        }

        private bool TryGetSelectedVersion(out string code, out int index)
        {
            code = NormalizeVersion(versionCombo.Text);
            index = -1;

            if (code.Length == 0)
            {
                MessageBox.Show(
                    this,
                    "Choose or type a Bible version.",
                    "Bible version required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            if (!versionMap.TryGetValue(code, out index))
            {
                MessageBox.Show(
                    this,
                    "Version '" + code + "' is not in this PC's version-map.txt.\r\n\r\n" +
                    "Open the Bible Version Map tab and add the correct local index first.",
                    "Version not mapped",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            return true;
        }

        private void UpdateVersionIndexLabel()
        {
            string code = NormalizeVersion(versionCombo.Text);
            int index;

            if (versionMap.TryGetValue(code, out index))
                versionIndexLabel.Text = "index " + index;
            else
                versionIndexLabel.Text = "not mapped";
        }

        private bool TryParseScripture(string input, out ScriptureRequest request)
        {
            request = null;

            if (input == null)
                return false;

            Match match = Regex.Match(
                input.Trim(),
                @"^(.+?)\s+(\d+)\s*:\s*(\d+)(?:\s*-\s*(\d+))?$");

            if (!match.Success)
                return false;

            int chapter;
            int verse;

            if (!int.TryParse(match.Groups[2].Value, out chapter))
                return false;

            if (!int.TryParse(match.Groups[3].Value, out verse))
                return false;

            if (chapter <= 0 || verse <= 0)
                return false;

            ScriptureDetection parsed=new ScriptureDetector().Feed(input);
            if(parsed==null) return false;
            request = new ScriptureRequest { Book=parsed.Book,Chapter=parsed.Chapter,Verse=parsed.Verse,VerseEnd=parsed.VerseEnd };
            return true;
        }

        private void SetBusy(bool busy)
        {
            busy=busy || presentationRunning;
            if(stopListeningButton!=null) stopListeningButton.Enabled=audioListening || presentationRunning;
            liveButton.Enabled = !busy;
            previewButton.Enabled = !busy;
            testVersionButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            resyncVersionButton.Enabled = !busy;
            if(previewDetectedButton!=null) previewDetectedButton.Enabled=!busy && lastDetection!=null;
            if(liveDetectedButton!=null) liveDetectedButton.Enabled=!busy && lastDetection!=null;
            UseWaitCursor = busy;
        }

        private void RunVersionOnly()
        {
            if(presentationRunning || !destination.Enabled) { AddLog("Enable the integration and wait for any pending command before testing versions."); return; }
            string code;
            int index;

            if (!TryGetSelectedVersion(out code, out index))
                return;

            try
            {
                SetBusy(true);
                AddLog("Version-only test requested: " + code);
                automation.TestVersionOnly(code, index);
                UpdateVersionStateLabel();
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex.Message);
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Automation error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RunScripture(bool goLive)
        {
            ScriptureRequest request;

            if (!TryParseScripture(scriptureTextBox.Text, out request))
            {
                MessageBox.Show(
                    this,
                    "Enter a valid verse or same-chapter range:\r\n\r\nJohn 3:16\r\nGenesis 49:3-4\r\n1 Corinthians 13:4",
                    "Invalid scripture reference",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string code = string.Empty;
            int index = -1;

            if (changeVersionCheck.Checked)
            {
                if (!TryGetSelectedVersion(out code, out index))
                    return;
            }

            try
            {
                SetBusy(true);

                AddLog(
                    string.Format(
                        "{0}: {1} {2}:{3}{4}",
                        goLive ? "GO LIVE" : "PREVIEW",
                        request.Book,
                        request.Chapter,
                        request.VerseSelection,
                        changeVersionCheck.Checked ? " [" + code + "]" : string.Empty));

                SubmitPresentation(
                    request,
                    changeVersionCheck.Checked,
                    code,
                    index,
                    goLive);

                if (changeVersionCheck.Checked)
                    UpdateVersionStateLabel();
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex.Message);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Automation error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void AddLog(string message)
        {
            if (logTextBox == null || IsDisposed)
                return;

            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { AddLog(message); }); }
                catch (InvalidOperationException) { }
                return;
            }

            string line = string.Format(
                "[{0}] {1}\r\n",
                DateTime.Now.ToString("HH:mm:ss"),
                message);

            logTextBox.AppendText(line);
            if (logTextBox.TextLength > 60000)
                logTextBox.Text = logTextBox.Text.Substring(logTextBox.TextLength - 40000);
            try
            {
                if (File.Exists(diagnosticPath) && new FileInfo(diagnosticPath).Length > 2000000)
                    File.WriteAllText(diagnosticPath, "Previous diagnostics rotated.\r\n", Encoding.UTF8);
                File.AppendAllText(diagnosticPath, line, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
