using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexAppInstaller
{
    // Pure-code WPF animation helpers (no XAML). Every entry point honors the global
    // Enabled flag: when false (reduced-motion or off-screen render) it applies the
    // END STATE immediately and skips the storyboard. Never throws.
    internal static class Motion
    {
        public static bool Enabled = true;

        public static EasingFunctionBase EaseOut()
        {
            return new CubicEase { EasingMode = EasingMode.EaseOut };
        }

        // Fade-in + slide-up entrance.
        public static void FadeInSlideUp(FrameworkElement el, double fromY = 18, int ms = 260, int delayMs = 0)
        {
            if (el == null) return;
            TranslateTransform tt = EnsureTranslate(el);
            if (!Enabled) { el.Opacity = 1; tt.Y = 0; return; }

            el.Opacity = 0; tt.Y = fromY;
            Duration d = new Duration(TimeSpan.FromMilliseconds(ms));
            TimeSpan begin = TimeSpan.FromMilliseconds(delayMs < 0 ? 0 : delayMs);
            EasingFunctionBase e = EaseOut();

            var fade = new DoubleAnimation { From = 0, To = 1, Duration = d, BeginTime = begin, EasingFunction = e };
            var slide = new DoubleAnimation { From = fromY, To = 0, Duration = d, BeginTime = begin, EasingFunction = e };
            fade.Completed += delegate { el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 1; };
            slide.Completed += delegate { tt.BeginAnimation(TranslateTransform.YProperty, null); tt.Y = 0; };
            el.BeginAnimation(UIElement.OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        // Smoothly glide a determinate ProgressBar to a target value.
        public static void ProgressTo(ProgressBar bar, double target, int ms = 240)
        {
            if (bar == null) return;
            if (target < bar.Minimum) target = bar.Minimum;
            if (target > bar.Maximum) target = bar.Maximum;
            if (!Enabled) { bar.BeginAnimation(ProgressBar.ValueProperty, null); bar.Value = target; return; }

            var a = new DoubleAnimation
            {
                From = bar.Value,
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            a.Completed += delegate { bar.BeginAnimation(ProgressBar.ValueProperty, null); bar.Value = target; };
            bar.BeginAnimation(ProgressBar.ValueProperty, a);
        }

        // Continuous shimmer sweep on a Border's gradient background. Returns the
        // Storyboard so the caller can Stop() it (null when disabled).
        public static Storyboard StartShimmer(Border border, Color baseC, Color hiC, int periodMs = 1300)
        {
            if (border == null) return null;
            if (!Enabled) { border.Background = new SolidColorBrush(baseC); return null; }

            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            var s0 = new GradientStop(baseC, -0.4);
            var s1 = new GradientStop(hiC, -0.2);
            var s2 = new GradientStop(baseC, 0.0);
            brush.GradientStops.Add(s0);
            brush.GradientStops.Add(s1);
            brush.GradientStops.Add(s2);
            border.Background = brush;

            Duration d = new Duration(TimeSpan.FromMilliseconds(periodMs));
            var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            AddOffsetSweep(sb, s0, -0.4, 1.0, d);
            AddOffsetSweep(sb, s1, -0.2, 1.2, d);
            AddOffsetSweep(sb, s2, 0.0, 1.4, d);
            sb.Begin(border, true);
            return sb;
        }

        public static void StopShimmer(Storyboard sb, Border border)
        {
            if (sb != null && border != null) { try { sb.Stop(border); } catch { } }
        }

        // Animate a control-owned (unfrozen) SolidColorBrush's color.
        public static void ColorTo(SolidColorBrush brush, Color to, int ms = 140)
        {
            if (brush == null) return;
            if (!Enabled || brush.IsFrozen) { if (!brush.IsFrozen) brush.Color = to; return; }
            var a = new ColorAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, a);
        }

        // One-shot color flash that returns to a base color.
        public static void Flash(SolidColorBrush brush, Color to, Color back, int upMs = 160, int downMs = 700)
        {
            if (brush == null || brush.IsFrozen) return;
            if (!Enabled) return;
            var up = new ColorAnimation { To = to, Duration = new Duration(TimeSpan.FromMilliseconds(upMs)) };
            up.Completed += delegate
            {
                var down = new ColorAnimation { To = back, Duration = new Duration(TimeSpan.FromMilliseconds(downMs)) };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, down);
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, up);
        }

        // Rotate (e.g. a disclosure chevron) to an angle.
        public static void RotateTo(FrameworkElement el, double angle, int ms = 200)
        {
            if (el == null) return;
            RotateTransform rt = EnsureRotate(el);
            if (!Enabled) { rt.Angle = angle; return; }
            var a = new DoubleAnimation
            {
                To = angle,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, a);
        }

        // Animate Window.Height (used by the Details disclosure).
        public static void HeightTo(Window w, double target, int ms = 220, Action onDone = null)
        {
            if (w == null) return;
            if (!Enabled)
            {
                w.BeginAnimation(FrameworkElement.HeightProperty, null);
                w.Height = target;
                if (onDone != null) onDone();
                return;
            }
            var a = new DoubleAnimation
            {
                From = w.ActualHeight,
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            };
            a.Completed += delegate
            {
                try
                {
                    if (w.IsLoaded)
                    {
                        w.BeginAnimation(FrameworkElement.HeightProperty, null);
                        w.Height = target;
                    }
                    if (onDone != null) onDone();
                }
                catch { /* window closed mid-animation */ }
            };
            w.BeginAnimation(FrameworkElement.HeightProperty, a);
        }

        // Translate an element on X (toggle knob) to a target.
        public static void TranslateXTo(FrameworkElement el, double x, int ms = 160)
        {
            if (el == null) return;
            TranslateTransform tt = EnsureTranslate(el);
            if (!Enabled) { tt.X = x; return; }
            var a = new DoubleAnimation
            {
                To = x,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            tt.BeginAnimation(TranslateTransform.XProperty, a);
        }

        // Gentle repeating opacity pulse (status dot). Returns Storyboard to Stop().
        public static Storyboard Pulse(UIElement el, int periodMs = 1400)
        {
            if (el == null || !Enabled) return null;
            var a = new DoubleAnimation
            {
                From = 1.0,
                To = 0.35,
                Duration = new Duration(TimeSpan.FromMilliseconds(periodMs)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var sb = new Storyboard();
            sb.Children.Add(a);
            Storyboard.SetTarget(a, el);
            Storyboard.SetTargetProperty(a, new PropertyPath(UIElement.OpacityProperty));
            sb.Begin();
            return sb;
        }

        public static void StopPulse(Storyboard sb, UIElement el)
        {
            if (sb != null) { try { sb.Stop(); } catch { } }
            if (el != null) el.Opacity = 1.0;
        }

        // Small scale "pop" (e.g. success glyph appearing).
        public static void Pop(FrameworkElement el, int ms = 320)
        {
            if (el == null || !Enabled) return;
            ScaleTransform st = EnsureScale(el);
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
            var ax = new DoubleAnimation { From = 0.8, To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = ease };
            var ay = ax.Clone();
            st.BeginAnimation(ScaleTransform.ScaleXProperty, ax);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, ay);
        }

        // ---- transform plumbing -------------------------------------------------

        private static TranslateTransform EnsureTranslate(FrameworkElement el)
        {
            return EnsureInGroup<TranslateTransform>(el);
        }
        private static RotateTransform EnsureRotate(FrameworkElement el)
        {
            var rt = EnsureInGroup<RotateTransform>(el);
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            return rt;
        }
        private static ScaleTransform EnsureScale(FrameworkElement el)
        {
            var st = EnsureInGroup<ScaleTransform>(el);
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            return st;
        }

        private static T EnsureInGroup<T>(FrameworkElement el) where T : Transform, new()
        {
            var direct = el.RenderTransform as T;
            if (direct != null) return direct;

            TransformGroup group = el.RenderTransform as TransformGroup;
            if (group == null)
            {
                group = new TransformGroup();
                Transform cur = el.RenderTransform;
                if (cur != null && cur != Transform.Identity) group.Children.Add(cur);
                el.RenderTransform = group;
            }
            foreach (Transform t in group.Children)
            {
                T match = t as T;
                if (match != null) return match;
            }
            T added = new T();
            group.Children.Add(added);
            return added;
        }

        private static void AddOffsetSweep(Storyboard sb, GradientStop stop, double from, double to, Duration d)
        {
            var a = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = d,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(a, stop);
            Storyboard.SetTargetProperty(a, new PropertyPath(GradientStop.OffsetProperty));
            sb.Children.Add(a);
        }
    }
}
