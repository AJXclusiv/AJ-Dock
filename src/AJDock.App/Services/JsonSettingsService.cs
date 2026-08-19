using System.IO;
using System.Text.Json;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        SettingsPath = Path.Combine(appData, "AJDock", "settings.json");
    }

    public string SettingsPath { get; }

    public DockSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = CreateDefaultSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<DockSettings>(json, JsonOptions) ?? CreateDefaultSettings();
            if (ApplyVisualMigration(settings))
            {
                Save(settings);
            }

            settings.Normalize();
            return settings;
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(DockSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static DockSettings CreateDefaultSettings()
    {
        var settings = new DockSettings
        {
            VisualProfileVersion = DockSettings.CurrentVisualProfileVersion
        };
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AddIfExists(settings, "Start", Path.Combine(windows, "explorer.exe"), "shell:AppsFolder");
        AddIfExists(settings, "File Explorer", Path.Combine(windows, "explorer.exe"));
        AddIfExists(settings, "Microsoft Edge", Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"));
        AddFirstExisting(settings, "Spotify",
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "Spotify.exe")
        ]);
        AddFirstExisting(settings, "Visual Studio Code",
        [
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")
        ]);
        AddFirstExisting(settings, "Discord",
        [
            TryFindDiscord(localAppData),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "Discord.exe")
        ]);
        AddIfExists(settings, "Steam", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"));
        AddIfExists(settings, "Recycle Bin", Path.Combine(windows, "explorer.exe"), "shell:RecycleBinFolder");
        return settings;
    }

    private static bool ApplyVisualMigration(DockSettings settings)
    {
        if (settings.VisualProfileVersion >= DockSettings.CurrentVisualProfileVersion)
        {
            return false;
        }

        settings.ThemeName = "Transparent";
        settings.Transparency = 0;
        settings.BorderOpacity = 0;
        settings.ShadowIntensity = Math.Min(settings.ShadowIntensity, 0.18);
        if (settings.IconSize < 40)
        {
            settings.IconSize = 40;
        }

        settings.DockSize = Math.Clamp(settings.DockSize, settings.IconSize + 10, settings.IconSize + 14);
        settings.IconSpacing = Math.Clamp(settings.IconSpacing, 8, 14);
        settings.MagnificationAmount = Math.Clamp(settings.MagnificationAmount, 1.45, 1.75);
        settings.AnimationSpeed = Math.Clamp(settings.AnimationSpeed, 80, 140);
        settings.BlurAmount = 0;
        settings.CornerRadius = 0;
        settings.VisualProfileVersion = DockSettings.CurrentVisualProfileVersion;
        return true;
    }

    private static void AddIfExists(DockSettings settings, string displayName, string path, string arguments = "")
    {
        if (!File.Exists(path))
        {
            return;
        }

        settings.PinnedApps.Add(new PinnedApp
        {
            DisplayName = displayName,
            TargetPath = path,
            Arguments = arguments
        });
    }

    private static void AddFirstExisting(DockSettings settings, string displayName, IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (File.Exists(path))
            {
                AddIfExists(settings, displayName, path);
                return;
            }
        }
    }

    private static string TryFindDiscord(string localAppData)
    {
        var discordRoot = Path.Combine(localAppData, "Discord");
        if (!Directory.Exists(discordRoot))
        {
            return string.Empty;
        }

        try
        {
            return Directory.GetFiles(discordRoot, "Discord.exe", SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
