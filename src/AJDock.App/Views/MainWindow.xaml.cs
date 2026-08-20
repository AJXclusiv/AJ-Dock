using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using AJDock.App.Native;
using AJDock.App.Services;
using AJDock.App.ViewModels;
using AJDock.Core.Models;

namespace AJDock.App.Views;

public partial class MainWindow : Window
{
    private const double SpectrumWidth = 420;
    private const double SpectrumHeight = 82;
    private readonly DockViewModel _viewModel;
    private readonly WindowEffectService _windowEffectService;
    private readonly string? _snapshotPath;
    private readonly Dictionary<Button, IconAnimationState> _iconAnimationStates = new();
    private readonly DispatcherTimer _previewCloseTimer;
    private readonly DispatcherTimer _smartHideTimer;
    private Point? _lastDockMousePosition;
    private Point? _dockItemDragStart;
    private SettingsWindow? _settingsWindow;
    private bool _isHidden;
    private bool _isPointerOverDock;
    private bool _playedStartupAnimation;
    private double _spectrumPhase;
    private double _smoothedAudioPeak;
    private TimeSpan? _lastRenderingTime;

    public MainWindow(DockViewModel viewModel, WindowEffectService windowEffectService, string? snapshotPath = null)
    {
        _viewModel = viewModel;
        _windowEffectService = windowEffectService;
        _snapshotPath = snapshotPath;
        _previewCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(260)
        };
        _previewCloseTimer.Tick += (_, _) =>
        {
            _previewCloseTimer.Stop();
            _viewModel.HideWindowPreview();
        };
        _smartHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _smartHideTimer.Tick += (_, _) => UpdateSmartHide();
        DataContext = viewModel;
        InitializeComponent();
        DockRoot.Opacity = 0;
        DockStartupTransform.Y = 14;

