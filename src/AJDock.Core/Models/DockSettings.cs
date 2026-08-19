using System.Text.Json.Serialization;

namespace AJDock.Core.Models;

public sealed class DockSettings
{
    public const int CurrentVisualProfileVersion = 4;
    public const double MinIconSize = 24;
    public const double MaxIconSize = 96;
    public const double MinSpacing = 2;
    public const double MaxSpacing = 32;
    public const double MinMagnification = 1;
    public const double MaxMagnification = 2.5;

    public DockPosition Position { get; set; } = DockPosition.Bottom;
    public double IconSize { get; set; } = 48;
    public double DockSize { get; set; } = 56;
    public double IconSpacing { get; set; } = 12;
    public double MagnificationAmount { get; set; } = 1.8;
    public double AnimationSpeed { get; set; } = 120;
    public double Transparency { get; set; } = 0;
    public double BlurAmount { get; set; } = 24;
    public double CornerRadius { get; set; } = 16;
    public double BorderOpacity { get; set; } = 0;
    public double ShadowIntensity { get; set; } = 0.18;
    public double DockOffset { get; set; } = 32;
    public double AudioVisualizerSensitivity { get; set; } = 0.55;
    public string AudioLineBaseHex { get; set; } = "#FFFFFF";
    public string AudioLineAccentHex { get; set; } = "#93F7FF";
    public string DockColorHex { get; set; } = "#0C1219";
    public string ThemeName { get; set; } = "Transparent";
    public ClockDisplayMode ClockDisplayMode { get; set; } = ClockDisplayMode.DateAndTime;
    public bool Use24HourClock { get; set; }
    public bool ShowSeconds { get; set; }
    public string DateFormat { get; set; } = "ddd, MMM d";
    public ClockSeparator ClockSeparator { get; set; } = ClockSeparator.Bullet;
    public bool ShowSystemStatusIcons { get; set; }
    public bool AutoHide { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool HideWindowsTaskbar { get; set; }
    public bool StartWithWindows { get; set; }
    public bool DockTakeoverMode { get; set; }
    public int VisualProfileVersion { get; set; }
    public List<PinnedApp> PinnedApps { get; set; } = [];

    [JsonIgnore]
    public double ClampedIconSize => Math.Clamp(IconSize, MinIconSize, MaxIconSize);

    public void Normalize()
    {
        IconSize = Math.Clamp(IconSize, MinIconSize, MaxIconSize);
        DockSize = Math.Clamp(DockSize, IconSize + 8, 120);
        IconSpacing = Math.Clamp(IconSpacing, MinSpacing, MaxSpacing);
        MagnificationAmount = Math.Clamp(MagnificationAmount, MinMagnification, MaxMagnification);
        AnimationSpeed = Math.Clamp(AnimationSpeed, 50, 600);
        Transparency = Math.Clamp(Transparency, 0, 1);
        BlurAmount = Math.Clamp(BlurAmount, 0, 40);
        CornerRadius = Math.Clamp(CornerRadius, 8, 28);
        BorderOpacity = Math.Clamp(BorderOpacity, 0, 1);
        ShadowIntensity = Math.Clamp(ShadowIntensity, 0, 1);
        DockOffset = Math.Clamp(DockOffset, 0, 96);
        AudioVisualizerSensitivity = Math.Clamp(AudioVisualizerSensitivity, 0.2, 1.5);
        if (!IsValidHexColor(AudioLineBaseHex))
        {
            AudioLineBaseHex = "#FFFFFF";
        }

        if (!IsValidHexColor(AudioLineAccentHex))
        {
            AudioLineAccentHex = "#93F7FF";
        }

        if (string.IsNullOrWhiteSpace(DockColorHex) || !DockColorHex.StartsWith('#') || DockColorHex.Length is not (7 or 9))
        {
            DockColorHex = "#0C1219";
        }

        if (string.IsNullOrWhiteSpace(ThemeName))
        {
            ThemeName = "Windows 11";
        }

        if (string.IsNullOrWhiteSpace(DateFormat))
        {
            DateFormat = "ddd, MMM d";
        }

        PinnedApps.RemoveAll(app => string.IsNullOrWhiteSpace(app.TargetPath));
    }

    private static bool IsValidHexColor(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.StartsWith('#')
            && value.Length is 7 or 9;
    }
}
