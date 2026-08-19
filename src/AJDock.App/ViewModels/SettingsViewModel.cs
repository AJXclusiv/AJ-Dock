using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action _persistAndApply;

    public SettingsViewModel(DockSettings settings, Action persistAndApply)
    {
        Settings = settings;
        _persistAndApply = persistAndApply;
        ToggleDockTakeoverModeCommand = new RelayCommand(_ => ToggleDockTakeoverMode());
        SetDockColorCommand = new RelayCommand(SetDockColor);
        ApplyThemePresetCommand = new RelayCommand(ApplyThemePreset);
    }

    public DockSettings Settings { get; }
    public Array DockPositions => Enum.GetValues(typeof(DockPosition));
    public Array ClockDisplayModes => Enum.GetValues(typeof(ClockDisplayMode));
    public Array ClockSeparators => Enum.GetValues(typeof(ClockSeparator));
    public IReadOnlyList<string> DateFormats { get; } = ["ddd, MMM d", "MMM d", "M/d/yyyy", "MM/dd/yy"];
    public IReadOnlyList<string> ThemeNames { get; } = ["Windows 11", "macOS Glass", "Minimal Dark", "Cyber", "Transparent"];
    public IReadOnlyList<string> DockColorPresets { get; } = ["#0C1219", "#102434", "#121827", "#241B32", "#082A24", "#2B2118"];
    public IReadOnlyList<string> ThemePresetNames { get; } = ["Clear Glass", "Frosted Blue", "Graphite", "Neon Edge", "Warm Dark", "OLED Pulse", "Plex Theater", "Aurora Glass"];
    public RelayCommand ToggleDockTakeoverModeCommand { get; }
    public RelayCommand SetDockColorCommand { get; }
    public RelayCommand ApplyThemePresetCommand { get; }

    public DockPosition Position
    {
        get => Settings.Position;
        set
        {
            if (Settings.Position == value)
            {
                return;
            }

            Settings.Position = value;
            Save();
            OnPropertyChanged();
        }
    }

    public double IconSize
    {
        get => Settings.IconSize;
        set
        {
            if (SetDouble(Settings.IconSize, value, v => Settings.IconSize = v))
            {
                OnPropertyChanged(nameof(DockSize));
            }
        }
    }

    public double DockSize
    {
        get => Settings.DockSize;
        set => SetDouble(Settings.DockSize, value, v => Settings.DockSize = v);
    }

    public double IconSpacing
    {
        get => Settings.IconSpacing;
        set => SetDouble(Settings.IconSpacing, value, v => Settings.IconSpacing = v);
    }

    public double MagnificationAmount
    {
        get => Settings.MagnificationAmount;
        set => SetDouble(Settings.MagnificationAmount, value, v => Settings.MagnificationAmount = v);
    }

    public double AnimationSpeed
    {
        get => Settings.AnimationSpeed;
        set => SetDouble(Settings.AnimationSpeed, value, v => Settings.AnimationSpeed = v);
    }

    public double Transparency
    {
        get => Settings.Transparency;
        set => SetDouble(Settings.Transparency, value, v => Settings.Transparency = v);
    }

    public double BlurAmount
    {
        get => Settings.BlurAmount;
        set => SetDouble(Settings.BlurAmount, value, v => Settings.BlurAmount = v);
    }

    public double CornerRadius
    {
        get => Settings.CornerRadius;
        set => SetDouble(Settings.CornerRadius, value, v => Settings.CornerRadius = v);
    }

    public double BorderOpacity
    {
        get => Settings.BorderOpacity;
        set => SetDouble(Settings.BorderOpacity, value, v => Settings.BorderOpacity = v);
    }

    public double ShadowIntensity
    {
        get => Settings.ShadowIntensity;
        set => SetDouble(Settings.ShadowIntensity, value, v => Settings.ShadowIntensity = v);
    }

    public double DockOffset
    {
        get => Settings.DockOffset;
        set => SetDouble(Settings.DockOffset, value, v => Settings.DockOffset = v);
    }

    public double AudioVisualizerSensitivity
    {
        get => Settings.AudioVisualizerSensitivity;
        set => SetDouble(Settings.AudioVisualizerSensitivity, value, v => Settings.AudioVisualizerSensitivity = v);
    }

    public string ThemeName
    {
        get => Settings.ThemeName;
        set
        {
            if (Settings.ThemeName == value)
            {
                return;
            }

            Settings.ThemeName = value;
            Save();
            OnPropertyChanged();
        }
    }

    public string DockColorHex
    {
        get => Settings.DockColorHex;
        set
        {
            if (Settings.DockColorHex == value)
            {
                return;
            }

            Settings.DockColorHex = value;
            Save();
            OnPropertyChanged();
        }
    }

    public ClockDisplayMode ClockDisplayMode
    {
        get => Settings.ClockDisplayMode;
        set
        {
            if (Settings.ClockDisplayMode == value)
            {
                return;
            }

            Settings.ClockDisplayMode = value;
            Save();
            OnPropertyChanged();
        }
    }

    public bool Use24HourClock
    {
        get => Settings.Use24HourClock;
        set => SetBool(Settings.Use24HourClock, value, v => Settings.Use24HourClock = v);
    }

    public bool ShowSeconds
    {
        get => Settings.ShowSeconds;
        set => SetBool(Settings.ShowSeconds, value, v => Settings.ShowSeconds = v);
    }

    public string DateFormat
    {
        get => Settings.DateFormat;
        set
        {
            if (Settings.DateFormat == value)
            {
                return;
            }

            Settings.DateFormat = value;
            Save();
            OnPropertyChanged();
        }
    }

    public ClockSeparator ClockSeparator
    {
        get => Settings.ClockSeparator;
        set
        {
            if (Settings.ClockSeparator == value)
            {
                return;
            }

            Settings.ClockSeparator = value;
            Save();
            OnPropertyChanged();
        }
    }

    public bool AutoHide
    {
        get => Settings.AutoHide;
        set => SetBool(Settings.AutoHide, value, v => Settings.AutoHide = v);
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set => SetBool(Settings.AlwaysOnTop, value, v => Settings.AlwaysOnTop = v);
    }

    public bool HideWindowsTaskbar
    {
        get => Settings.HideWindowsTaskbar;
        set
        {
            if (Settings.HideWindowsTaskbar == value)
            {
                return;
            }

            Settings.HideWindowsTaskbar = value;
            if (!value)
            {
                Settings.DockTakeoverMode = false;
            }

            Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DockTakeoverMode));
            OnPropertyChanged(nameof(DockTakeoverModeText));
            OnPropertyChanged(nameof(DockTakeoverModeStatus));
        }
    }

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set => SetBool(Settings.StartWithWindows, value, v => Settings.StartWithWindows = v);
    }

    public bool DockTakeoverMode
    {
        get => Settings.DockTakeoverMode;
        set
        {
            if (Settings.DockTakeoverMode == value)
            {
                return;
            }

            Settings.DockTakeoverMode = value;
            if (value)
            {
                ApplyDockTakeoverPreset();
            }
            else
            {
                Settings.HideWindowsTaskbar = false;
            }

            Save();
            NotifyTakeoverPropertiesChanged();
        }
    }

    public string DockTakeoverModeText => DockTakeoverMode ? "Exit AJ Dock Takeover" : "Enable AJ Dock Takeover";
    public string DockTakeoverModeStatus => DockTakeoverMode
        ? "AJ Dock is replacing the Windows taskbar surface. Turn this off to restore the native taskbar."
        : "Hide the Windows taskbar and promote AJ Dock as the desktop control surface.";

    private bool SetDouble(double current, double value, Action<double> setter)
    {
        if (Math.Abs(current - value) < 0.01)
        {
            return false;
        }

        setter(value);
        Save();
        OnPropertyChanged();
        return true;
    }

    private void SetBool(bool current, bool value, Action<bool> setter)
    {
        if (current == value)
        {
            return;
        }

        setter(value);
        Save();
        OnPropertyChanged();
    }

    private void ToggleDockTakeoverMode()
    {
        DockTakeoverMode = !DockTakeoverMode;
    }

    private void SetDockColor(object? parameter)
    {
        if (parameter is string color)
        {
            DockColorHex = color;
        }
    }

    private void ApplyThemePreset(object? parameter)
    {
        if (parameter is not string preset)
        {
            return;
        }

        switch (preset)
        {
            case "Frosted Blue":
                Settings.ThemeName = "Windows 11";
                Settings.DockColorHex = "#102434";
                Settings.AudioLineBaseHex = "#FFFFFF";
                Settings.AudioLineAccentHex = "#93F7FF";
                Settings.Transparency = 0.08;
                Settings.BlurAmount = 30;
                Settings.CornerRadius = 18;
                Settings.ShadowIntensity = 0.22;
                break;
            case "Graphite":
                Settings.ThemeName = "Minimal Dark";
                Settings.DockColorHex = "#121827";
                Settings.AudioLineBaseHex = "#EAF0F7";
                Settings.AudioLineAccentHex = "#A8B6C8";
                Settings.Transparency = 0;
                Settings.BlurAmount = 18;
                Settings.CornerRadius = 14;
                Settings.ShadowIntensity = 0.16;
                break;
            case "Neon Edge":
                Settings.ThemeName = "Cyber";
                Settings.DockColorHex = "#061E2A";
                Settings.AudioLineBaseHex = "#F7FFFFFF";
                Settings.AudioLineAccentHex = "#00D8FF";
                Settings.AudioVisualizerSensitivity = 0.8;
                Settings.Transparency = 0.05;
                Settings.BlurAmount = 32;
                Settings.CornerRadius = 16;
                Settings.ShadowIntensity = 0.32;
                break;
            case "Warm Dark":
                Settings.ThemeName = "macOS Glass";
                Settings.DockColorHex = "#2B2118";
                Settings.AudioLineBaseHex = "#FFF3E6";
                Settings.AudioLineAccentHex = "#FFB86B";
                Settings.Transparency = 0.08;
                Settings.BlurAmount = 26;
                Settings.CornerRadius = 18;
                Settings.ShadowIntensity = 0.2;
                break;
            case "OLED Pulse":
                Settings.ThemeName = "Transparent";
                Settings.DockColorHex = "#05070A";
                Settings.AudioLineBaseHex = "#FFFFFF";
                Settings.AudioLineAccentHex = "#7CFFCB";
                Settings.AudioVisualizerSensitivity = 0.72;
                Settings.Transparency = 0;
                Settings.BlurAmount = 18;
                Settings.CornerRadius = 14;
                Settings.ShadowIntensity = 0.24;
                break;
            case "Plex Theater":
                Settings.ThemeName = "Minimal Dark";
                Settings.DockColorHex = "#15120A";
                Settings.AudioLineBaseHex = "#FFF7E8";
                Settings.AudioLineAccentHex = "#E5A00D";
                Settings.AudioVisualizerSensitivity = 0.68;
                Settings.Transparency = 0.02;
                Settings.BlurAmount = 22;
                Settings.CornerRadius = 16;
                Settings.ShadowIntensity = 0.22;
                break;
            case "Aurora Glass":
                Settings.ThemeName = "macOS Glass";
                Settings.DockColorHex = "#10262C";
                Settings.AudioLineBaseHex = "#F8FFFFFF";
                Settings.AudioLineAccentHex = "#87FFD8";
                Settings.AudioVisualizerSensitivity = 0.64;
                Settings.Transparency = 0.08;
                Settings.BlurAmount = 34;
                Settings.CornerRadius = 18;
                Settings.ShadowIntensity = 0.26;
                break;
            default:
                Settings.ThemeName = "Transparent";
                Settings.DockColorHex = "#0C1219";
                Settings.AudioLineBaseHex = "#FFFFFF";
                Settings.AudioLineAccentHex = "#93F7FF";
                Settings.AudioVisualizerSensitivity = 0.55;
                Settings.Transparency = 0;
                Settings.BlurAmount = 24;
                Settings.CornerRadius = 16;
                Settings.ShadowIntensity = 0.18;
                break;
        }

        Save();
        NotifyAppearancePropertiesChanged();
    }

    private void ApplyDockTakeoverPreset()
    {
        Settings.HideWindowsTaskbar = true;
        Settings.AlwaysOnTop = true;
        Settings.AutoHide = false;
        Settings.StartWithWindows = true;
        Settings.Position = DockPosition.Bottom;
        Settings.DockOffset = Math.Min(Settings.DockOffset, 18);
        Settings.ThemeName = "Transparent";
        Settings.DockColorHex = "#0C1219";
        Settings.Transparency = 0;
        Settings.BorderOpacity = 0;
    }

    private void NotifyTakeoverPropertiesChanged()
    {
        OnPropertyChanged(nameof(DockTakeoverMode));
        OnPropertyChanged(nameof(DockTakeoverModeText));
        OnPropertyChanged(nameof(DockTakeoverModeStatus));
        OnPropertyChanged(nameof(HideWindowsTaskbar));
        OnPropertyChanged(nameof(AutoHide));
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(DockOffset));
        OnPropertyChanged(nameof(ThemeName));
        OnPropertyChanged(nameof(DockColorHex));
        OnPropertyChanged(nameof(Transparency));
        OnPropertyChanged(nameof(BorderOpacity));
        OnPropertyChanged(nameof(AudioVisualizerSensitivity));
    }

    private void NotifyAppearancePropertiesChanged()
    {
        OnPropertyChanged(nameof(ThemeName));
        OnPropertyChanged(nameof(DockColorHex));
        OnPropertyChanged(nameof(Transparency));
        OnPropertyChanged(nameof(BlurAmount));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(ShadowIntensity));
        OnPropertyChanged(nameof(AudioVisualizerSensitivity));
    }

    private void Save()
    {
        Settings.Normalize();
        _persistAndApply();
    }
}
