using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexAppInstaller
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Off-screen render for design QA: --render <out.png> [dark|light] [WxH]
            if (args.Length >= 2 && args[0].Equals("--render", StringComparison.OrdinalIgnoreCase))
            {
                return Render(args);
            }

            // Headless construction check (no window shown).
            if (args.Length > 0 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Motion.Enabled = false;
                new Application();
                var w = new MainWindow(true, null);
                w.RootForRender.Measure(new Size(720, 560));
                w.RootForRender.Arrange(new Rect(0, 0, 720, 560));
                w.RootForRender.UpdateLayout();
                return w.HasBackend ? 0 : 2;
            }

            var app = new Application();
            app.Run(new MainWindow());
            return 0;
        }

        private static int Render(string[] args)
        {
            try
            {
                Motion.Enabled = false;
                new Application();

                string outPath = args[1];
                bool dark = true;
                bool demo = false;
                int w = 720, h = 560;
                bool explicitSize = false;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i].Equals("light", StringComparison.OrdinalIgnoreCase)) dark = false;
                    else if (args[i].Equals("dark", StringComparison.OrdinalIgnoreCase)) dark = true;
                    else if (args[i].Equals("demo", StringComparison.OrdinalIgnoreCase)) demo = true;
                    else if (args[i].IndexOf('x') > 0)
                    {
                        var parts = args[i].ToLowerInvariant().Split('x');
                        int pw, ph;
                        if (parts.Length == 2 && int.TryParse(parts[0], out pw) && int.TryParse(parts[1], out ph)) { w = pw; h = ph; explicitSize = true; }
                    }
                }
                if (demo && !explicitSize) h = 748;

                var win = new MainWindow(true, dark);
                if (demo) win.SimulateBusyForRender();
                var host = new Border
                {
                    Width = w,
                    Height = h,
                    Background = win.CanvasBrush,
                    Child = win.RootForRender
                };
                host.Measure(new Size(w, h));
                host.Arrange(new Rect(0, 0, w, h));
                host.UpdateLayout();

                double scale = 1.5;
                var rtb = new RenderTargetBitmap(
                    (int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                rtb.Render(host);

                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Open(outPath, FileMode.Create, FileAccess.Write))
                {
                    enc.Save(fs);
                }
                Console.WriteLine("Rendered " + outPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Render failed: " + ex);
                return 1;
            }
        }
    }
}
