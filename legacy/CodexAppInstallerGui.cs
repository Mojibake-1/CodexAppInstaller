using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CodexAppInstallerGui
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (InstallerForm form = new InstallerForm())
                {
                    return form.HasBackend ? 0 : 2;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
            return 0;
        }
    }

    internal sealed class InstallerForm : Form
    {
        private string scriptPath;

        private TextBox targetBox;
        private TextBox logBox;
        private Label statusLabel;
        private Label packageLabel;
        private ProgressBar progressBar;
        private Button browseButton;
        private Button resolveButton;
        private Button installButton;
        private Button cancelButton;
        private Button openFolderButton;
        private CheckBox keepWorkDirCheck;

        private Process runningProcess;

        public InstallerForm()
        {
            scriptPath = ResolveBackendScript();
            BuildUi();
            AppendLog("Ready.");
            if (!HasBackend)
            {
                SetStatus("缺少 Install-CodexApp.ps1", false);
                AppendLog("Missing backend script.");
                resolveButton.Enabled = false;
                installButton.Enabled = false;
            }
        }

        public bool HasBackend
        {
            get { return File.Exists(scriptPath); }
        }

        private void BuildUi()
        {
            Text = "Codex App Installer";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 650);
            Size = new Size(980, 720);
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Palette.Canvas;
            AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.BackColor = Palette.Canvas;
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildControls(), 0, 1);
            root.Controls.Add(BuildLog(), 0, 2);
            root.Controls.Add(BuildFooter(), 0, 3);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Palette.Ink;
            header.Padding = new Padding(30, 22, 30, 18);

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "Codex App 安装器";
            title.Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
            title.ForeColor = Palette.HeaderText;
            title.Location = new Point(30, 22);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = "下载 Microsoft Store 的最新 MSIX，校验后安装到本地目录";
            subtitle.Font = new Font("Segoe UI", 10f);
            subtitle.ForeColor = Palette.HeaderMuted;
            subtitle.Location = new Point(32, 70);

            Label badge = new Label();
            badge.AutoSize = true;
            badge.Text = "x64";
            badge.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            badge.ForeColor = Palette.HeaderText;
            badge.BackColor = Palette.Accent;
            badge.Padding = new Padding(10, 5, 10, 5);
            badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            badge.Location = new Point(ClientSize.Width - 88, 28);

            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(badge);
            header.Resize += delegate
            {
                badge.Left = header.ClientSize.Width - badge.Width - 30;
            };
            return header;
        }

        private Control BuildControls()
        {
            BorderPanel panel = new BorderPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(30, 22, 30, 16);
            panel.BackColor = Palette.Surface;
            panel.BorderColor = Palette.Line;

            Label targetLabel = new Label();
            targetLabel.Text = "安装目录";
            targetLabel.AutoSize = true;
            targetLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            targetLabel.ForeColor = Palette.Text;
            targetLabel.Location = new Point(30, 24);

            targetBox = new TextBox();
            targetBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            targetBox.Font = new Font("Segoe UI", 10.5f);
            targetBox.Location = new Point(30, 52);
            targetBox.Width = panel.ClientSize.Width - 156;
            targetBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Codex");

            browseButton = CreateButton("浏览", Palette.Button, Palette.Text);
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Location = new Point(panel.ClientSize.Width - 116, 50);
            browseButton.Size = new Size(86, 32);
            browseButton.Click += delegate { BrowseTarget(); };

            keepWorkDirCheck = new CheckBox();
            keepWorkDirCheck.Text = "保留临时目录";
            keepWorkDirCheck.AutoSize = true;
            keepWorkDirCheck.ForeColor = Palette.MutedText;
            keepWorkDirCheck.Location = new Point(30, 98);

            resolveButton = CreateButton("解析最新版", Palette.Button, Palette.Text);
            resolveButton.Location = new Point(30, 128);
            resolveButton.Size = new Size(116, 36);
            resolveButton.Click += delegate { RunInstaller(true); };

            installButton = CreateButton("安装/更新", Palette.Accent, Palette.HeaderText);
            installButton.Location = new Point(154, 128);
            installButton.Size = new Size(118, 36);
            installButton.Click += delegate { RunInstaller(false); };

            cancelButton = CreateButton("取消", Palette.Warning, Palette.HeaderText);
            cancelButton.Location = new Point(280, 128);
            cancelButton.Size = new Size(86, 36);
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelProcess(); };

            openFolderButton = CreateButton("打开目录", Palette.Button, Palette.Text);
            openFolderButton.Location = new Point(374, 128);
            openFolderButton.Size = new Size(100, 36);
            openFolderButton.Click += delegate { OpenTargetFolder(); };

            packageLabel = new Label();
            packageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            packageLabel.Text = "尚未解析包版本";
            packageLabel.ForeColor = Palette.MutedText;
            packageLabel.AutoEllipsis = true;
            packageLabel.Location = new Point(500, 137);
            packageLabel.Size = new Size(panel.ClientSize.Width - 530, 20);

            panel.Controls.Add(targetLabel);
            panel.Controls.Add(targetBox);
            panel.Controls.Add(browseButton);
            panel.Controls.Add(keepWorkDirCheck);
            panel.Controls.Add(resolveButton);
            panel.Controls.Add(installButton);
            panel.Controls.Add(cancelButton);
            panel.Controls.Add(openFolderButton);
            panel.Controls.Add(packageLabel);

            panel.Resize += delegate
            {
                targetBox.Width = Math.Max(260, panel.ClientSize.Width - 156);
                browseButton.Left = panel.ClientSize.Width - browseButton.Width - 30;
                packageLabel.Width = Math.Max(180, panel.ClientSize.Width - packageLabel.Left - 30);
            };
            return panel;
        }

        private Control BuildLog()
        {
            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.Padding = new Padding(30, 18, 30, 12);
            shell.BackColor = Palette.Canvas;

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Palette.LogBack;
            logBox.ForeColor = Palette.LogText;
            logBox.Font = new Font("Consolas", 9.5f);
            logBox.Margin = new Padding(0);

            BorderPanel logFrame = new BorderPanel();
            logFrame.Dock = DockStyle.Fill;
            logFrame.BackColor = Palette.LogBack;
            logFrame.BorderColor = Palette.LogLine;
            logFrame.Padding = new Padding(14);
            logFrame.Controls.Add(logBox);

            shell.Controls.Add(logFrame);
            return shell;
        }

        private Control BuildFooter()
        {
            BorderPanel footer = new BorderPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = Palette.Surface;
            footer.BorderColor = Palette.Line;
            footer.Padding = new Padding(30, 12, 30, 12);

            statusLabel = new Label();
            statusLabel.Text = "就绪";
            statusLabel.ForeColor = Palette.Text;
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Location = new Point(30, 14);
            statusLabel.Size = new Size(360, 24);

            progressBar = new ProgressBar();
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Location = new Point(410, 18);
            progressBar.Size = new Size(footer.ClientSize.Width - 440, 14);

            footer.Controls.Add(statusLabel);
            footer.Controls.Add(progressBar);
            footer.Resize += delegate
            {
                progressBar.Width = Math.Max(220, footer.ClientSize.Width - progressBar.Left - 30);
            };
            return footer;
        }

        private Button CreateButton(string text, Color back, Color fore)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void BrowseTarget()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 Codex App 安装目录";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(targetBox.Text.Trim()))
                {
                    dialog.SelectedPath = targetBox.Text.Trim();
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    targetBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void RunInstaller(bool resolveOnly)
        {
            if (runningProcess != null)
            {
                return;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "找不到 Install-CodexApp.ps1。", "无法启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string target = targetBox.Text.Trim();
            if (!resolveOnly && target.Length == 0)
            {
                MessageBox.Show(this, "请选择安装目录。", "缺少目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            logBox.Clear();
            AppendLog(resolveOnly ? "Resolving latest package..." : "Installing or updating...");
            SetBusy(resolveOnly ? "正在解析最新版" : "正在安装/更新");

            string powerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(powerShell))
            {
                powerShell = "powershell.exe";
            }

            string arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + Quote(scriptPath);
            if (resolveOnly)
            {
                arguments += " -ResolveOnly";
            }
            else
            {
                arguments += " -TargetDir " + Quote(target);
                if (keepWorkDirCheck.Checked)
                {
                    arguments += " -KeepWorkDir";
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = powerShell;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            runningProcess = new Process();
            runningProcess.StartInfo = startInfo;
            runningProcess.EnableRaisingEvents = true;
            runningProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) BeginInvoke(new Action<string>(AppendLog), e.Data);
            };
            runningProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) BeginInvoke(new Action<string>(AppendLog), e.Data);
            };
            runningProcess.Exited += delegate
            {
                int code = runningProcess.ExitCode;
                BeginInvoke(new Action<int>(ProcessFinished), code);
            };

            try
            {
                runningProcess.Start();
                runningProcess.BeginOutputReadLine();
                runningProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLog("Start failed: " + ex.Message);
                CleanupProcess();
                SetIdle("启动失败", false);
            }
        }

        private void CancelProcess()
        {
            if (runningProcess == null)
            {
                return;
            }

            try
            {
                if (!runningProcess.HasExited)
                {
                    KillProcessTree(runningProcess.Id);
                    AppendLog("Cancelled by user.");
                }
            }
            catch (Exception ex)
            {
                AppendLog("Cancel failed: " + ex.Message);
            }
        }

        private void ProcessFinished(int exitCode)
        {
            CleanupProcess();
            if (exitCode == 0)
            {
                SetIdle("完成", true);
                AppendLog("Done.");
            }
            else
            {
                SetIdle("失败，退出码 " + exitCode, false);
                AppendLog("Failed with exit code " + exitCode + ".");
            }
        }

        private void CleanupProcess()
        {
            if (runningProcess != null)
            {
                runningProcess.Dispose();
                runningProcess = null;
            }
        }

        private void SetBusy(string text)
        {
            SetStatus(text, true);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 22;
            browseButton.Enabled = false;
            resolveButton.Enabled = false;
            installButton.Enabled = false;
            openFolderButton.Enabled = false;
            keepWorkDirCheck.Enabled = false;
            cancelButton.Enabled = true;
        }

        private void SetIdle(string text, bool success)
        {
            SetStatus(text, success);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.Value = success ? 100 : 0;
            browseButton.Enabled = true;
            resolveButton.Enabled = true;
            installButton.Enabled = true;
            openFolderButton.Enabled = true;
            keepWorkDirCheck.Enabled = true;
            cancelButton.Enabled = false;
        }

        private void SetStatus(string text, bool success)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = success ? Palette.Accent : Palette.Warning;
        }

        private void AppendLog(string line)
        {
            string clean = line.Replace("\r", "").TrimEnd();
            if (clean.Length == 0)
            {
                logBox.AppendText(Environment.NewLine);
                return;
            }

            logBox.AppendText(clean + Environment.NewLine);

            Match fileMatch = Regex.Match(clean, @"File:\s+(OpenAI\.Codex_[^\s]+)", RegexOptions.IgnoreCase);
            if (fileMatch.Success)
            {
                packageLabel.Text = fileMatch.Groups[1].Value;
            }

            if (clean.IndexOf("Installed successfully", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                packageLabel.Text = "安装完成：" + packageLabel.Text;
            }
        }

        private void OpenTargetFolder()
        {
            string target = targetBox.Text.Trim();
            if (target.Length == 0)
            {
                return;
            }

            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
            }

            Process.Start("explorer.exe", Quote(target));
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void KillProcessTree(int processId)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "taskkill.exe";
            startInfo.Arguments = "/PID " + processId + " /T /F";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;

            using (Process killer = Process.Start(startInfo))
            {
                if (killer != null)
                {
                    killer.WaitForExit(5000);
                }
            }
        }

        private static string ResolveBackendScript()
        {
            string external = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Install-CodexApp.ps1");
            if (File.Exists(external))
            {
                return external;
            }

            Stream embedded = null;
            FileStream output = null;
            try
            {
                embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Install-CodexApp.ps1");
                if (embedded == null)
                {
                    return "";
                }

                string dir = Path.Combine(Path.GetTempPath(), "CodexAppInstallerGui");
                Directory.CreateDirectory(dir);
                string tempScript = Path.Combine(dir, "Install-CodexApp.ps1");
                output = File.Open(tempScript, FileMode.Create, FileAccess.Write, FileShare.Read);
                embedded.CopyTo(output);
                return tempScript;
            }
            catch
            {
                return "";
            }
            finally
            {
                if (output != null) output.Dispose();
                if (embedded != null) embedded.Dispose();
            }
        }
    }

    internal sealed class BorderPanel : Panel
    {
        public Color BorderColor { get; set; }

        public BorderPanel()
        {
            BorderColor = Palette.Line;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(BorderColor))
            {
                Rectangle rect = ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
    }

    internal static class Palette
    {
        public static readonly Color Canvas = ColorTranslator.FromHtml("#F5F7F4");
        public static readonly Color Surface = ColorTranslator.FromHtml("#FBFCFA");
        public static readonly Color Ink = ColorTranslator.FromHtml("#263239");
        public static readonly Color Text = ColorTranslator.FromHtml("#1F2A2E");
        public static readonly Color MutedText = ColorTranslator.FromHtml("#627078");
        public static readonly Color HeaderText = ColorTranslator.FromHtml("#F4F8F7");
        public static readonly Color HeaderMuted = ColorTranslator.FromHtml("#BCC9C6");
        public static readonly Color Line = ColorTranslator.FromHtml("#DCE2DF");
        public static readonly Color Button = ColorTranslator.FromHtml("#E8EEEA");
        public static readonly Color Accent = ColorTranslator.FromHtml("#177C73");
        public static readonly Color Warning = ColorTranslator.FromHtml("#C8673C");
        public static readonly Color LogBack = ColorTranslator.FromHtml("#20272A");
        public static readonly Color LogText = ColorTranslator.FromHtml("#DCE6E1");
        public static readonly Color LogLine = ColorTranslator.FromHtml("#394448");
    }
}