        SourceInitialized += (_, _) => _windowEffectService.Apply(this, useBackdrop: false);
        Loaded += (_, _) =>
        {
            ApplyVisualSettings();
            PositionDock();
            RunStartupAnimation();
            _smartHideTimer.Start();
            if (!string.IsNullOrWhiteSpace(_snapshotPath))
            {
                _ = CaptureSnapshotAndShutdownAsync(_snapshotPath);
            }
        };
        SizeChanged += (_, _) => PositionDock();
        Closing += (_, _) =>
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _previewCloseTimer.Stop();
            _smartHideTimer.Stop();
            _viewModel.Dispose();
        };
        _viewModel.RequestLayoutUpdate += (_, _) =>
        {
            ApplyVisualSettings();
            PositionDock();
        };
        _viewModel.RequestOpenSettings += (_, _) => OpenSettings();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private void DockShell_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DockShell_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files)
        {
            _viewModel.PinDroppedFile(file);
        }
    }

    private void StartMenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            return;
        }

        e.Handled = true;
        OpenSettings();
    }

    private void DockShell_MouseMove(object sender, MouseEventArgs e)
    {
        _lastDockMousePosition = e.GetPosition(DockItems);
    }

    private void DockShell_MouseEnter(object sender, MouseEventArgs e)
    {
        _isPointerOverDock = true;
        if (_isHidden)
        {
            _isHidden = false;
            AnimateDockToVisible();
        }
    }

    private void DockShell_MouseLeave(object sender, MouseEventArgs e)
    {
        _isPointerOverDock = false;
        _lastDockMousePosition = null;
        HideHoverLabel();
        if (_viewModel.Settings.AutoHide)
        {
            _isHidden = true;
            AnimateDockToHidden();
        }
    }

    private void DockItem_MouseEnter(object sender, MouseEventArgs e)
    {
        _previewCloseTimer.Stop();
        if (sender is Button { DataContext: DockItemViewModel item })
        {
            ShowHoverLabel((Button)sender, item.DisplayName);
            _viewModel.ShowWindowPreview(item);
        }
    }

    private void DockItem_MouseLeave(object sender, MouseEventArgs e)
    {
        HideHoverLabel();
        _previewCloseTimer.Stop();
        _previewCloseTimer.Start();
    }

    private void DockItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dockItemDragStart = e.GetPosition(this);
    }

    private void DockItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _dockItemDragStart is not { } start
            || sender is not Button { DataContext: DockItemViewModel item } button)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        HideHoverLabel();
        _viewModel.HideWindowPreview();
        DragDrop.DoDragDrop(button, new DataObject(typeof(DockItemViewModel), item), DragDropEffects.Move);
        _dockItemDragStart = null;
    }

    private void DockItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(DockItemViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void DockItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is Button { DataContext: DockItemViewModel target }
            && e.Data.GetData(typeof(DockItemViewModel)) is DockItemViewModel source)
        {
            _viewModel.MoveDockItem(source, target);
            e.Handled = true;
        }
    }

    private void WindowPreviewPopup_MouseEnter(object sender, MouseEventArgs e)
    {
        _previewCloseTimer.Stop();
    }

    private void WindowPreviewPopup_MouseLeave(object sender, MouseEventArgs e)
    {
        _previewCloseTimer.Stop();
        _previewCloseTimer.Start();
    }

    public void ActivateAndShowSettings()
    {
        AppLog.Write("Activating dock and showing settings.");
        _isHidden = false;
        BeginAnimation(TopProperty, null);
        ApplyVisualSettings();
        PositionDock();
        Show();
        Activate();
        OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(new SettingsViewModel(_viewModel.Settings, _viewModel.SaveAndApply))
        {
            Owner = this
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ApplyVisualSettings()
    {
        var magnificationHeadroom = _viewModel.Settings.IconSize * (DockSettings.MaxMagnification - 1) + 14;
        var chromeHeight = Math.Clamp(_viewModel.Settings.DockSize, _viewModel.Settings.IconSize + 8, _viewModel.Settings.IconSize + 18);
        DockHeadroomRow.Height = new GridLength(magnificationHeadroom);
        DockChromeRow.Height = new GridLength(chromeHeight);
        DockRoot.MinHeight = chromeHeight + magnificationHeadroom;
        DockRoot.Height = double.NaN;
        DockChrome.Height = chromeHeight;
        DockContent.Height = chromeHeight;
        DockChrome.CornerRadius = new CornerRadius(_viewModel.Settings.CornerRadius);
        DockChrome.Background = CreateDockBrush();
        DockChrome.BorderThickness = _viewModel.Settings.BorderOpacity <= 0.01
            ? new Thickness(0)
            : new Thickness(1);
        DockChrome.BorderBrush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(_viewModel.Settings.BorderOpacity * 255),
            166,
            219,
            255));
        ApplyAudioLineTheme();
        if (DockChrome.Effect is DropShadowEffect shadow)
        {
            shadow.Opacity = _viewModel.Settings.ShadowIntensity;
            shadow.BlurRadius = 22 + (_viewModel.Settings.BlurAmount * 0.45);
        }

        Topmost = _viewModel.Settings.AlwaysOnTop;
    }

    private void PositionDock()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        var edgeMargin = _viewModel.Settings.DockOffset;

        switch (_viewModel.Settings.Position)
        {
            case DockPosition.Top:
                Left = work.Left + ((work.Width - ActualWidth) / 2);
                Top = work.Top + edgeMargin;
                break;
            case DockPosition.Left:
                Left = work.Left + edgeMargin;
                Top = work.Top + ((work.Height - ActualHeight) / 2);
                break;
            case DockPosition.Right:
                Left = work.Right - ActualWidth - edgeMargin;
                Top = work.Top + ((work.Height - ActualHeight) / 2);
                break;
            default:
                Left = work.Left + ((work.Width - ActualWidth) / 2);
                Top = work.Bottom - ActualHeight - edgeMargin;
                break;
        }
    }

    private void AnimateDockToVisible()
    {
        BeginAnimation(TopProperty, null);
        PositionDock();
    }

    private void RunStartupAnimation()
    {
        if (_playedStartupAnimation)
        {
            return;
        }

        _playedStartupAnimation = true;
        DockRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        DockStartupTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(340))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 }
        });
    }

    private void ShowHoverLabel(Button target, string text)
    {
        DockHoverLabelText.Text = text;
        DockHoverLabelPopup.PlacementTarget = target;
        DockHoverLabelPopup.IsOpen = true;
    }

    private void HideHoverLabel()
    {
        DockHoverLabelPopup.IsOpen = false;
    }

    private void AnimateDockToHidden()
    {
        var work = SystemParameters.WorkArea;
        var hiddenTop = _viewModel.Settings.Position == DockPosition.Top
            ? work.Top - ActualHeight + 5
            : work.Bottom - 5;

        BeginAnimation(TopProperty, new DoubleAnimation(hiddenTop, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void UpdateSmartHide()
    {
        if (!_viewModel.Settings.AutoHide)
        {
            if (_isHidden)
            {
                _isHidden = false;
                AnimateDockToVisible();
            }

            return;
        }

        if (_isPointerOverDock
            || _viewModel.IsWifiPopoverOpen
            || _viewModel.IsSystemPopoverOpen
            || _viewModel.IsVolumePopoverOpen
            || _viewModel.IsPreviewOpen)
        {
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero)
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        if (processId == Environment.ProcessId)
        {
            return;
        }

        var shouldHide = NativeMethods.IsZoomed(foreground) || ForegroundIntersectsDock(foreground);
        if (shouldHide && !_isHidden)
        {
            _isHidden = true;
            AnimateDockToHidden();
        }
    }

    private bool ForegroundIntersectsDock(nint hWnd)
    {
        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            return false;
        }

        var dockHandle = new WindowInteropHelper(this).Handle;
        if (hWnd == dockHandle)
        {
            return false;
        }

        var dockRect = new Rect(Left, Top, ActualWidth, ActualHeight);
        var foregroundRect = new Rect(
            windowRect.Left,
            windowRect.Top,
            Math.Max(0, windowRect.Width),
            Math.Max(0, windowRect.Height));
        return dockRect.IntersectsWith(foregroundRect);
    }

    private Brush CreateDockBrush()
    {
        var alpha = (byte)Math.Round(_viewModel.Settings.Transparency * 255);
        var theme = _viewModel.Settings.ThemeName;
        var dockColor = ParseDockColor(_viewModel.Settings.DockColorHex);

        if (theme.Equals("Cyber", StringComparison.OrdinalIgnoreCase))
        {
            return new LinearGradientBrush(
                Stops(Color.FromArgb(alpha, 4, 26, 34), WithAlpha(Darken(dockColor, 0.55), alpha)),
                new Point(0, 0),
                new Point(1, 1));
        }

        if (theme.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(WithAlpha(dockColor, alpha));
        }

        if (theme.Equals("macOS Glass", StringComparison.OrdinalIgnoreCase))
        {
            return new LinearGradientBrush(
                Stops(WithAlpha(Lighten(dockColor, 0.28), (byte)Math.Min(210, alpha + 28)), WithAlpha(Darken(dockColor, 0.18), alpha)),
                new Point(0, 0),
                new Point(0, 1));
        }

        if (theme.Equals("Minimal Dark", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(WithAlpha(Darken(dockColor, 0.22), (byte)Math.Max((int)alpha, 220)));
        }

        return new LinearGradientBrush(
            Stops(WithAlpha(Lighten(dockColor, 0.2), (byte)Math.Min(235, alpha + 36)), WithAlpha(Darken(dockColor, 0.12), alpha)),
            new Point(0, 0),
            new Point(0, 1));
    }

    private static Color ParseDockColor(string color)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }
        catch
        {
            return Color.FromRgb(12, 18, 25);
        }
    }

    private void ApplyAudioLineTheme()
    {
        var baseColor = ParseDockColor(_viewModel.Settings.AudioLineBaseHex);
        var accentColor = ParseDockColor(_viewModel.Settings.AudioLineAccentHex);
        var baseBrush = new SolidColorBrush(baseColor);
        var accentBrush = new SolidColorBrush(accentColor);
        if (baseBrush.CanFreeze)
        {
            baseBrush.Freeze();
        }

        if (accentBrush.CanFreeze)
        {
            accentBrush.Freeze();
        }

        SpotifyLeftSpectrum.Stroke = baseBrush;
        SpotifyRightSpectrum.Stroke = baseBrush;
        SpotifyLeftSpectrumEcho.Stroke = baseBrush;
        SpotifyRightSpectrumEcho.Stroke = baseBrush;
        SpotifyLeftSpectrumGlow.Stroke = accentBrush;
        SpotifyRightSpectrumGlow.Stroke = accentBrush;
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(color.R + ((255 - color.R) * amount), 0, 255),
            (byte)Math.Clamp(color.G + ((255 - color.G) * amount), 0, 255),
            (byte)Math.Clamp(color.B + ((255 - color.B) * amount), 0, 255));
    }

    private static Color Darken(Color color, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.G * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.B * (1 - amount), 0, 255));
    }

    private static GradientStopCollection Stops(Color start, Color end)
    {
        var stops = new GradientStopCollection();
        stops.Add(new GradientStop(start, 0));
        stops.Add(new GradientStop(end, 1));
        return stops;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        UpdateSpotifySpectrum();

        var buttons = FindVisualChildren<Button>(DockItems)
            .Where(button => button.DataContext is DockItemViewModel)
            .ToList();

        if (buttons.Count == 0)
        {
            return;
        }

        var pointer = _lastDockMousePosition;
        var influenceRadius = Math.Max(_viewModel.Settings.IconSize * 2.4, 130);
        var frameSeconds = GetFrameSeconds(e);
        var speedFactor = Math.Clamp(120 / _viewModel.Settings.AnimationSpeed, 0.3, 2.4);
        var stiffness = 220 + (260 * speedFactor);
        var damping = 20 + (10 / Math.Sqrt(speedFactor));
        var focusedButton = buttons.FirstOrDefault(button => button.IsMouseOver);
        if (focusedButton is null && _isPointerOverDock && pointer is { } focusPosition)
        {
            focusedButton = buttons
                .OrderBy(button =>
                {
                    var center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), DockItems);
                    return Math.Abs(focusPosition.X - center.X);
                })
                .FirstOrDefault();
        }

        foreach (var button in buttons)
        {
            var state = GetAnimationState(button);
            var targetScale = 1d;
            var targetY = 0d;
            var distance = double.MaxValue;
            var isFocused = false;

            if (_isPointerOverDock && pointer is { } position)
            {
                var center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), DockItems);
                distance = Math.Abs(position.X - center.X);
                var normalized = Math.Clamp(distance / influenceRadius, 0, 1);
                var falloff = Math.Pow((Math.Cos(normalized * Math.PI) + 1) / 2, 1.35);
                targetScale = 1 + ((_viewModel.Settings.MagnificationAmount - 1) * falloff);
                targetY = -(_viewModel.Settings.IconSize * (targetScale - 1) * 0.2);

            }

            isFocused = button.IsMouseOver || button == focusedButton || distance < _viewModel.Settings.IconSize * 0.58;
            if (isFocused && !state.WasFocused)
            {
                state.ScaleVelocity += 5.2 * speedFactor;
                state.TranslateVelocity -= _viewModel.Settings.IconSize * 5.8 * speedFactor;
            }

            StepSpring(ref state.Scale, ref state.ScaleVelocity, targetScale, stiffness, damping, frameSeconds);
            StepSpring(ref state.TranslateY, ref state.TranslateVelocity, targetY, stiffness * 0.9, damping * 0.86, frameSeconds);

            var bounce = isFocused
                ? Math.Sin(Math.Min(state.FocusPulse, 1) * Math.PI) * 0.08
                : 0;
            state.FocusPulse = isFocused
                ? Math.Min(1, state.FocusPulse + (frameSeconds * 7.5))
                : Math.Max(0, state.FocusPulse - (frameSeconds * 8));
            state.WasFocused = isFocused;

            var displayScale = Math.Clamp(state.Scale + bounce, 0.82, DockSettings.MaxMagnification + 0.18);
            var squash = isFocused ? Math.Clamp(1 - ((displayScale - 1) * 0.08), 0.9, 1) : 1;
            state.ScaleTransform.ScaleX = displayScale * (1 + ((1 - squash) * 0.55));
            state.ScaleTransform.ScaleY = displayScale * squash;
            state.TranslateTransform.Y = state.TranslateY;
            SetDockItemZIndex(button, Math.Max(0, (int)Math.Round(displayScale * 100)));
        }

        if (focusedButton is not null)
        {
            SetDockItemZIndex(focusedButton, 10_000);
        }
    }

    private void UpdateSpotifySpectrum()
    {
        var peak = Math.Clamp(_viewModel.GetAudioPeakPercent() / 100d, 0, 1);
        var sensitivity = Math.Clamp(_viewModel.Settings.AudioVisualizerSensitivity, 0.2, 1.5);
        var shapedPeak = Math.Clamp(Math.Pow(peak, 0.62) * (0.45 + sensitivity), 0, 1);
        _smoothedAudioPeak += (shapedPeak - _smoothedAudioPeak) * 0.46;
        if (peak < 0.006)
        {
            _smoothedAudioPeak *= 0.72;
            if (_smoothedAudioPeak < 0.012)
            {
                _smoothedAudioPeak = 0;
                SetSpectrumIdle();
                return;
            }
        }

        _spectrumPhase += 0.28 + (_smoothedAudioPeak * (0.32 + sensitivity * 0.42));

        var baseline = BuildSpectrumBaselinePoints();
        var left = BuildSpectrumPoints(SpectrumWidth, SpectrumHeight, mirrored: true);
        var right = BuildSpectrumPoints(SpectrumWidth, SpectrumHeight, mirrored: false);
        var leftEcho = BuildSpectrumEchoPoints(SpectrumWidth, SpectrumHeight, mirrored: true);
        var rightEcho = BuildSpectrumEchoPoints(SpectrumWidth, SpectrumHeight, mirrored: false);
        SpotifyLeftSpectrum.Points = baseline;
        SpotifyLeftSpectrumGlow.Points = left;
        SpotifyLeftSpectrumEcho.Points = leftEcho;
        SpotifyRightSpectrum.Points = baseline;
        SpotifyRightSpectrumGlow.Points = right;
        SpotifyRightSpectrumEcho.Points = rightEcho;

        SpotifyLeftSpectrum.Opacity = 1;
        SpotifyRightSpectrum.Opacity = 1;
        SpotifyLeftSpectrumEcho.Opacity = 1;
        SpotifyRightSpectrumEcho.Opacity = SpotifyLeftSpectrumEcho.Opacity;
        SpotifyLeftSpectrumGlow.Opacity = 1;
        SpotifyRightSpectrumGlow.Opacity = SpotifyLeftSpectrumGlow.Opacity;
    }

    private double GetFrameSeconds(EventArgs args)
    {
        if (args is not RenderingEventArgs renderingArgs)
        {
            return 1d / 60d;
        }

        var elapsed = renderingArgs.RenderingTime;
        var seconds = _lastRenderingTime is { } previous
            ? (elapsed - previous).TotalSeconds
            : 1d / 60d;
        _lastRenderingTime = elapsed;
        return Math.Clamp(seconds, 1d / 120d, 1d / 30d);
    }

    private static void StepSpring(ref double value, ref double velocity, double target, double stiffness, double damping, double seconds)
    {
        var displacement = target - value;
        var acceleration = (displacement * stiffness) - (velocity * damping);
        velocity += acceleration * seconds;
        value += velocity * seconds;

        if (Math.Abs(target - value) < 0.0008 && Math.Abs(velocity) < 0.0008)
        {
            value = target;
            velocity = 0;
        }
    }

    private PointCollection BuildSpectrumPoints(double width, double height, bool mirrored)
    {
        const int samples = 160;
        var points = new PointCollection(samples);
        var center = height / 2;
        var activity = Math.Clamp(_smoothedAudioPeak, 0, 1);
        var sensitivity = Math.Clamp(_viewModel.Settings.AudioVisualizerSensitivity, 0.2, 1.5);
        var pulse = 0.62 + (sensitivity * 0.3) + (activity * 0.26);
        var amplitude = 0.96 + (sensitivity * 0.76);
        for (var index = 0; index < samples; index++)
        {
            var t = index / (double)(samples - 1);
            var x = mirrored ? width - (t * width) : t * width;
            var broadEnvelope = Gaussian(t, 0.5, 0.42);
            var edgeTaper = SmoothStep(Math.Clamp(Math.Min(t / 0.06, (1 - t) / 0.06), 0, 1));
            var activeSpan = broadEnvelope * edgeTaper;
            var signature = (Gaussian(t, 0.2, 0.075) * 18)
                + (Gaussian(t, 0.35, 0.1) * 11)
                - (Gaussian(t, 0.5, 0.11) * 15);
            var distributed = (Math.Sin((_spectrumPhase * 1.35) - (t * 8.2)) * 9.6)
                + (Math.Sin((_spectrumPhase * 1.9) + (t * 13.8)) * 5.9)
                + (Math.Sin((_spectrumPhase * 2.65) - (t * 21)) * 2.2);
            var satellite = (Gaussian(t, 0.66, 0.16) * Math.Sin((_spectrumPhase * 1.1) + (t * 6.4)) * 10.4)
                + (Gaussian(t, 0.82, 0.12) * Math.Sin((_spectrumPhase * 1.45) - (t * 8.5)) * 6.2);
            var wave = ((signature * 0.72) + distributed + satellite) * activeSpan * activity * amplitude * pulse;
            var y = center - wave;
            points.Add(new Point(x, Math.Clamp(y, 2, height - 2)));
        }

        return points;
    }

    private PointCollection BuildSpectrumEchoPoints(double width, double height, bool mirrored)
    {
        const int samples = 160;
        var points = new PointCollection(samples);
        var center = height / 2;
        var activity = Math.Clamp(_smoothedAudioPeak, 0, 1);
        var sensitivity = Math.Clamp(_viewModel.Settings.AudioVisualizerSensitivity, 0.2, 1.5);
        var amplitude = 0.96 + (sensitivity * 0.76);
        for (var index = 0; index < samples; index++)
        {
            var t = index / (double)(samples - 1);
            var x = mirrored ? width - (t * width) : t * width;
            var broadEnvelope = Gaussian(t, 0.5, 0.42);
            var edgeTaper = SmoothStep(Math.Clamp(Math.Min(t / 0.06, (1 - t) / 0.06), 0, 1));
            var activeSpan = broadEnvelope * edgeTaper;
            var crossing = (Gaussian(t, 0.25, 0.1) * 10.8)
                - (Gaussian(t, 0.43, 0.13) * 10.2)
                + (Gaussian(t, 0.63, 0.18) * Math.Sin((_spectrumPhase * 1.12) + (t * 8)) * 6.2);
            var longMotion = (Math.Sin((_spectrumPhase * 1.05) + (t * 7)) * 7.4)
                + (Math.Sin((_spectrumPhase * 1.75) - (t * 14.5)) * 3.6);
            var wave = (crossing + longMotion) * activeSpan * activity * amplitude;
            var y = center - wave;
            points.Add(new Point(x, Math.Clamp(y, 4, height - 4)));
        }

        return points;
    }

    private static PointCollection BuildSpectrumBaselinePoints()
    {
        return new PointCollection
        {
            new(0, SpectrumHeight / 2),
            new(SpectrumWidth, SpectrumHeight / 2)
        };
    }

    private static double Gaussian(double value, double center, double spread)
    {
        return Math.Exp(-Math.Pow((value - center) / spread, 2));
    }

    private static double SmoothStep(double value)
    {
        return value * value * (3 - (2 * value));
    }

    private void SetSpectrumIdle()
    {
        var idle = BuildSpectrumBaselinePoints();
        SpotifyLeftSpectrum.Points = idle;
        SpotifyLeftSpectrumGlow.Points = idle;
        SpotifyLeftSpectrumEcho.Points = idle;
        SpotifyRightSpectrum.Points = idle;
        SpotifyRightSpectrumGlow.Points = idle;
        SpotifyRightSpectrumEcho.Points = idle;
        SpotifyLeftSpectrum.Opacity = 1;
        SpotifyRightSpectrum.Opacity = 1;
        SpotifyLeftSpectrumEcho.Opacity = 0;
        SpotifyRightSpectrumEcho.Opacity = 0;
        SpotifyLeftSpectrumGlow.Opacity = 0;
        SpotifyRightSpectrumGlow.Opacity = 0;
    }

    private static void SetDockItemZIndex(Button button, int zIndex)
    {
        Panel.SetZIndex(button, zIndex);

        if (FindVisualParent<ContentPresenter>(button) is { } presenter)
        {
            Panel.SetZIndex(presenter, zIndex);
        }
    }

    private IconAnimationState GetAnimationState(Button button)
    {
        if (_iconAnimationStates.TryGetValue(button, out var state))
        {
            return state;
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        button.RenderTransformOrigin = new Point(0.5, 1);
        button.RenderTransform = new TransformGroup
        {
            Children = new TransformCollection { scale, translate }
        };

        state = new IconAnimationState(scale, translate);
        _iconAnimationStates[button] = state;
        return state;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T typed)
            {
                return typed;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private sealed class IconAnimationState
    {
        public IconAnimationState(ScaleTransform scaleTransform, TranslateTransform translateTransform)
        {
            ScaleTransform = scaleTransform;
            TranslateTransform = translateTransform;
        }

        public ScaleTransform ScaleTransform { get; }
        public TranslateTransform TranslateTransform { get; }
        public double Scale = 1;
        public double ScaleVelocity;
        public double TranslateY;
        public double TranslateVelocity;
        public double FocusPulse;
        public bool WasFocused;
    }

    private async Task CaptureSnapshotAndShutdownAsync(string path)
    {
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();

            var width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            AppLog.Write($"Snapshot saved to {path}.");
        }
        catch (Exception exception)
        {
            AppLog.Write("Snapshot failed.", exception);
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }
}
