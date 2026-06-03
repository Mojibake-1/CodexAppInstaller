using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CodexAppInstaller
{
    // Shared context: the active palette + a registry of restyle callbacks so the
    // whole tree can be re-themed on a live dark/light flip without rebuilding it.
    internal sealed class UiContext
    {
        public Palette P;
        private readonly List<Action> _restyle = new List<Action>();

        public UiContext(Palette p) { P = p; }

        // Register a styling action; runs once now and again on every Reapply().
        public void Themed(Action a) { _restyle.Add(a); a(); }

        public void Reapply(Palette p) { P = p; foreach (var a in _restyle) a(); }
    }

    internal enum BtnStyle { Primary, Secondary, Ghost, Danger }

    // A button plus the handles needed to re-theme it and to morph it in place.
    internal sealed class ButtonParts
    {
        public Button Button;
        public SolidColorBrush Bg;        // unfrozen, animatable
        public SolidColorBrush BorderBr;  // unfrozen, animatable
        public SolidColorBrush Fg;        // unfrozen
        public TextBlock GlyphTb;
        public TextBlock LabelTb;
        public Color Normal, Hover, Pressed;

        public void SetColors(Color n, Color h, Color p)
        {
            Normal = n; Hover = h; Pressed = p;
            if (Button.IsMouseOver) Motion.ColorTo(Bg, h, 0); else Bg.Color = n;
        }
    }

    internal static class Ui
    {
        // ---- text ---------------------------------------------------------------

        public static TextBlock Tb(UiContext ctx, string text, FontFamily font, double size,
            string role, FontWeight? weight = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = font,
                FontSize = size
            };
            TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Ideal);
            if (weight.HasValue) tb.FontWeight = weight.Value;
            ctx.Themed(delegate { tb.Foreground = ctx.P.B(role); });
            return tb;
        }

        public static TextBlock Icon(UiContext ctx, string glyph, double size, string role)
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontFamily = Fonts.Icons,
                FontSize = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ctx.Themed(delegate { tb.Foreground = ctx.P.B(role); });
            return tb;
        }

        // ---- containers ---------------------------------------------------------

        public static Border Card(UiContext ctx)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            DropShadowEffect shadow = null;
            if (!Perf.Lite)
            {
                // Blur effects are very expensive under software rendering, so only soft-shadow
                // the card on capable machines. Lite mode leans on a stronger border instead.
                shadow = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Direction = 270, Color = Colors.Black };
                card.Effect = shadow;
            }
            ctx.Themed(delegate
            {
                card.Background = ctx.P.B("surface");
                card.BorderBrush = ctx.P.B(Perf.Lite ? "borderStrong" : "border");
                if (shadow != null) shadow.Opacity = ctx.P.IsDark ? 0.36 : 0.14;
            });
            return card;
        }

        public static Border Divider(UiContext ctx, double top = 12, double bottom = 12)
        {
            var d = new Border { Height = 1, Margin = new Thickness(0, top, 0, bottom), SnapsToDevicePixels = true };
            ctx.Themed(delegate { d.Background = ctx.P.B("dividerHairline"); });
            return d;
        }

        // ---- buttons ------------------------------------------------------------

        public static ButtonParts Button(UiContext ctx, BtnStyle style, string glyph, string label,
            double minWidth = 0, double height = 36)
        {
            var parts = new ButtonParts();
            parts.Bg = new SolidColorBrush(Colors.Transparent);
            parts.BorderBr = new SolidColorBrush(Colors.Transparent);
            parts.Fg = new SolidColorBrush(Colors.White);

            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            if (!string.IsNullOrEmpty(glyph))
            {
                parts.GlyphTb = new TextBlock
                {
                    Text = glyph,
                    FontFamily = Fonts.Icons,
                    FontSize = 15,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, string.IsNullOrEmpty(label) ? 0 : 8, 0),
                    Foreground = parts.Fg
                };
                content.Children.Add(parts.GlyphTb);
            }
            if (!string.IsNullOrEmpty(label))
            {
                parts.LabelTb = new TextBlock
                {
                    Text = label,
                    FontFamily = Fonts.Text,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = parts.Fg
                };
                content.Children.Add(parts.LabelTb);
            }

            var btn = new Button
            {
                Content = content,
                Height = height,
                MinWidth = minWidth,
                Padding = new Thickness(14, 0, 14, 0),
                Background = parts.Bg,
                BorderBrush = parts.BorderBr,
                BorderThickness = new Thickness(style == BtnStyle.Ghost ? 0 : (style == BtnStyle.Danger ? 1 : 0)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true,
                SnapsToDevicePixels = true,
                Template = RoundedButtonTemplate(6)
            };
            parts.Button = btn;

            // Hover / press color transitions on the per-instance (unfrozen) brush.
            btn.MouseEnter += delegate { Motion.ColorTo(parts.Bg, parts.Hover, 120); };
            btn.MouseLeave += delegate { Motion.ColorTo(parts.Bg, parts.Normal, 120); };
            btn.PreviewMouseLeftButtonDown += delegate { Motion.ColorTo(parts.Bg, parts.Pressed, 60); };
            btn.PreviewMouseLeftButtonUp += delegate { Motion.ColorTo(parts.Bg, btn.IsMouseOver ? parts.Hover : parts.Normal, 100); };
            btn.IsEnabledChanged += delegate { btn.Opacity = btn.IsEnabled ? 1.0 : 0.55; };

            // Theme-reactive recolor based on the style.
            ctx.Themed(delegate { Recolor(ctx, parts, style); });
            return parts;
        }

        public static void Recolor(UiContext ctx, ButtonParts parts, BtnStyle style)
        {
            Palette p = ctx.P;
            switch (style)
            {
                case BtnStyle.Primary:
                    parts.SetColors(p.C("accent"), p.C("accentHover"), p.C("accentPressed"));
                    parts.Fg.Color = p.C("accentText");
                    parts.BorderBr.Color = Colors.Transparent;
                    break;
                case BtnStyle.Secondary:
                    parts.SetColors(p.C("surfaceAlt"), Palette.Lerp(p.C("surfaceAlt"), p.C("textPrimary"), 0.10), Palette.Lerp(p.C("surfaceAlt"), p.C("textPrimary"), 0.04));
                    parts.Fg.Color = p.C("textPrimary");
                    parts.BorderBr.Color = Colors.Transparent;
                    break;
                case BtnStyle.Ghost:
                    parts.SetColors(Colors.Transparent, WithAlpha(p.C("textPrimary"), 0.08), WithAlpha(p.C("textPrimary"), 0.14));
                    parts.Fg.Color = p.C("accent");
                    parts.BorderBr.Color = Colors.Transparent;
                    break;
                case BtnStyle.Danger:
                    parts.SetColors(Colors.Transparent, WithAlpha(p.C("danger"), 0.16), WithAlpha(p.C("danger"), 0.26));
                    parts.Fg.Color = p.C("danger");
                    parts.BorderBr.Color = p.C("danger");
                    break;
            }
        }

        public static ControlTemplate RoundedButtonTemplate(double radius)
        {
            var t = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            t.VisualTree = border;
            return t;
        }

        // Flat caption button (minimize/close) with template triggers.
        public static Button CaptionButton(UiContext ctx, string glyph, bool isClose, Action onClick)
        {
            var glyphTb = new TextBlock
            {
                Text = glyph,
                FontFamily = Fonts.Icons,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var bg = new SolidColorBrush(Colors.Transparent);
            var btn = new Button
            {
                Width = 46,
                Height = 32,
                Content = glyphTb,
                Background = bg,
                BorderThickness = new Thickness(0),
                Focusable = false,
                Template = RoundedButtonTemplate(0)
            };
            WindowChromeShim.SetHitTestVisible(btn);
            if (onClick != null) btn.Click += delegate { onClick(); };

            ctx.Themed(delegate { glyphTb.Foreground = ctx.P.B("textSecondary"); });
            Color hoverC = isClose ? Palette.Hex("#C42B1C") : default(Color);
            btn.MouseEnter += delegate
            {
                if (isClose) { Motion.ColorTo(bg, Palette.Hex("#C42B1C"), 90); glyphTb.Foreground = System.Windows.Media.Brushes.White; }
                else Motion.ColorTo(bg, WithAlpha(ctx.P.C("textPrimary"), 0.10), 90);
            };
            btn.MouseLeave += delegate
            {
                Motion.ColorTo(bg, Colors.Transparent, 120);
                glyphTb.Foreground = ctx.P.B("textSecondary");
            };
            return btn;
        }

        // ---- progress bar (rounded, themable) -----------------------------------

        public static ProgressBar ProgressBar(UiContext ctx, double height = 4)
        {
            var bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = height,
                SnapsToDevicePixels = true
            };
            var track = new SolidColorBrush();
            var fill = new SolidColorBrush();
            bar.Tag = new Brush[] { track, fill };
            ctx.Themed(delegate
            {
                track.Color = ctx.P.C("dividerHairline");
                fill.Color = ctx.P.C("accent");
            });
            bar.Template = ProgressTemplate(track, fill, height);
            return bar;
        }

        private static ControlTemplate ProgressTemplate(Brush track, Brush fill, double height)
        {
            var t = new ControlTemplate(typeof(ProgressBar));
            var root = new FrameworkElementFactory(typeof(Border));
            root.SetValue(Border.CornerRadiusProperty, new CornerRadius(height / 2));
            root.SetValue(Border.BackgroundProperty, track);
            root.SetValue(Border.ClipToBoundsProperty, true);

            var grid = new FrameworkElementFactory(typeof(Grid));

            var ptrack = new FrameworkElementFactory(typeof(Border));
            ptrack.Name = "PART_Track";
            ptrack.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);

            var indicator = new FrameworkElementFactory(typeof(Border));
            indicator.Name = "PART_Indicator";
            indicator.SetValue(Border.BackgroundProperty, fill);
            indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(height / 2));
            indicator.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);

            grid.AppendChild(ptrack);
            grid.AppendChild(indicator);
            root.AppendChild(grid);
            t.VisualTree = root;
            return t;
        }

        // ---- misc ---------------------------------------------------------------

        public static Color WithAlpha(Color c, double a)
        {
            return Color.FromArgb((byte)Math.Round(255 * a), c.R, c.G, c.B);
        }
    }

    // WindowChrome hit-test attached-property shim (kept tiny + isolated).
    internal static class WindowChromeShim
    {
        public static void SetHitTestVisible(UIElement el)
        {
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(el, true);
        }
    }
}
