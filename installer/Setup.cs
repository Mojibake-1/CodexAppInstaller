using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

// Self-contained installer for the Codex 安装程序 GUI. No third-party packager:
// the app payload is embedded as resources and unpacked on install. Per-user
// (installs to %LOCALAPPDATA%), no administrator rights required.
//
//   (no args)    show the setup window
//   /S /SILENT   install silently (no UI), no launch
//   /uninstall   remove the app (add /S for silent)
namespace CodexSetup
{
    internal static class App
    {
        public const string ProductName = "Codex 安装程序";
        public const string Version = "2.0.0";
        public const string Publisher = "Codex App Installer";
        public const string AppId = "CodexAppInstaller";

        public static string InstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", AppId);
            }
        }
        public static string AppExe { get { return Path.Combine(InstallDir, "CodexAppInstaller.exe"); } }
        public static string Ps1 { get { return Path.Combine(InstallDir, "Install-CodexApp.ps1"); } }
        public static string IconPath { get { return Path.Combine(InstallDir, "app.ico"); } }
        public static string UninstallExe { get { return Path.Combine(InstallDir, "uninstall.exe"); } }

        public static string StartMenuLnk
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs", ProductName + ".lnk");
            }
        }
        public static string DesktopLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk"); }
        }
        public const string RegUninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CodexAppInstaller";
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool silent = false, uninstall = false;
            foreach (string a in args)
            {
                string s = a.TrimStart('-', '/').ToLowerInvariant();
                if (s == "s" || s == "silent" || s == "verysilent" || s == "q" || s == "quiet") silent = true;
                if (s == "uninstall" || s == "u" || s == "x") uninstall = true;
            }

            if (uninstall)
            {
                try { Installer.Uninstall(); } catch { }
                if (!silent)
                    MessageBox.Show(App.ProductName + " 已卸载。", App.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
                return 0;
            }

            if (silent)
            {
                try { Installer.Install(true); return 0; }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }

            var app = new Application();
            return app.Run(new SetupWindow());
        }
    }

    internal static class Installer
    {
        public static void Install(bool desktopShortcut)
        {
            Directory.CreateDirectory(App.InstallDir);
            Extract("CodexApp.exe", App.AppExe);
            Extract("Install.ps1", App.Ps1);
            Extract("CodexApp.ico", App.IconPath);

            // Copy ourselves in as the uninstaller.
            try
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;
                File.Copy(self, App.UninstallExe, true);
            }
            catch { }

            CreateShortcut(App.StartMenuLnk);
            if (desktopShortcut) CreateShortcut(App.DesktopLnk);
            RegisterUninstall();
        }

        public static void Uninstall()
        {
            SafeDelete(App.StartMenuLnk);
            SafeDelete(App.DesktopLnk);
            try { Registry.CurrentUser.DeleteSubKeyTree(App.RegUninstall, false); } catch { }

            // We are running from inside InstallDir (uninstall.exe), so we cannot delete it
            // directly. Hand off to a detached cmd that waits for us to exit, then removes it.
            try
            {
                string dir = App.InstallDir;
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + dir + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch { }
        }

        private static void Extract(string resourceName, string destPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null) throw new FileNotFoundException("Missing embedded resource: " + resourceName);
                using (FileStream fs = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static void CreateShortcut(string lnkPath)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                object shell = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type lt = lnk.GetType();
                Set(lt, lnk, "TargetPath", App.AppExe);
                Set(lt, lnk, "WorkingDirectory", App.InstallDir);
                Set(lt, lnk, "IconLocation", App.IconPath + ",0");
                Set(lt, lnk, "Description", App.ProductName);
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            catch { }
        }

        private static void Set(Type t, object o, string prop, object val)
        {
            t.InvokeMember(prop, BindingFlags.SetProperty, null, o, new object[] { val });
        }

        private static void RegisterUninstall()
        {
            try
            {
                long bytes = 0;
                try { foreach (var f in Directory.GetFiles(App.InstallDir)) bytes += new FileInfo(f).Length; } catch { }
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(App.RegUninstall))
                {
                    k.SetValue("DisplayName", App.ProductName);
                    k.SetValue("DisplayVersion", App.Version);
                    k.SetValue("Publisher", App.Publisher);
                    k.SetValue("DisplayIcon", App.IconPath);
                    k.SetValue("InstallLocation", App.InstallDir);
                    k.SetValue("UninstallString", "\"" + App.UninstallExe + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + App.UninstallExe + "\" /uninstall /S");
                    k.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public static void Launch()
        {
            try { Process.Start(new ProcessStartInfo(App.AppExe) { UseShellExecute = true, WorkingDirectory = App.InstallDir }); }
            catch { }
        }

        public static ImageSource LoadIcon()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("CodexApp.ico"))
                {
                    if (s == null) return null;
                    var dec = BitmapDecoder.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    BitmapFrame best = null;
                    foreach (BitmapFrame f in dec.Frames)
                        if (best == null || f.PixelWidth > best.PixelWidth) best = f;
                    return best;
                }
            }
            catch { return null; }
        }

        // Live Windows accent colour (ABGR DWORD) with a sensible fallback.
        public static Color Accent()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (k != null)
                    {
                        object raw = k.GetValue("AccentColor");
                        if (raw != null)
                        {
                            uint abgr = unchecked((uint)Convert.ToInt32(raw));
                            return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
                        }
                    }
                }
            }
            catch { }
            return Color.FromRgb(0x4C, 0xC2, 0xFF);
        }
    }

    internal sealed class SetupWindow : Window
    {
        private readonly Color _accent = Installer.Accent();
        private Button _primary;
        private Button _close;
        private CheckBox _desktop;
        private ProgressBar _bar;
        private TextBlock _status;
        private bool _installed;

        private static readonly Color Bg = Color.FromRgb(0x1E, 0x1E, 0x1E);
        private static readonly Color Card = Color.FromRgb(0x2A, 0x2A, 0x2A);
        private static readonly Color Line = Color.FromRgb(0x3A, 0x3A, 0x3A);
        private static readonly Color Text = Color.FromRgb(0xF2, 0xF2, 0xF2);
        private static readonly Color Muted = Color.FromRgb(0xB0, 0xB0, 0xB0);
        private static readonly FontFamily Ui = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");

        public SetupWindow()
        {
            Title = App.ProductName;
            Width = 480; Height = 312;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            FontFamily = Ui;
            Icon = Installer.LoadIcon() as ImageSource;
            UseLayoutRounding = true;
            MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };

            BuildUi();
        }

        private void BuildUi()
        {
            var shell = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Bg),
                BorderBrush = new SolidColorBrush(Line),
                BorderThickness = new Thickness(1)
            };
            var root = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // path
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // option
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // status
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons
            shell.Child = root;
            Content = shell;

            // header: icon + title + close
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Top };
            ImageSource ico = Installer.LoadIcon();
            if (ico != null) tile.Child = new Image { Source = ico, Width = 44, Height = 44 };
            else { tile.Background = new SolidColorBrush(_accent); }
            Grid.SetColumn(tile, 0);
            header.Children.Add(tile);

            var titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Tb(App.ProductName, 18, Text, FontWeights.SemiBold));
            titles.Children.Add(Tb("快速安装到此电脑（无需管理员）", 12, Muted, FontWeights.Normal, 2));
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);

            _close = TextButton("✕", delegate { Close(); });
            _close.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(_close, 2);
            header.Children.Add(_close);

            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // path card
            var pathCard = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Card),
                BorderBrush = new SolidColorBrush(Line),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 18, 0, 0)
            };
            var pathStack = new StackPanel();
            pathStack.Children.Add(Tb("安装位置", 11, Muted, FontWeights.Normal));
            var p = Tb(App.InstallDir, 12.5, Text, FontWeights.Normal, 3);
            p.TextTrimming = TextTrimming.CharacterEllipsis;
            pathStack.Children.Add(p);
            pathCard.Child = pathStack;
            Grid.SetRow(pathCard, 1);
            root.Children.Add(pathCard);

            // desktop shortcut option
            _desktop = new CheckBox
            {
                Content = "创建桌面快捷方式",
                IsChecked = true,
                Foreground = new SolidColorBrush(Text),
                Margin = new Thickness(2, 14, 0, 0),
                FontSize = 13
            };
            Grid.SetRow(_desktop, 2);
            root.Children.Add(_desktop);

            // status + progress
            var statusWrap = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 12, 0, 12) };
            _bar = new ProgressBar
            {
                Height = 4, Minimum = 0, Maximum = 100, Value = 0,
                Background = new SolidColorBrush(Line),
                Foreground = new SolidColorBrush(_accent),
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            _status = Tb("", 12, Muted, FontWeights.Normal);
            _status.Margin = new Thickness(0, 0, 0, 6);
            statusWrap.Children.Add(_status);
            statusWrap.Children.Add(_bar);
            Grid.SetRow(statusWrap, 3);
            root.Children.Add(statusWrap);

            // buttons
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = FilledButton("关闭", Card, Text, delegate { Close(); });
            cancel.Margin = new Thickness(0, 0, 8, 0);
            _primary = FilledButton("安装", _accent, IdealOn(_accent), delegate { OnPrimary(); });
            btns.Children.Add(cancel);
            btns.Children.Add(_primary);
            Grid.SetRow(btns, 4);
            root.Children.Add(btns);
        }

        private void OnPrimary()
        {
            if (_installed) { Installer.Launch(); Close(); return; }

            _primary.IsEnabled = false;
            _desktop.IsEnabled = false;
            _bar.Visibility = Visibility.Visible;
            _status.Text = "正在安装…";
            bool desktop = _desktop.IsChecked == true;

            var th = new Thread(delegate ()
            {
                string err = null;
                try
                {
                    Step(15, "正在准备…");
                    Thread.Sleep(120);
                    Step(45, "正在复制文件…");
                    Installer.Install(desktop);
                    Step(85, "正在创建快捷方式…");
                    Thread.Sleep(120);
                    Step(100, "完成");
                }
                catch (Exception ex) { err = ex.Message; }

                Dispatcher.BeginInvoke(new Action(delegate { Done(err); }));
            });
            th.IsBackground = true;
            th.SetApartmentState(ApartmentState.STA); // WScript.Shell COM wants STA
            th.Start();
        }

        private void Step(double pct, string text)
        {
            Dispatcher.BeginInvoke(new Action(delegate { _bar.Value = pct; _status.Text = text; }));
        }

        private void Done(string err)
        {
            if (err != null)
            {
                _status.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0xA4));
                _status.Text = "安装失败：" + err;
                _primary.IsEnabled = true;
                _desktop.IsEnabled = true;
                return;
            }
            _installed = true;
            _status.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F));
            _status.Text = "✓ 安装完成";
            SetButton(_primary, "启动", _accent, IdealOn(_accent));
            _primary.IsEnabled = true;
        }

        // ---- tiny UI helpers ----

        private TextBlock Tb(string text, double size, Color color, FontWeight weight, double topMargin = 0)
        {
            return new TextBlock
            {
                Text = text, FontSize = size, FontWeight = weight,
                Foreground = new SolidColorBrush(color), FontFamily = Ui,
                Margin = new Thickness(0, topMargin, 0, 0), TextWrapping = TextWrapping.NoWrap
            };
        }

        private Button FilledButton(string text, Color bg, Color fg, Action onClick)
        {
            var b = new Button
            {
                Content = text, Height = 34, MinWidth = 96, Padding = new Thickness(16, 0, 16, 0),
                Foreground = new SolidColorBrush(fg), Background = new SolidColorBrush(bg),
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 13, FontFamily = Ui,
                Template = RoundTemplate(6)
            };
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        private void SetButton(Button b, string text, Color bg, Color fg)
        {
            b.Content = text;
            b.Background = new SolidColorBrush(bg);
            b.Foreground = new SolidColorBrush(fg);
        }

        private Button TextButton(string text, Action onClick)
        {
            var b = new Button
            {
                Content = text, Width = 30, Height = 30, Foreground = new SolidColorBrush(Muted),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FontSize = 13, Template = RoundTemplate(6)
            };
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        private static ControlTemplate RoundTemplate(double radius)
        {
            var t = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            t.VisualTree = border;
            return t;
        }

        private static Color IdealOn(Color c)
        {
            double L = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
            return L > 150 ? Color.FromRgb(0x0A, 0x22, 0x30) : Colors.White;
        }
    }
}
