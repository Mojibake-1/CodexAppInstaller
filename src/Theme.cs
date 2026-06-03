using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace CodexAppInstaller
{
    // The full role-based palette (dark + light) from the design brief, with the
    // live Windows accent colour injected over the static accent roles. Brushes are
    // frozen for cheap, thread-safe static assignment.
    internal sealed class Palette
    {
        private readonly Dictionary<string, Color> _c = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SolidColorBrush> _b = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);

        public bool IsDark { get; private set; }
        public Color SystemAccent { get; private set; }

        public Palette(bool dark, Color systemAccent) { Apply(dark, systemAccent); }

        public void Apply(bool dark, Color systemAccent)
        {
            IsDark = dark;
            SystemAccent = systemAccent;
            _c.Clear();
            _b.Clear();

            var src = dark ? Dark : Light;
            foreach (var kv in src) _c[kv.Key] = Hex(kv.Value);

            // Inject the live system accent.
            Color accent = dark ? Lerp(systemAccent, Colors.White, 0.45) : systemAccent;
            _c["accent"] = accent;
            _c["accentHover"] = Lerp(accent, Colors.White, 0.14);
            _c["accentPressed"] = Lerp(accent, Colors.Black, 0.12);
            _c["accentText"] = IdealTextOn(accent);
            _c["accentTileTop"] = Lerp(accent, Colors.White, 0.08);
            _c["accentTileBottom"] = Lerp(accent, Colors.Black, 0.18);
            _c["focusRing"] = accent;
            _c["logAccent"] = dark ? accent : Lerp(systemAccent, Colors.White, 0.45);
        }

        public Color C(string role)
        {
            Color c;
            return _c.TryGetValue(role, out c) ? c : Colors.Magenta; // magenta flags a missing role
        }

        // Cached frozen brush for static assignment.
        public SolidColorBrush B(string role)
        {
            SolidColorBrush b;
            if (_b.TryGetValue(role, out b)) return b;
            b = new SolidColorBrush(C(role));
            b.Freeze();
            _b[role] = b;
            return b;
        }

        // A fresh, UNFROZEN brush (for properties you will animate).
        public SolidColorBrush Mutable(string role) { return new SolidColorBrush(C(role)); }

        // ---- helpers ------------------------------------------------------------

        public static Color Hex(string s)
        {
            s = s.TrimStart('#');
            byte a = 0xFF, r, g, b;
            if (s.Length == 8)
            {
                a = Convert.ToByte(s.Substring(0, 2), 16);
                r = Convert.ToByte(s.Substring(2, 2), 16);
                g = Convert.ToByte(s.Substring(4, 2), 16);
                b = Convert.ToByte(s.Substring(6, 2), 16);
            }
            else
            {
                r = Convert.ToByte(s.Substring(0, 2), 16);
                g = Convert.ToByte(s.Substring(2, 2), 16);
                b = Convert.ToByte(s.Substring(4, 2), 16);
            }
            return Color.FromArgb(a, r, g, b);
        }

        public static Color Lerp(Color from, Color to, double t)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return Color.FromArgb(
                (byte)Math.Round(from.A + (to.A - from.A) * t),
                (byte)Math.Round(from.R + (to.R - from.R) * t),
                (byte)Math.Round(from.G + (to.G - from.G) * t),
                (byte)Math.Round(from.B + (to.B - from.B) * t));
        }

        // Relative luminance -> pick near-black or white text for AA contrast.
        public static Color IdealTextOn(Color c)
        {
            double L = (0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B));
            return L > 0.40 ? Hex("#0A2230") : Colors.White;
        }

        private static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static readonly Dictionary<string, string> Dark = new Dictionary<string, string>
        {
            { "canvas", "#202020" }, { "surface", "#2B2B2B" }, { "surfaceAlt", "#323232" }, { "surfaceInput", "#272727" },
            { "textPrimary", "#FFFFFF" }, { "textSecondary", "#E0E0E0" }, { "textMuted", "#C5C5C5" }, { "textDisabled", "#7A7A7A" },
            { "success", "#6CCB5F" }, { "successDim", "#3C5A36" }, { "danger", "#FF99A4" }, { "dangerText", "#3A0E12" },
            { "warning", "#FCE100" }, { "border", "#3D3D3D" }, { "borderStrong", "#4A4A4A" }, { "dividerHairline", "#363636" },
            { "cardTopHighlight", "#3A3A3A" },
            { "logBg", "#1B1B1B" }, { "logText", "#D4D4D4" }, { "logDim", "#6E7681" }, { "logKey", "#9CA3AF" },
            { "logValue", "#E6EDF3" }, { "logSuccess", "#6CCB5F" }, { "logError", "#FF99A4" },
            { "scrollThumb", "#4A4A4A" }, { "statusDotIdle", "#8A8A8A" },
        };

        private static readonly Dictionary<string, string> Light = new Dictionary<string, string>
        {
            { "canvas", "#F3F3F3" }, { "surface", "#FBFBFB" }, { "surfaceAlt", "#F0F0F0" }, { "surfaceInput", "#FFFFFF" },
            { "textPrimary", "#1A1A1A" }, { "textSecondary", "#383838" }, { "textMuted", "#5E5E5E" }, { "textDisabled", "#A0A0A0" },
            { "success", "#0F7B0F" }, { "successDim", "#DFF1DC" }, { "danger", "#C42B1C" }, { "dangerText", "#FFFFFF" },
            { "warning", "#9D5D00" }, { "border", "#E5E5E5" }, { "borderStrong", "#D6D6D6" }, { "dividerHairline", "#ECECEC" },
            { "cardTopHighlight", "#FFFFFF" },
            { "logBg", "#1B1B1B" }, { "logText", "#D4D4D4" }, { "logDim", "#6E7681" }, { "logKey", "#9CA3AF" },
            { "logValue", "#E6EDF3" }, { "logSuccess", "#6CCB5F" }, { "logError", "#FF99A4" },
            { "scrollThumb", "#C8C8C8" }, { "statusDotIdle", "#9A9A9A" },
        };
    }

    // Centralized font families (verified resolvable on Win11). The "Microsoft YaHei UI"
    // fallback gives crisp Simplified-Chinese glyphs for the localized UI.
    internal static class Fonts
    {
        public static readonly FontFamily Display = new FontFamily("Segoe UI Variable Display, Segoe UI Semibold, Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily Text = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily Small = new FontFamily("Segoe UI Variable Small, Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        public static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI, Cascadia Code");
    }

    // Maps the backend script's English step headers to Simplified Chinese for display.
    // Falls back to the original text for anything unrecognized.
    internal static class Lang
    {
        private static readonly Dictionary<string, string> Steps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Resolve fresh Microsoft Store URL", "解析 Microsoft Store 最新地址" },
            { "Download MSIX", "下载 MSIX 安装包" },
            { "Verify package hash", "校验安装包哈希" },
            { "Extract MSIX", "解压 MSIX" },
            { "Install app files", "安装应用文件" },
        };

        public static string Step(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            string zh;
            return Steps.TryGetValue(title.Trim(), out zh) ? zh : title;
        }
    }

    // Segoe Fluent Icons / Segoe MDL2 glyph code points, written as \u escapes so the
    // source stays plain ASCII (no Private-Use-Area chars to get mangled by tooling).
    internal static class Glyph
    {
        public const string App        = ""; // CommandPrompt (mirrors the ">_" app icon)
        public const string Folder     = ""; // Folder
        public const string Download   = ""; // Download
        public const string Cancel     = ""; // Cancel (X)
        public const string Search     = ""; // Search
        public const string Pencil     = ""; // Edit
        public const string OpenFolder = ""; // FolderOpen
        public const string Chevron    = ""; // ChevronDown
        public const string CheckMark  = ""; // CheckMark
        public const string Error      = ""; // ErrorBadge
        public const string Copy       = ""; // Copy
        public const string Minimize   = ""; // ChromeMinimize
        public const string Close      = ""; // ChromeClose
        public const string Terminal   = ""; // CommandPrompt
    }
}
