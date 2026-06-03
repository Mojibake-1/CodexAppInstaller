using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;

namespace CodexAppInstaller
{
    internal sealed class MainWindow : Window
    {
        private enum JobState { Idle, Resolving, Installing, Success, Failed }

        private readonly bool _forRender;
        private bool _dark;
        private Color _accent;
        private Palette _p;
        private UiContext _ctx;
        private IDisposable _themeHook;

        private readonly InstallerBackend _backend = new InstallerBackend();
        private readonly string _scriptPath;
        private string _targetDir;
        private JobState _state = JobState.Idle;
        private bool _resolveOnly;
        private bool _running;
        private bool _cancelled;
        private volatile bool _windowClosed;
        private string _pendingError;

        // UI references
        private DockPanel _root;
        private TextBlock _pathText;
        private TextBlock _diskText;
        private Switch _keepTemp;
        private ButtonParts _checkBtn, _installBtn, _openBtn, _changeBtn;
        private Border _disclosure;
        private TextBlock _chevron;
        private Border _consoleCard;
        private ConsoleLog _console;
        private bool _consoleOpen;
        private double _collapsedHeight;
        private bool _heightCached;
        private const double ConsoleExtra = 188;

        private System.Windows.Shapes.Ellipse _statusDot;
        private SolidColorBrush _statusDotBrush;
        private TextBlock _statusText;
        private StepPips _pips;
        private ProgressBar _bar;
        private Border _shimmer;
        private SolidColorBrush _footerWash;
        private Border _footer;
        private TextBlock _telemetry;
        private Storyboard _shimmerSb, _dotPulseSb;

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private DateTime _jobStart;
        private int _curStep, _totStep;

        public bool HasBackend { get { return File.Exists(_scriptPath); } }
        public FrameworkElement RootForRender { get { return _root; } }
        public Brush CanvasBrush { get { return _p.B("canvas"); } }

        public MainWindow() : this(false, null) { }

