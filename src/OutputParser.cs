using System.Text.RegularExpressions;

namespace CodexAppInstaller
{
    internal enum LineKind { Normal, Rule, Step, ProgressBar, SubStep, Info, Ok }

    // Interprets the backend's English console output into structured UI updates.
    // The patterns mirror exactly what Install-CodexApp.ps1 prints.
    internal static class OutputParser
    {
        private static readonly Regex StepRe =
            new Regex(@"^\s*\[(?<i>\d+)\s*/\s*(?<n>\d+)\]\s+(?<t>.+?)\s*$", RegexOptions.Compiled);

        // "   [#########-------]   42.3%"
        private static readonly Regex BarRe =
            new Regex(@"\[[#\-\s]*\]\s*(?<p>\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);

        // bare percent fallback ("  42.3%")
        private static readonly Regex PctRe =
            new Regex(@"(?<p>\d+(?:\.\d+)?)\s*%\s*$", RegexOptions.Compiled);

        private static readonly Regex FileRe = new Regex(@"^\s*File:\s+(?<v>\S.*?)\s*$", RegexOptions.Compiled);
        private static readonly Regex SizeRe = new Regex(@"^\s*Size:\s+(?<v>\S.*?)\s*$", RegexOptions.Compiled);
        private static readonly Regex UrlRe = new Regex(@"^\s*URL:\s+(?<v>\S.*?)\s*$", RegexOptions.Compiled);
        private static readonly Regex ErrRe = new Regex(@"^\s*Error:\s+(?<v>\S.*?)\s*$", RegexOptions.Compiled);
        private static readonly Regex RuleRe = new Regex(@"^\s*={6,}\s*$", RegexOptions.Compiled);
        // version inside the package file name
        private static readonly Regex VerRe =
            new Regex(@"OpenAI\.Codex_(?<v>[0-9][0-9.]*?)_x64", RegexOptions.Compiled);

        public static bool TryStep(string line, out int index, out int total, out string text)
        {
            index = total = 0; text = null;
            var m = StepRe.Match(line);
            if (!m.Success) return false;
            index = int.Parse(m.Groups["i"].Value);
            total = int.Parse(m.Groups["n"].Value);
            text = m.Groups["t"].Value;
            return true;
        }

        public static bool TryPercent(string line, out double percent)
        {
            percent = 0;
            var m = BarRe.Match(line);
            if (!m.Success) m = PctRe.Match(line);
            if (!m.Success) return false;
            return double.TryParse(m.Groups["p"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out percent);
        }

        public static bool IsProgressBar(string line) { return BarRe.IsMatch(line); }
        public static bool IsRule(string line) { return RuleRe.IsMatch(line); }

        public static bool TryFile(string line, out string value) { return Cap(FileRe, line, out value); }
        public static bool TrySize(string line, out string value) { return Cap(SizeRe, line, out value); }
        public static bool TryUrl(string line, out string value) { return Cap(UrlRe, line, out value); }
        public static bool TryError(string line, out string value) { return Cap(ErrRe, line, out value); }

        public static bool TryVersion(string text, out string version) { return Cap(VerRe, text, out version, "v"); }

        public static bool IsHashVerified(string line)
        {
            return line.IndexOf("verified", System.StringComparison.OrdinalIgnoreCase) >= 0
                && (line.IndexOf("SHA", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public static bool IsSuccess(string line)
        {
            return line.IndexOf("Installed successfully", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public static bool IsResolved(string line)
        {
            return line.IndexOf("Resolved package", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public static bool IsFailure(string line)
        {
            return line.IndexOf("Install failed", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Cap(Regex re, string line, out string value, string group = "v")
        {
            value = null;
            var m = re.Match(line);
            if (!m.Success) return false;
            value = m.Groups[group].Value;
            return true;
        }
    }
}
