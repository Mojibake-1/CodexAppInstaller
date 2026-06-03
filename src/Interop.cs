using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace CodexAppInstaller
{
    // Windows version helpers. With the bundled app.manifest (Win10/11 GUID) the
    // real build number is reported; we also read the registry as a belt-and-suspenders
    // fallback in case the manifest is stripped.
    internal static class Os
    {
        private static readonly Lazy<bool> _win11 = new Lazy<bool>(ComputeWin11);

        public static bool IsWindows11 { get { return _win11.Value; } }

        private static bool ComputeWin11()
        {
            // Test hook: CODEX_FORCE_WIN10=1 forces the Windows-10 fallback path on a Win11 box.
            try { if (Environment.GetEnvironmentVariable("CODEX_FORCE_WIN10") == "1") return false; } catch { }
            int build = BuildNumber();
            return build >= 22000;
        }

        public static int BuildNumber()
        {
            try
            {
                Version v = Environment.OSVersion.Version;
                if (v.Major >= 10 && v.Build >= 22000) return v.Build;
            }
            catch { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        object cb = k.GetValue("CurrentBuildNumber");
                        int n;
                        if (cb != null && int.TryParse(cb.ToString(), out n)) return n;
                    }
                }
            }
            catch { }
            try { return Environment.OSVersion.Version.Build; } catch { return 0; }
        }
    }

    // Detects graphics capability so we can run a "lite" mode on low-end / software-rendered
    // machines (no GPU, VMs, Remote Desktop) where blur shadows and 60fps storyboards are
    // expensive. Also honours the system reduced-motion setting.
    internal static class Perf
    {
        // RenderCapability.Tier is reported in the high word: 0 = software (no HW accel).
        public static readonly bool SoftwareRender = (System.Windows.Media.RenderCapability.Tier >> 16) == 0;

        public static readonly bool Forced =
            SafeEnv("CODEX_INSTALLER_LITE") == "1";

        // No shadows / no continuous animations on low-end machines.
        public static readonly bool Lite = SoftwareRender || Forced;

        // Disable motion either in lite mode or when the user asked for reduced motion.
        public static readonly bool NoMotion = Lite || !ClientAnimationOn();

        private static bool ClientAnimationOn()
        {
            try { return System.Windows.SystemParameters.ClientAreaAnimation; }
            catch { return true; }
        }

        private static string SafeEnv(string name)
        {
            try { return Environment.GetEnvironmentVariable(name); } catch { return null; }
        }
    }

    // Applies a Mica system backdrop (Acrylic fallback) and the immersive dark-mode
    // title bar to a WPF window on Windows 11. Verified against csc + the WPF 4.x refs.
    internal static class MicaBackdrop
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Win10 2004+/Win11
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;     // Win11 22000+
        private const int DWMSBT_MAINWINDOW = 2;              // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3;         // Acrylic

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int L, R, T, B; }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);

        // Apply (or defer until the HWND exists). Returns true when applied/deferred.
        public static bool Apply(Window w, bool dark)
        {
            if (w == null || !Os.IsWindows11) return false;

            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero)
            {
                EventHandler h = null;
                h = delegate { w.SourceInitialized -= h; ApplyCore(w, dark); };
                w.SourceInitialized += h;
                return true;
            }
            return ApplyCore(w, dark);
        }

        private static bool ApplyCore(Window w, bool dark)
        {
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return false;

            w.Background = Brushes.Transparent; // let the backdrop show through

            MARGINS m = new MARGINS { L = -1, R = -1, T = -1, B = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            int backdrop = DWMSBT_MAINWINDOW;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            if (hr != 0)
            {
                int acrylic = DWMSBT_TRANSIENTWINDOW;
                hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref acrylic, sizeof(int));
            }
            return hr == 0;
        }

        // Re-apply only the dark/light title-bar flag (used on live theme flips).
        public static void SetDarkTitleBar(Window w, bool dark)
        {
            if (w == null) return;
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
    }

    // Forces Windows 11 rounded corners on a WPF window.
    internal static class WindowCorners
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Apply(Window w)
        {
            if (w == null || !Os.IsWindows11) return;
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero)
            {
                EventHandler h = null;
                h = delegate { w.SourceInitialized -= h; ApplyCore(new WindowInteropHelper(w).Handle); };
                w.SourceInitialized += h;
                return;
            }
            ApplyCore(hwnd);
        }

        private static void ApplyCore(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }
    }

    // Reads the live Windows accent color from the registry (no WinRT).
    internal static class AccentColors
    {
        private static readonly Color Fallback = Color.FromRgb(0x00, 0x78, 0xD4); // Windows blue

        public static Color Get()
        {
            Color c;
            if (TryDword(@"Software\Microsoft\Windows\DWM", "AccentColor", out c)) return c;
            if (TryDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", "AccentColorMenu", out c)) return c;
            return Fallback;
        }

        private static bool TryDword(string sub, string name, out Color color)
        {
            color = Fallback;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(sub))
                {
                    if (key == null) return false;
                    object raw = key.GetValue(name);
                    if (raw == null) return false;

                    // Stored as 0xAABBGGRR (ABGR).
                    uint abgr = unchecked((uint)Convert.ToInt32(raw));
                    byte a = (byte)((abgr >> 24) & 0xFF);
                    byte b = (byte)((abgr >> 16) & 0xFF);
                    byte g = (byte)((abgr >> 8) & 0xFF);
                    byte r = (byte)(abgr & 0xFF);
                    if (a == 0) a = 0xFF;
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }
            catch { return false; }
        }
    }

    // Detects light/dark mode and raises live changes via WM_SETTINGCHANGE.
    internal static class ThemeWatcher
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const int WM_SETTINGCHANGE = 0x001A;

        public static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key == null) return false;
                    object v = key.GetValue("AppsUseLightTheme");
                    if (v is int) return ((int)v) == 0; // 0 = dark
                    return false;
                }
            }
            catch { return false; }
        }

        // Hook the window; invokes onChanged(isDark) only when the state flips.
        public static IDisposable Attach(Window window, Action<bool> onChanged)
        {
            if (window == null || onChanged == null) return new Noop();

            HwndSource src = (HwndSource)PresentationSource.FromVisual(window);
            if (src == null)
            {
                Hook[] holder = new Hook[1];
                EventHandler init = null;
                init = delegate
                {
                    window.SourceInitialized -= init;
                    holder[0] = new Hook(window, onChanged);
                };
                window.SourceInitialized += init;
                return new Deferred(delegate
                {
                    window.SourceInitialized -= init;
                    if (holder[0] != null) holder[0].Dispose();
                });
            }
            return new Hook(window, onChanged);
        }

        private sealed class Hook : IDisposable
        {
            private HwndSource _src;
            private readonly Action<bool> _cb;
            private bool _lastDark;

            public Hook(Window w, Action<bool> cb)
            {
                _cb = cb;
                _src = (HwndSource)PresentationSource.FromVisual(w);
                _lastDark = IsSystemDark();
                if (_src != null) _src.AddHook(WndProc);
            }

            private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
            {
                if (msg == WM_SETTINGCHANGE)
                {
                    bool now = IsSystemDark();
                    if (now != _lastDark) { _lastDark = now; _cb(now); }
                }
                return IntPtr.Zero;
            }

            public void Dispose()
            {
                if (_src != null) { _src.RemoveHook(WndProc); _src = null; }
            }
        }

        private sealed class Deferred : IDisposable
        {
            private Action _d;
            public Deferred(Action d) { _d = d; }
            public void Dispose() { Action d = _d; _d = null; if (d != null) d(); }
        }

        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
