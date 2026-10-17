using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class GazelySetup
{
    private const string ExpectedHash = "__ARCHIVE_SHA256__";
    private const string ScriptBase64 = "__INSTALL_SCRIPT_BASE64__";

    private static string Quote(string value)
    {
        if (value.IndexOf('"') >= 0) throw new ArgumentException("Invalid path.");
        return "\"" + value + "\"";
    }

    private static string Install(string installRoot, bool noShortcuts)
    {
        string tempParent = Path.GetFullPath(Path.GetTempPath());
        string temp = Path.Combine(tempParent, "gzsetup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(temp);
        try
        {
            string archive = Path.Combine(temp, "__ARCHIVE_NAME__");
            string script = Path.Combine(temp, "Install-Gazely.ps1");
            using (FileStream self = File.OpenRead(Assembly.GetExecutingAssembly().Location))
            {
                if (self.Length < 8) throw new InvalidDataException("Installer payload is missing.");
                self.Seek(-8, SeekOrigin.End);
                byte[] footer = new byte[8];
                if (self.Read(footer, 0, 8) != 8) throw new InvalidDataException("Installer footer is incomplete.");
                long length = BitConverter.ToInt64(footer, 0);
                if (length <= 0 || length > self.Length - 8) throw new InvalidDataException("Installer payload length is invalid.");
                self.Position = self.Length - 8 - length;
                using (FileStream output = File.Create(archive))
                {
                    byte[] buffer = new byte[1024 * 1024];
                    while (length > 0)
                    {
                        int read = self.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                        if (read <= 0) throw new EndOfStreamException("Installer payload ended early.");
                        output.Write(buffer, 0, read);
                        length -= read;
                    }
                }
            }
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(archive))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                if (!string.Equals(actual, ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer checksum did not match. Nothing was installed.");
            }
            File.WriteAllBytes(script, Convert.FromBase64String(ScriptBase64));
            string arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                " -Archive " + Quote(archive) + " -ExpectedSha256 " + ExpectedHash;
            if (!string.IsNullOrEmpty(installRoot)) arguments += " -InstallRoot " + Quote(installRoot);
            if (noShortcuts) arguments += " -NoShortcuts";
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("powershell.exe", arguments)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                process.Start();
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(stdout, stderr);
                if (process.ExitCode != 0) throw new InvalidOperationException("Setup failed: " + stderr.Result.Trim());
                return stdout.Result.Trim();
            }
        }
        finally
        {
            string safeParent = tempParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(temp).StartsWith(safeParent, StringComparison.OrdinalIgnoreCase) && Directory.Exists(temp))
                Directory.Delete(temp, true);
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string installRoot = null;
        bool noShortcuts = false, quiet = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--install-root" && i + 1 < args.Length) installRoot = args[++i];
            else if (args[i] == "--no-shortcuts") noShortcuts = true;
            else if (args[i] == "--quiet") quiet = true;
            else return 2;
        }
        if (quiet)
        {
            try { Install(installRoot, noShortcuts); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Path.GetTempPath(), "gazely-setup-error.txt"), ex.ToString()); return 1; }
        }
        Application.EnableVisualStyles();
        Form form = new Form { Text = "Gazely Setup", Size = new Size(440, 170),
            StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false };
        Label label = new Label { Text = "Installing Gazely from bundled offline files…", AutoSize = false,
            Bounds = new Rectangle(24, 25, 380, 30) };
        ProgressBar progress = new ProgressBar { Style = ProgressBarStyle.Marquee,
            Bounds = new Rectangle(24, 70, 380, 20) };
        form.Controls.Add(label); form.Controls.Add(progress);
        bool installing = true;
        form.FormClosing += delegate(object sender, FormClosingEventArgs e) { if (installing) e.Cancel = true; };
        form.Shown += async delegate
        {
            try
            {
                string result = await Task.Run(() => Install(installRoot, noShortcuts));
                installing = false;
                MessageBox.Show(form, result + "\r\n\r\nOpen Gazely from the Start menu.",
                    "Gazely is ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                installing = false;
                MessageBox.Show(form, ex.Message, "Gazely setup could not finish",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            form.Close();
        };
        Application.Run(form);
        return 0;
    }
}
