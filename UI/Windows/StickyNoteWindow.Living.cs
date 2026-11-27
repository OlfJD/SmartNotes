using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmartNotes.Core.Models;
using SmartNotes.UI.Skins;
using Path = System.Windows.Shapes.Path;
using Image = System.Windows.Controls.Image;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SmartNotes.UI.Windows;

/// <summary>
/// Living themes (Settings > Note Appearance, off by default): small animations that run only while the note is
/// focused or under the mouse, so an idle desktop costs nothing.
///   Astral: now and then a shooting star crosses the sky.
///   Elden Ring: the Great Rune slowly breathes.
///   Deep Sea: the creature's light breathes.
///   Cyberpunk: a short glitch when an edit is saved.
///   Medieval: a wax seal is pressed onto the scroll when the note is locked (and stays while it is locked).
/// </summary>
public partial class StickyNoteWindow
{
    private bool _hovered;
    private DispatcherTimer? _shootingStarTimer;
    private Ellipse? _breathingGlow;
    private Image? _waxSeal;
    private readonly Random _livingRandom = new();

    private bool LivingOn => _settingsService.Settings.LivingThemes;
    private bool LivingActive => LivingOn && (IsActive || _hovered) && IsVisible;

    /// <summary>Starts or stops the ambient animations; call when focus, hover, skin, size or the setting changes.</summary>
    public void UpdateLiving()
    {
        if (LivingLayer == null) return;
        bool active = LivingActive;
        string skin = _currentSkin.Key;

        // shooting stars (Astral)
        if (active && skin == NoteSkin.AstralKey)
        {
            if (_shootingStarTimer == null)
            {
                _shootingStarTimer = new DispatcherTimer();
                _shootingStarTimer.Tick += (s, e) =>
                {
                    _shootingStarTimer.Interval = TimeSpan.FromSeconds(3 + _livingRandom.NextDouble() * 5);
                    if (LivingActive && _currentSkin.Key == NoteSkin.AstralKey) LaunchShootingStar();
                    else _shootingStarTimer.Stop();
                };
            }
            if (!_shootingStarTimer.IsEnabled)
            {
                _shootingStarTimer.Interval = TimeSpan.FromSeconds(0.8);
                _shootingStarTimer.Start();
            }
        }
        else
        {
            _shootingStarTimer?.Stop();
        }

        // breathing glow over the rune or the creature
        if (active && BreathingGlowPlacement() is { } glow)
        {
            ShowBreathingGlow(glow.Centre, glow.Radius);
        }
        else
        {
            HideBreathingGlow();
        }

        UpdateWaxSeal(animate: false);
    }

    private void Window_MouseEnter_Living()
    {
        _hovered = true;
        UpdateLiving();
    }

    private void Window_MouseLeave_Living()
    {
        _hovered = false;
        UpdateLiving();
    }

    // ---------------------------------------------------------------- Astral: shooting star