        public MainWindow(bool forRender, bool? forceDark)
        {
            _forRender = forRender;
            _accent = AccentColors.Get();
            _dark = forceDark.HasValue ? forceDark.Value : ThemeWatcher.IsSystemDark();
            _p = new Palette(_dark, _accent);
            _ctx = new UiContext(_p);

            _scriptPath = InstallerBackend.ResolveScript();
            _targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Codex");

            // Low-end / software-rendered machines (and reduced-motion): turn motion off so
            // we never spin a 60fps storyboard on a CPU-only renderer. (Render/self-test
            // modes already disabled motion in Program before constructing.)
            if (Perf.NoMotion) Motion.Enabled = false;

            BuildUi();

            if (forRender)
            {
                _root.Background = _p.B("canvas");
                return;
            }

            Title = "Codex 安装程序";
            Width = 720; Height = 560;
            MinWidth = 680; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;            // MUST stay false for Mica + native shadow/snap
            ResizeMode = ResizeMode.CanResize;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FontFamily = Fonts.Text;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            Background = _p.B("canvas");           // fallback before Mica applies
            Content = _root;

            var chrome = new WindowChrome
            {
                CaptionHeight = 40,
                // Win11 needs the glass fully extended (-1) so Mica reaches the edges; on
                // Windows 10 a thin frame gives the standard drop shadow without glass artifacts.
                GlassFrameThickness = Os.IsWindows11 ? new Thickness(-1) : new Thickness(1),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(Os.IsWindows11 ? 8 : 0),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            MicaBackdrop.Apply(this, _dark);   // no-ops on Windows 10 (stays solid canvas)
            WindowCorners.Apply(this);          // no-ops on Windows 10 (square corners)

            Loaded += OnLoaded;
            Closed += OnClosed;

            _backend.Segment += OnSegment;
            _backend.Exited += OnExited;

            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += delegate { UpdateTelemetry(); };

            if (!HasBackend)
            {
                SetStatus("未找到 Install-CodexApp.ps1", "danger");
                _checkBtn.Button.IsEnabled = false;
                _installBtn.Button.IsEnabled = false;
            }
            else
            {
                SetStatus("就绪", "statusDotIdle");
            }
        }

        // ---- layout -------------------------------------------------------------

        private void BuildUi()
        {
            _root = new DockPanel { LastChildFill = true };

            _root.Children.Add(Docked(BuildTitleBar(), Dock.Top));

            var content = new Grid { Margin = new Thickness(24, 6, 24, 16) };
            for (int i = 0; i < 6; i++) content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Insert(5, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Add(content, BuildHero(), 0);
            Add(content, BuildConfigCard(), 1);
            Add(content, BuildActionRow(), 2);
            Add(content, BuildDisclosure(), 3);
            Add(content, BuildConsole(), 4);   // sits directly under Details when expanded
            // row 5 = star spacer (between console and footer)
            Add(content, BuildFooter(), 6);

            _root.Children.Add(content);
        }

        private UIElement BuildTitleBar()
        {
            var grid = new Grid { Height = 40, Background = Brushes.Transparent };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            left.Children.Add(Ui.Icon(_ctx, Glyph.App, 15, "accent"));
            var word = Ui.Tb(_ctx, "Codex 安装程序", Fonts.Small, 13, "textPrimary", FontWeights.SemiBold);
            word.Margin = new Thickness(8, 0, 0, 0);
            word.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(word);
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var caption = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            caption.Children.Add(Ui.CaptionButton(_ctx, Glyph.Minimize, false, delegate { SystemCommands.MinimizeWindow(this); }));
            caption.Children.Add(Ui.CaptionButton(_ctx, Glyph.Close, true, delegate { Close(); }));
            Grid.SetColumn(caption, 1);
            grid.Children.Add(caption);

            return grid;
        }

        private UIElement BuildHero()
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tile = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(11),
                VerticalAlignment = VerticalAlignment.Top
            };
            _ctx.Themed(delegate
            {
                tile.Background = new LinearGradientBrush(_p.C("accentTileTop"), _p.C("accentTileBottom"), 90);
            });
            tile.Child = Ui.Icon(_ctx, Glyph.App, 24, "accentText");
            Grid.SetColumn(tile, 0);
            grid.Children.Add(tile);

            var text = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var h1 = Ui.Tb(_ctx, "安装 Codex", Fonts.Display, 28, "textPrimary", FontWeights.SemiBold);
            h1.LineHeight = 36;
            var sub = Ui.Tb(_ctx, "从 Microsoft Store 下载最新版本，校验后为你安装到本地目录。",
                Fonts.Text, 13, "textMuted");
            sub.Margin = new Thickness(0, 4, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(h1);
            text.Children.Add(sub);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        private UIElement BuildConfigCard()
        {
            var card = Ui.Card(_ctx);
            card.Margin = new Thickness(0, 0, 0, 16);
            var stack = new StackPanel { Margin = new Thickness(16) };
            card.Child = stack;

            // Row 1: install location
            stack.Children.Add(Ui.Tb(_ctx, "安装位置", Fonts.Text, 12, "textMuted"));

            var pill = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Height = 36,
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(10, 0, 6, 0)
            };
            _ctx.Themed(delegate { pill.Background = _p.B("surfaceInput"); pill.BorderBrush = _p.B("border"); });

            var pillGrid = new Grid();
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var folder = Ui.Icon(_ctx, Glyph.Folder, 15, "textMuted");
            folder.Margin = new Thickness(0, 0, 8, 0);
            folder.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(folder, 0);
            pillGrid.Children.Add(folder);

            _pathText = Ui.Tb(_ctx, ShortenPath(_targetDir), Fonts.Text, 13, "textPrimary");
            _pathText.VerticalAlignment = VerticalAlignment.Center;
            _pathText.TextTrimming = TextTrimming.CharacterEllipsis;
            _pathText.ToolTip = _targetDir;
            Grid.SetColumn(_pathText, 1);
            pillGrid.Children.Add(_pathText);

            _changeBtn = Ui.Button(_ctx, BtnStyle.Ghost, Glyph.Pencil, "更改", 0, 28);
            _changeBtn.Button.Padding = new Thickness(8, 0, 8, 0);
            _changeBtn.GlyphTb.FontSize = 13;
            _changeBtn.Button.Click += delegate { OnBrowse(); };
            Grid.SetColumn(_changeBtn.Button, 2);
            pillGrid.Children.Add(_changeBtn.Button);

            pill.Child = pillGrid;
            stack.Children.Add(pill);

            _diskText = Ui.Tb(_ctx, "", Fonts.Small, 12, "textMuted");
            _diskText.Margin = new Thickness(2, 6, 0, 0);
            stack.Children.Add(_diskText);
            UpdateDiskFree();

            stack.Children.Add(Ui.Divider(_ctx, 14, 14));

            // Row 2: options
            var opt = new StackPanel { Orientation = Orientation.Horizontal };
            _keepTemp = new Switch(_ctx);
            opt.Children.Add(_keepTemp);
            var optText = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            optText.Children.Add(Ui.Tb(_ctx, "安装后保留临时文件", Fonts.Text, 13, "textPrimary"));
            var help = Ui.Tb(_ctx, "在磁盘上保留下载的 MSIX 和解压出的文件，便于检查。", Fonts.Small, 12, "textMuted");
            help.Margin = new Thickness(0, 1, 0, 0);
            optText.Children.Add(help);
            opt.Children.Add(optText);
            stack.Children.Add(opt);

            return card;
        }

        private UIElement BuildActionRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _openBtn = Ui.Button(_ctx, BtnStyle.Secondary, Glyph.OpenFolder, "打开文件夹", 0, 36);
            _openBtn.Button.Margin = new Thickness(0, 0, 8, 0);
            _openBtn.Button.Visibility = Visibility.Collapsed;
            _openBtn.Button.Click += delegate { OnOpenFolder(); };
            row.Children.Add(_openBtn.Button);

            _checkBtn = Ui.Button(_ctx, BtnStyle.Secondary, Glyph.Search, "检查最新版本", 0, 36);
            _checkBtn.Button.Margin = new Thickness(0, 0, 8, 0);
            _checkBtn.Button.Click += delegate { StartJob(true); };
            row.Children.Add(_checkBtn.Button);

            _installBtn = Ui.Button(_ctx, BtnStyle.Primary, Glyph.Download, "安装", 120, 36);
            _installBtn.Button.Click += delegate { OnPrimaryClick(); };
            row.Children.Add(_installBtn.Button);

            return row;
        }

