using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CodexAppInstaller
{
    // Runs the PowerShell backend (Install-CodexApp.ps1) and streams its output.
    //
    // The GUI does NOT change install logic. It only:
    //   * locates the script (external copy preferred, embedded fallback),
    //   * launches powershell.exe with the right arguments,
    //   * reads stdout/stderr CHARACTER BY CHARACTER so that the script's
    //     carriage-return progress repaints ("[####----]  42.3%\r") are captured
    //     in real time instead of being collapsed by a line-based reader,
    //   * raises events for the UI thread to render.
    internal sealed class InstallerBackend
    {
        // text, endedWithCarriageReturn (true = transient in-place repaint, e.g. a progress bar)
        public event Action<string, bool> Segment;
        public event Action<int> Exited;

        private Process process;
        private readonly object gate = new object();

        public bool IsRunning
        {
            get { lock (gate) { return process != null; } }
        }

        public static string PowerShellPath()
        {
            string ps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            return File.Exists(ps) ? ps : "powershell.exe";
        }

        // Build the argument string for either resolve-only or full install.
        public static string BuildArguments(string scriptPath, bool resolveOnly, string targetDir, bool keepWorkDir)
        {
            var sb = new StringBuilder();
            sb.Append("-NoLogo -NoProfile -ExecutionPolicy Bypass -File ").Append(Quote(scriptPath));
            if (resolveOnly)
            {
                sb.Append(" -ResolveOnly");
            }
            else
            {
                sb.Append(" -TargetDir ").Append(Quote(targetDir));
                if (keepWorkDir) sb.Append(" -KeepWorkDir");
            }
            return sb.ToString();
        }

        public void Start(string scriptPath, bool resolveOnly, string targetDir, bool keepWorkDir)
        {
            lock (gate)
            {
                if (process != null) return;

                var psi = new ProcessStartInfo
                {
                    FileName = PowerShellPath(),
                    Arguments = BuildArguments(scriptPath, resolveOnly, targetDir, keepWorkDir),
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.Exited += delegate
                {
                    int code = -1;
                    try { code = p.ExitCode; } catch { }
                    lock (gate)
                    {
                        // Only clear if the field still refers to THIS process, so a stale
                        // Exited can never null out a process started afterwards.
                        if (ReferenceEquals(process, p)) { try { p.Dispose(); } catch { } process = null; }
                    }
                    var h = Exited;
                    if (h != null) h(code);
                };

                // Publish BEFORE starting: a process that exits almost immediately must not
                // slip its Exited callback in ahead of this assignment and strand us as
                // "running" forever (process != null with nothing actually running).
                process = p;
                try
                {
                    p.Start();
                    StartReader(p.StandardOutput);
                    StartReader(p.StandardError);
                }
                catch
                {
                    if (ReferenceEquals(process, p)) process = null;
                    throw;
                }
            }
        }

        private void StartReader(StreamReader reader)
        {
            var t = new Thread(delegate ()
            {
                var buf = new StringBuilder(256);
                try
                {
                    int ch;
                    while ((ch = reader.Read()) >= 0)
                    {
                        char c = (char)ch;
                        if (c == '\n')
                        {
                            Emit(buf, false);
                        }
                        else if (c == '\r')
                        {
                            // Could be a lone \r (progress repaint) or the \r of \r\n.
                            int next = reader.Peek();
                            if (next == '\n')
                            {
                                reader.Read();      // consume the \n
                                Emit(buf, false);   // real new line
                            }
                            else
                            {
                                Emit(buf, true);    // transient in-place repaint
                            }
                        }
                        else
                        {
                            buf.Append(c);
                        }
                    }
                    if (buf.Length > 0) Emit(buf, false);
                }
                catch
                {
                    // Stream closed or unexpected read error: flush whatever we buffered.
                    if (buf.Length > 0) Emit(buf, false);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void Emit(StringBuilder buf, bool cr)
        {
            string text = buf.ToString();
            buf.Length = 0;
            var h = Segment;
            if (h != null) h(text, cr);
        }

        public void Cancel()
        {
            Process p;
            lock (gate) { p = process; }
            if (p == null) return;
            try
            {
                if (!p.HasExited) KillTree(p.Id);
            }
            catch { }
        }

        private static void KillTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + pid + " /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using (var killer = Process.Start(psi))
                {
                    if (killer != null) killer.WaitForExit(5000);
                }
            }
            catch { }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // External script next to the exe is preferred (auditable / replaceable);
        // otherwise extract the embedded copy to %TEMP%.
        public static string ResolveScript()
        {
            string external = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Install-CodexApp.ps1");
            if (File.Exists(external)) return external;

            try
            {
                using (Stream embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Install-CodexApp.ps1"))
                {
                    if (embedded == null) return "";
                    string dir = Path.Combine(Path.GetTempPath(), "CodexAppInstallerGui");
                    Directory.CreateDirectory(dir);
                    string temp = Path.Combine(dir, "Install-CodexApp.ps1");
                    using (FileStream output = File.Open(temp, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        embedded.CopyTo(output);
                    }
                    return temp;
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
