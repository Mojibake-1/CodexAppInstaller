using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CodexAppInstaller
{
    // Win11-style pill toggle switch, drawn in code, with a sliding knob.
    internal sealed class Switch : ToggleButton
    {
        private readonly UiContext _ctx;
        private readonly Border _track;
        private readonly Border _knob;
        private readonly SolidColorBrush _trackBg = new SolidColorBrush(Colors.Gray);
        private readonly SolidColorBrush _knobBg = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush _trackBorder = new SolidColorBrush(Colors.Transparent);
        private readonly TranslateTransform _knobT = new TranslateTransform();
        private const double Travel = 20;

        public Switch(UiContext ctx)
        {
            _ctx = ctx;
            Width = 40; Height = 20;
            Cursor = Cursors.Hand;
            Focusable = true;
            VerticalAlignment = VerticalAlignment.Center;

            _track = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = _trackBg,
                BorderBrush = _trackBorder,
                BorderThickness = new Thickness(1)
            };
            _knob = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = _knobBg,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                RenderTransform = _knobT
            };
            var grid = new Grid();
            grid.Children.Add(_track);
            grid.Children.Add(_knob);
            Content = grid;
            Template = BareTemplate();

            Checked += delegate { UpdateVisual(true); };
            Unchecked += delegate { UpdateVisual(true); };
            _ctx.Themed(Restyle);
            UpdateVisual(false);
        }

        private void Restyle()
        {
            bool on = IsChecked == true;
            _trackBg.Color = on ? _ctx.P.C("accent") : _ctx.P.C("borderStrong");
            _trackBorder.Color = on ? _ctx.P.C("accent") : _ctx.P.C("border");
            _knobBg.Color = on ? _ctx.P.C("accentText") : _ctx.P.C("textSecondary");
        }

        private void UpdateVisual(bool animate)
        {
            bool on = IsChecked == true;
            double x = on ? Travel : 0;
            if (animate) Motion.TranslateXTo(_knob, x, 150);
            else _knobT.X = x;
            Motion.ColorTo(_trackBg, on ? _ctx.P.C("accent") : _ctx.P.C("borderStrong"), animate ? 150 : 0);
            Motion.ColorTo(_trackBorder, on ? _ctx.P.C("accent") : _ctx.P.C("border"), animate ? 150 : 0);
            _knobBg.Color = on ? _ctx.P.C("accentText") : _ctx.P.C("textSecondary");
        }

        private static ControlTemplate BareTemplate()
        {
            var t = new ControlTemplate(typeof(ToggleButton));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            t.VisualTree = cp;
            return t;
        }
    }

    // A row of small pips mapping to the backend's "[n/total]" step headers.
    internal sealed class StepPips : StackPanel
    {
        private readonly UiContext _ctx;
        private readonly List<Border> _pips = new List<Border>();
        private int _total = 5;
        private int _current = 0;

        public StepPips(UiContext ctx)
        {
            _ctx = ctx;
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Build(_total);
            _ctx.Themed(Restyle);
        }

        private void Build(int total)
        {
            Children.Clear();
            _pips.Clear();
            for (int i = 0; i < total; i++)
            {
                var b = new Border
                {
                    Width = 12,
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0),
                    SnapsToDevicePixels = true
                };
                _pips.Add(b);
                Children.Add(b);
            }
        }

        public void SetState(int total, int current)
        {
            if (total < 1) total = 1;
            if (total != _total) { _total = total; Build(total); }
            _current = current;
            Restyle();
        }

        public void Reset() { SetState(_total, 0); }

        private void Restyle()
        {
            for (int i = 0; i < _pips.Count; i++)
                _pips[i].Background = (i < _current) ? _ctx.P.B("accent") : _ctx.P.B("surfaceAlt");
        }
    }

    // A read-only, syntax-tinted console. Stays dark in both themes by design.
    internal sealed class ConsoleLog : Border
    {
        private readonly UiContext _ctx;
        private readonly RichTextBox _rtb;
        private readonly FlowDocument _doc;
        public bool AutoScroll = true;

        public ConsoleLog(UiContext ctx)
        {
            _ctx = ctx;
            CornerRadius = new CornerRadius(8);
            SnapsToDevicePixels = true;

            _doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = Fonts.Mono,
                FontSize = 12.5,
                LineHeight = 18
            };
            _rtb = new RichTextBox
            {
                Document = _doc,
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(12, 10, 8, 10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _rtb.IsInactiveSelectionHighlightEnabled = false;
            Child = _rtb;

            _ctx.Themed(delegate
            {
                Background = _ctx.P.B("logBg");
                _rtb.Foreground = _ctx.P.B("logText");
            });
        }

        private Paragraph NewPara()
        {
            return new Paragraph { Margin = new Thickness(0, 0, 0, 0) };
        }

        private Run R(string text, string role, bool bold = false)
        {
            var r = new Run(text) { Foreground = _ctx.P.B(role) };
            if (bold) r.FontWeight = FontWeights.SemiBold;
            return r;
        }

        public void Step(string text)
        {
            var p = NewPara();
            p.Margin = new Thickness(0, 6, 0, 2);
            p.Inlines.Add(R("▸ ", "logAccent", true)); // ▸
            p.Inlines.Add(R(text, "logAccent", true));
            Add(p);
        }

        public void KeyValue(string key, string value)
        {
            var p = NewPara();
            p.Inlines.Add(R(key + "  ", "logKey"));
            p.Inlines.Add(R(value, "logValue"));
            Add(p);
        }

        public void Ok(string text)
        {
            var p = NewPara();
            var glyph = new Run(Glyph.CheckMark + " ") { Foreground = _ctx.P.B("logSuccess"), FontFamily = Fonts.Icons };
            p.Inlines.Add(glyph);
            p.Inlines.Add(R(text, "logSuccess"));
            Add(p);
        }

        public void Err(string text)
        {
            var p = NewPara();
            p.Inlines.Add(R(text, "logError"));
            Add(p);
        }

        public void Dim(string text) { Add2(R(text, "logDim")); }
        public void Line(string text) { Add2(R(text, "logText")); }

        private void Add2(Run run)
        {
            var p = NewPara();
            p.Inlines.Add(run);
            Add(p);
        }

        private void Add(Paragraph p)
        {
            _doc.Blocks.Add(p);
            if (AutoScroll) _rtb.ScrollToEnd();
        }

        public void Clear() { _doc.Blocks.Clear(); }

        public string AllText()
        {
            return new TextRange(_doc.ContentStart, _doc.ContentEnd).Text;
        }
    }
}