    private void LaunchShootingStar()
    {
        double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
        if (w < 200 || h < 160) return;
        double top = _currentSkin.ContentInset.Top + 50, bottom = h - 60;
        if (bottom - top < 30) return;

        double length = 50 + _livingRandom.NextDouble() * 40;
        double angle = (18 + _livingRandom.NextDouble() * 22) * Math.PI / 180;   // falling to the left
        var dir = new Vector(-Math.Cos(angle), Math.Sin(angle));
        var start = new Point(w * (0.55 + _livingRandom.NextDouble() * 0.4), top + _livingRandom.NextDouble() * (bottom - top) * 0.5);
        double travel = 90 + _livingRandom.NextDouble() * 70;

        var glow = _currentTheme.GlowBrush.Color;
        var tail = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, glow.R, glow.G, glow.B), 0),
                new GradientStop(Color.FromArgb(150, glow.R, glow.G, glow.B), 0.7),
                new GradientStop(Colors.White, 1),
            }
        };
        var star = new Path
        {
            Data = new LineGeometry(new Point(0, 0), new Point(length, 0)),
            Stroke = tail,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0,
        };
        // the line is drawn pointing right; turn it so its bright end leads along dir
        var move = new TranslateTransform(start.X, start.Y);
        star.RenderTransform = new TransformGroup
        {
            Children = { new TranslateTransform(-length, 0), new RotateTransform(Math.Atan2(dir.Y, dir.X) * 180 / Math.PI), move }
        };
        LivingLayer.Children.Add(star);

        var duration = TimeSpan.FromSeconds(0.75);
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(start.X, start.X + dir.X * travel, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(start.Y, start.Y + dir.Y * travel, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.25)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        fade.Completed += (s, e) => LivingLayer.Children.Remove(star);
        star.BeginAnimation(OpacityProperty, fade);
    }

    // ---------------------------------------------------------------- Elden Ring rune, Deep Sea creature: breathing glow

    private (Point Centre, double Radius)? BreathingGlowPlacement()
    {
        double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
        if (_currentSkin.Key == NoteSkin.EldenRingKey && EldenRingArt.RunePlacement(w, h, RuneBottomReserve(h)) is { } rune)
        {
            return (rune.Centre, rune.Box * 0.42);
        }
        if (_currentSkin.Key == NoteSkin.DeepSeaKey && DeepSeaArt.CreaturePlacement(w, h, RuneBottomReserve(h)) is { } creature)
        {
            string tag = _note.Id.ToString("N").Substring(0, 4).ToUpperInvariant();
            string which = DeepSeaArt.Resolve(_note.GetEmblem(NoteSkin.DeepSeaKey), tag);
            return (DeepSeaArt.GlowPoint(which, creature.Origin, creature.Box), creature.Box * (which == "Anglerfish" ? 0.2 : 0.38));
        }
        return null;
    }

    private void ShowBreathingGlow(Point centre, double radius)
    {
        var c = _currentTheme.GlowBrush.Color;
        if (_breathingGlow == null)
        {
            _breathingGlow = new Ellipse { IsHitTestVisible = false, Opacity = 0 };
            LivingLayer.Children.Add(_breathingGlow);
            _breathingGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.55, TimeSpan.FromSeconds(2.2))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        }
        _breathingGlow.Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(150, c.R, c.G, c.B), 0),
                new GradientStop(Color.FromArgb(50, c.R, c.G, c.B), 0.5),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
            }
        };
        _breathingGlow.Width = _breathingGlow.Height = radius * 2;
        Canvas.SetLeft(_breathingGlow, centre.X - radius);
        Canvas.SetTop(_breathingGlow, centre.Y - radius);
    }

    private void HideBreathingGlow()
    {
        if (_breathingGlow == null) return;
        _breathingGlow.BeginAnimation(OpacityProperty, null); // no animation left running
        LivingLayer.Children.Remove(_breathingGlow);
        _breathingGlow = null;
    }

    // ---------------------------------------------------------------- Cyberpunk: glitch when an edit is saved

    private void GlitchIfLiving()
    {
        if (!LivingOn || _currentSkin.Key != NoteSkin.CyberpunkKey || !IsVisible || LivingOverlay == null) return;
        double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
        if (w < 100 || h < 100) return;

        // the content jumps sideways for a few frames
        var shift = ContentGrid.RenderTransform as TranslateTransform;
        if (shift == null) ContentGrid.RenderTransform = shift = new TranslateTransform();
        var jitter = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(220) };
        foreach (var (x, t) in new[] { (0.0, 0.0), (-3.0, 0.15), (2.5, 0.35), (-1.5, 0.55), (1.0, 0.75), (0.0, 1.0) })
            jitter.KeyFrames.Add(new DiscreteDoubleKeyFrame(x, KeyTime.FromPercent(t)));
        shift.BeginAnimation(TranslateTransform.XProperty, jitter);

        // and a few slices of the accent colour flicker across it
        var accent = _currentTheme.GlowBrush.Color;
        for (int i = 0; i < 4; i++)
        {
            var slice = new Rectangle
            {
                Width = w * (0.25 + _livingRandom.NextDouble() * 0.5),
                Height = 1 + _livingRandom.NextDouble() * 3,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(90 + _livingRandom.Next(80)), accent.R, accent.G, accent.B)),
                Opacity = 0,
            };
            Canvas.SetLeft(slice, _livingRandom.NextDouble() * (w - slice.Width));
            Canvas.SetTop(slice, 20 + _livingRandom.NextDouble() * (h - 40));
            LivingOverlay.Children.Add(slice);
            var flicker = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(180 + i * 25), BeginTime = TimeSpan.FromMilliseconds(i * 20) };
            flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.4)));
            flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.6)));
            flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            flicker.Completed += (s, e) => LivingOverlay.Children.Remove(slice);
            slice.BeginAnimation(OpacityProperty, flicker);
        }
    }

    // ---------------------------------------------------------------- Medieval: wax seal on a locked note

    /// <summary>While a Medieval note is locked (and living themes are on) a wax seal sits on the lower rod;
    /// <paramref name="animate"/> presses it on, or lifts it off, instead of just showing or hiding it.</summary>
    private void UpdateWaxSeal(bool animate)
    {
        if (LivingOverlay == null) return;
        bool want = LivingOn && _note.IsLocked && _currentSkin.Key == NoteSkin.MedievalKey;
        if (want)
        {
            double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
            if (w < 160 || h < 120) return;
            const double size = 40;
            if (_waxSeal == null)
            {
                _waxSeal = new Image { Width = size, Height = size, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
                LivingOverlay.Children.Add(_waxSeal);
            }
            else
            {
                animate = false; // already pressed on
            }
            _waxSeal.Source = WaxSealArt.Render(_currentTheme.PrimaryBrush.Color, TxtTitle.Text, (int)Math.Round(size * Math.Max(1, VisualTreeHelper.GetDpi(this).PixelsPerDip)));
            // on the lower rod, below the footer text
            Canvas.SetLeft(_waxSeal, w / 2 - size / 2);
            Canvas.SetTop(_waxSeal, h - 12 - size / 2);
            if (animate)
            {
                var press = new ScaleTransform(1.7, 1.7);
                _waxSeal.RenderTransform = new TransformGroup { Children = { press, new RotateTransform(-8 + _livingRandom.NextDouble() * 16) } };
                var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
                press.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.7, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                press.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.7, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                _waxSeal.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            }
        }
        else if (_waxSeal != null)
        {
            var seal = _waxSeal;
            _waxSeal = null;
            if (animate)
            {
                var lift = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                lift.Completed += (s, e) => LivingOverlay.Children.Remove(seal);
                seal.BeginAnimation(OpacityProperty, lift);
            }
            else
            {
                LivingOverlay.Children.Remove(seal);
            }
        }
    }
}