        private UIElement BuildDisclosure()
        {
            _disclosure = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = Ui.Tb(_ctx, "详细信息", Fonts.Text, 13, "textSecondary");
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(label, 0);
            g.Children.Add(label);

            _chevron = Ui.Icon(_ctx, Glyph.Chevron, 12, "textMuted");
            _chevron.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(_chevron, 1);
            g.Children.Add(_chevron);

            _disclosure.Child = g;
            var hoverBg = new SolidColorBrush(Colors.Transparent);
            _disclosure.Background = hoverBg;
            _disclosure.MouseEnter += delegate { Motion.ColorTo(hoverBg, Ui.WithAlpha(_p.C("textPrimary"), 0.06), 100); };
            _disclosure.MouseLeave += delegate { Motion.ColorTo(hoverBg, Colors.Transparent, 120); };
            _disclosure.MouseLeftButtonUp += delegate { ToggleConsole(); };
            return _disclosure;
        }

        private UIElement BuildConsole()
        {
            _console = new ConsoleLog(_ctx)
            {
                Height = 168,
                Margin = new Thickness(0, 8, 0, 4),
                Visibility = Visibility.Collapsed
            };
            _consoleCard = _console;
            return _console;
        }

        private UIElement BuildFooter()
        {
            _footer = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 10, 0, 0),
                Margin = new Thickness(0, 8, 0, 0)
            };
            _footerWash = new SolidColorBrush(Colors.Transparent);
            _footer.Background = _footerWash;
            _ctx.Themed(delegate { _footer.BorderBrush = _p.B("dividerHairline"); });

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var statusWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _statusDotBrush = new SolidColorBrush(_p.C("statusDotIdle"));
            _statusDot = new System.Windows.Shapes.Ellipse { Width = 9, Height = 9, Fill = _statusDotBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) };
            statusWrap.Children.Add(_statusDot);
            _statusText = Ui.Tb(_ctx, "就绪", Fonts.Text, 13, "textPrimary");
            _statusText.VerticalAlignment = VerticalAlignment.Center;
            statusWrap.Children.Add(_statusText);
            Grid.SetColumn(statusWrap, 0);
            top.Children.Add(statusWrap);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _telemetry = Ui.Tb(_ctx, "", Fonts.Mono, 11.5, "logDim");
            _telemetry.VerticalAlignment = VerticalAlignment.Center;
            _telemetry.Margin = new Thickness(0, 0, 12, 0);
            right.Children.Add(_telemetry);
            _pips = new StepPips(_ctx);
            right.Children.Add(_pips);
            Grid.SetColumn(right, 1);
            top.Children.Add(right);

            Grid.SetRow(top, 0);
            grid.Children.Add(top);

            // progress track + shimmer overlay (same cell)
            var barHost = new Grid { Margin = new Thickness(2, 0, 2, 0) };
            _bar = Ui.ProgressBar(_ctx, 4);
            _shimmer = new Border { Height = 4, CornerRadius = new CornerRadius(2), Visibility = Visibility.Collapsed };
            barHost.Children.Add(_bar);
            barHost.Children.Add(_shimmer);
            Grid.SetRow(barHost, 1);
            grid.Children.Add(barHost);

            _footer.Child = grid;
            return _footer;
        }

        // ---- lifecycle ----------------------------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_heightCached) { _collapsedHeight = ActualHeight; _heightCached = true; }
            _themeHook = ThemeWatcher.Attach(this, OnSystemThemeChanged);

            // Entrance: hero, card, action row settle in.
            var content = _root.Children.Count > 1 ? _root.Children[1] as Grid : null;
            if (content != null)
            {
                Motion.FadeInSlideUp(content.Children[0] as FrameworkElement, 14, 240, 0);
                Motion.FadeInSlideUp(content.Children[1] as FrameworkElement, 14, 260, 40);
                Motion.FadeInSlideUp(content.Children[2] as FrameworkElement, 14, 260, 80);
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _windowClosed = true;   // stop any queued backend->UI updates from touching dead controls
            _running = false;
            _backend.Segment -= OnSegment;
            _backend.Exited -= OnExited;
            try { _backend.Cancel(); } catch { }
            Motion.StopShimmer(_shimmerSb, _shimmer);
            Motion.StopPulse(_dotPulseSb, _statusDot);
            _timer.Stop();
            if (_themeHook != null) _themeHook.Dispose();
        }

        private void OnSystemThemeChanged(bool dark)
        {
            _dark = dark;
            _accent = AccentColors.Get();
            _p.Apply(dark, _accent);
            _ctx.Reapply(_p);
            Background = _p.B("canvas");
            MicaBackdrop.SetDarkTitleBar(this, dark);
            // keep dynamic states correct after a re-theme
            if (_running) MorphPrimary(true);
            RefreshStatusColor();
        }

        // ---- actions ------------------------------------------------------------

        private void OnPrimaryClick()
        {
            if (_running) OnCancel();
            else StartJob(false);
        }

        private const string AppFolderName = "Codex";

        private void OnBrowse()
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // The picker should open at the PARENT of the current target (the target already
            // ends with the Codex folder), so the user re-picks the containing directory.
            string initial = ParentOfTarget(_targetDir);

            string picked = FolderPicker.Pick(hwnd, "选择安装位置（将在其中创建 Codex 文件夹）", initial);
            if (string.IsNullOrEmpty(picked)) return;

            // Treat the chosen folder as the parent and install into "<picked>\Codex",
            // unless the user already picked a folder named Codex.
            string leaf = "";
            try { leaf = Path.GetFileName(picked.TrimEnd('\\', '/')); } catch { }
            _targetDir = string.Equals(leaf, AppFolderName, StringComparison.OrdinalIgnoreCase)
                ? picked
                : Path.Combine(picked, AppFolderName);

            _pathText.Text = ShortenPath(_targetDir);
            _pathText.ToolTip = _targetDir;
            UpdateDiskFree();
        }

        private static string ParentOfTarget(string target)
        {
            try
            {
                string trimmed = target.TrimEnd('\\', '/');
                string leaf = Path.GetFileName(trimmed);
                if (string.Equals(leaf, AppFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(trimmed);
                    if (!string.IsNullOrEmpty(parent)) return parent;
                }
            }
            catch { }
            return target;
        }

        private void OnOpenFolder()
        {
            try
            {
                if (!Directory.Exists(_targetDir)) Directory.CreateDirectory(_targetDir);
                Process.Start("explorer.exe", "\"" + _targetDir + "\"");
            }
            catch { }
        }

        private void OnCancel()
        {
            if (_cancelled) return;   // idempotent: ignore repeated Cancel clicks
            _cancelled = true;
            try { _backend.Cancel(); } catch { }
            _console.Err("已被用户取消。");
            SetStatus("正在取消…", "textMuted");
        }

        private void StartJob(bool resolveOnly)
        {
            if (_running || !HasBackend) return;
            _resolveOnly = resolveOnly;
            _running = true;
            _finished = false;
            _cancelled = false;
            _pendingError = null;
            _pkgFriendly = null;   // don't carry a stale package name into a new run
            _curStep = 0;
            _totStep = resolveOnly ? 1 : 5;
            _jobStart = DateTime.Now;

            _console.Clear();
            if (!_consoleOpen) { /* keep collapsed; user can open Details */ }

            _state = resolveOnly ? JobState.Resolving : JobState.Installing;
            SetStatus(resolveOnly ? "正在解析最新版本…" : "正在启动…", "accent", busy: true);
            _pips.SetState(_totStep, 0);
            SetIndeterminate(true);

            _checkBtn.Button.IsEnabled = false;
            _changeBtn.Button.IsEnabled = false;
            _keepTemp.IsEnabled = false;
            _openBtn.Button.Visibility = Visibility.Collapsed;

            if (resolveOnly) _installBtn.Button.IsEnabled = false;
            else MorphPrimary(true); // Install -> Cancel

            _timer.Start();

            try
            {
                _backend.Start(_scriptPath, resolveOnly, _targetDir, _keepTemp.IsChecked == true);
            }
            catch (Exception ex)
            {
                _console.Err("启动失败：" + ex.Message);
                FinishJob(-1);
            }
        }

        // ---- backend stream -> UI ----------------------------------------------

        private void OnSegment(string text, bool cr)
        {
            if (_windowClosed) return;
            Dispatcher.BeginInvoke(new Action<string, bool>(HandleLine), DispatcherPriority.Background, text, cr);
        }

        private void OnExited(int code)
        {
            if (_windowClosed) return;
            Dispatcher.BeginInvoke(new Action<int>(FinishJob), DispatcherPriority.Normal, code);
        }

        private void HandleLine(string text, bool cr)
        {
            if (text == null || _windowClosed) return;

            double pct;
            if (OutputParser.IsProgressBar(text) || (cr && OutputParser.TryPercent(text, out pct)))
            {
                if (OutputParser.TryPercent(text, out pct))
                {
                    SetIndeterminate(false);
                    Motion.ProgressTo(_bar, pct);
                    string name = _pkgFriendly ?? "安装包";
                    SetStatus(string.Format(CultureInfo.InvariantCulture, "正在下载 {0} — {1:0.0}%", name, pct), "accent", busy: true);
                }
                return; // never log the raw ASCII bar
            }
            if (cr) return; // other transient repaints: ignore

            int idx, tot;
            string title;
            if (OutputParser.TryStep(text, out idx, out tot, out title))
            {
                _curStep = idx; _totStep = tot;
                _pips.SetState(tot, idx);
                if (title.IndexOf("Download", StringComparison.OrdinalIgnoreCase) < 0)
                    SetIndeterminate(true);
                string zhTitle = Lang.Step(title);
                SetStatus(zhTitle, "accent", busy: true);
                _console.Step(zhTitle);
                return;
            }

            string val;
            if (OutputParser.IsHashVerified(text)) { _console.Ok("安装包哈希校验通过"); SetStatus("正在校验安装包", "accent", busy: true); return; }
            if (OutputParser.IsSuccess(text)) { _console.Ok("安装成功"); return; }
            if (OutputParser.IsResolved(text)) { _console.Ok("已解析最新安装包"); return; }
            if (OutputParser.TryError(text, out val)) { _pendingError = val; _console.Err("错误：" + val); return; }
            if (OutputParser.IsFailure(text)) { _console.Err("安装失败"); return; }

            if (OutputParser.TryFile(text, out val))
            {
                _console.KeyValue("文件", val);
                string ver; OutputParser.TryVersion(val, out ver);
                _pkgFriendly = string.IsNullOrEmpty(ver) ? "Codex" : ("OpenAI.Codex " + ver);
                return;
            }
            if (OutputParser.TrySize(text, out val)) { _console.KeyValue("大小", val); return; }
            if (OutputParser.TryUrl(text, out val)) { _console.KeyValue("链接", val); return; }
            if (OutputParser.IsRule(text)) return; // hide ===== rules

            string t = text.Trim();
            if (t.Length > 0) _console.Line(text.TrimEnd());
        }

        private string _pkgFriendly;

        private bool _finished;

        private void FinishJob(int exitCode)
        {
            if (_windowClosed || _finished) return;   // guard close + Cancel/Exited double-finalize
            _finished = true;
            _running = false;
            _timer.Stop();

            _checkBtn.Button.IsEnabled = HasBackend;
            _changeBtn.Button.IsEnabled = true;
            _keepTemp.IsEnabled = true;
            _installBtn.Button.IsEnabled = HasBackend;
            MorphPrimary(false); // Cancel -> Install

            if (_cancelled)
            {
                _state = JobState.Idle;
                SetIndeterminate(false);
                Motion.ProgressTo(_bar, 0);
                _pips.Reset();
                StopPulse();
                SetStatus("已取消", "statusDotIdle");
                return;
            }

            if (exitCode == 0)
            {
                _state = _resolveOnly ? JobState.Idle : JobState.Success;
                SetIndeterminate(false);
                Motion.ProgressTo(_bar, 100);
                _pips.SetState(_totStep, _totStep);
                StopPulse();
                if (_resolveOnly)
                {
                    SetStatus("最新版本已就绪", "success");
                }
                else
                {
                    SetStatus("安装完成", "success");
                    _openBtn.Button.Visibility = Visibility.Visible;
                    FlashSuccess();
                }
            }
            else
            {
                _state = JobState.Failed;
                SetIndeterminate(false);
                Motion.ProgressTo(_bar, 0);
                StopPulse();
                string msg = _pendingError != null ? ("失败 — " + _pendingError) : ("失败（退出码 " + exitCode + "）");
                SetStatus(msg, "danger");
                FlashFailure();
            }
        }

        // ---- status / progress visuals -----------------------------------------

        private void SetStatus(string text, string dotRole, bool busy = false)
        {
            if (_statusText != null) _statusText.Text = text;
            if (_statusDotBrush != null) _statusDotBrush.Color = _p.C(dotRole);
            if (busy) StartPulse(); else StopPulse();
        }

        private void RefreshStatusColor()
        {
            // re-pull the dot colour for the current state on a theme flip
            string role = _state == JobState.Success ? "success"
                : _state == JobState.Failed ? "danger"
                : (_running ? "accent" : "statusDotIdle");
            if (_statusDotBrush != null) _statusDotBrush.Color = _p.C(role);
        }

        private void StartPulse()
        {
            if (_dotPulseSb == null && !_forRender) _dotPulseSb = Motion.Pulse(_statusDot);
        }
        private void StopPulse()
        {
            Motion.StopPulse(_dotPulseSb, _statusDot);
            _dotPulseSb = null;
        }

        private void SetIndeterminate(bool on)
        {
            if (on)
            {
                _bar.Visibility = Visibility.Collapsed;
                _shimmer.Visibility = Visibility.Visible;
                if (_shimmerSb == null)
                    _shimmerSb = Motion.StartShimmer(_shimmer, _p.C("dividerHairline"), _p.C("accent"));
            }
            else
            {
                Motion.StopShimmer(_shimmerSb, _shimmer);
                _shimmerSb = null;
                _shimmer.Visibility = Visibility.Collapsed;
                _bar.Visibility = Visibility.Visible;
            }
        }

        private void FlashSuccess()
        {
            var brushes = _bar.Tag as Brush[];
            if (brushes != null && brushes.Length > 1)
            {
                var fill = brushes[1] as SolidColorBrush;
                if (fill != null && !fill.IsFrozen) Motion.Flash(fill, _p.C("success"), _p.C("accent"), 180, 600);
            }
        }

        private void FlashFailure()
        {
            if (_footerWash != null) Motion.Flash(_footerWash, Ui.WithAlpha(_p.C("danger"), 0.10), Colors.Transparent, 200, 800);
        }

        // ---- primary button morph -----------------------------------------------

        private void MorphPrimary(bool toCancel)
        {
            if (toCancel)
            {
                Ui.Recolor(_ctx, _installBtn, BtnStyle.Danger);
                _installBtn.GlyphTb.Text = Glyph.Cancel;
                _installBtn.LabelTb.Text = "取消";
            }
            else
            {
                Ui.Recolor(_ctx, _installBtn, BtnStyle.Primary);
                _installBtn.GlyphTb.Text = Glyph.Download;
                _installBtn.LabelTb.Text = "安装";
            }
        }

        // ---- helpers ------------------------------------------------------------

        private void ToggleConsole()
        {
            bool opening = !_consoleOpen;
            // Refresh the collapsed-height cache from the live size (the window may have
            // been resized since it was first cached), so collapse returns to the right height.
            if (opening) _collapsedHeight = ActualHeight;
            _consoleOpen = opening;
            Motion.RotateTo(_chevron, _consoleOpen ? 180 : 0, 200);
            if (_forRender) { _consoleCard.Visibility = _consoleOpen ? Visibility.Visible : Visibility.Collapsed; return; }

            if (_consoleOpen)
            {
                _consoleCard.Visibility = Visibility.Visible;
                Motion.HeightTo(this, _collapsedHeight + ConsoleExtra, 220);
            }
            else
            {
                Motion.HeightTo(this, _collapsedHeight, 220, delegate { _consoleCard.Visibility = Visibility.Collapsed; });
            }
        }

        private void UpdateTelemetry()
        {
            if (!_running || _windowClosed) return;
            TimeSpan el = DateTime.Now - _jobStart;
            string step = _totStep > 1 ? string.Format("步骤 {0}/{1}   ", Math.Max(1, _curStep), _totStep) : "";
            _telemetry.Text = string.Format("{0}{1:00}:{2:00}", step, (int)el.TotalMinutes, el.Seconds);
        }

        private void UpdateDiskFree()
        {
            try
            {
                string root = Path.GetPathRoot(_targetDir);
                var di = new DriveInfo(root);
                _diskText.Text = string.Format("{1} 可用空间 {0}", HumanSize(di.AvailableFreeSpace), di.Name.TrimEnd('\\'));
            }
            catch { _diskText.Text = ""; }
        }

        private static string HumanSize(double bytes)
        {
            if (bytes >= 1L << 30) return string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", bytes / (1L << 30));
            if (bytes >= 1L << 20) return string.Format(CultureInfo.InvariantCulture, "{0:0} MB", bytes / (1L << 20));
            return string.Format(CultureInfo.InvariantCulture, "{0:0} KB", bytes / 1024.0);
        }

        private string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 52) return path;
            try
            {
                string root = Path.GetPathRoot(path);
                string leaf = Path.GetFileName(path.TrimEnd('\\'));
                string parent = Path.GetFileName(Path.GetDirectoryName(path).TrimEnd('\\'));
                return root + "...\\" + parent + "\\" + leaf;
            }
            catch { return path; }
        }

        // Drive the UI into a representative "installing" state for off-screen QA renders.
        public void SimulateBusyForRender()
        {
            _running = true; _totStep = 5; _curStep = 2;
            _pips.SetState(5, 2);
            MorphPrimary(true);
            _checkBtn.Button.IsEnabled = false;
            _changeBtn.Button.IsEnabled = false;
            _keepTemp.IsEnabled = false;
            SetIndeterminate(false);
            _bar.Value = 42.3;
            _statusText.Text = "正在下载 OpenAI.Codex 1.4.2 — 42.3%";
            _statusDotBrush.Color = _p.C("accent");
            _telemetry.Text = "步骤 2/5   00:07";

            _consoleOpen = true;
            _consoleCard.Visibility = Visibility.Visible;
            _chevron.RenderTransform = new RotateTransform(180) { };
            _chevron.RenderTransformOrigin = new Point(0.5, 0.5);
            _console.Step(Lang.Step("Resolve fresh Microsoft Store URL"));
            _console.KeyValue("文件", "OpenAI.Codex_1.4.2.0_x64__2p2nqsd0c76g0.msix");
            _console.KeyValue("大小", "118.7 MB");
            _console.Ok("安装包哈希校验通过");
            _console.Step(Lang.Step("Download MSIX"));
            _console.Line("正在从 Microsoft Store 分发 CDN 获取 …");
        }

        // ---- tiny layout helpers -------------------------------------------------

        private static UIElement Docked(UIElement el, Dock side) { DockPanel.SetDock(el, side); return el; }
        private static void Add(Grid g, UIElement el, int row) { Grid.SetRow(el, row); g.Children.Add(el); }
    }
}
