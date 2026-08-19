using System.IO;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class StartMenuAppService
{
    private readonly ShortcutResolver _shortcutResolver;

    public StartMenuAppService(ShortcutResolver shortcutResolver)
    {
        _shortcutResolver = shortcutResolver;
    }

    public IReadOnlyList<PinnedApp> GetApplications()
    {
        var startMenuRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
        };

        return startMenuRoots
            .Where(Directory.Exists)
            .SelectMany(root => SafeEnumerateFiles(root, "*.lnk"))
            .Select(CreateAppFromShortcut)
            .Where(app => app is not null)
            .Cast<PinnedApp>()
            .GroupBy(app => $"{app.NormalizedTargetPath}|{app.Arguments}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase).First())
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(120)
            .ToList();
    }

    private PinnedApp? CreateAppFromShortcut(string shortcutPath)
    {
        var shortcut = _shortcutResolver.Resolve(shortcutPath);
        if (string.IsNullOrWhiteSpace(shortcut.TargetPath) || !File.Exists(shortcut.TargetPath))
        {
            return null;
        }

        return new PinnedApp
        {
            DisplayName = Path.GetFileNameWithoutExtension(shortcutPath),
            TargetPath = shortcut.TargetPath,
            Arguments = shortcut.Arguments,
            CustomIconPath = shortcut.IconPath
        };
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
        }
        catch
        {
            return [];
        }
    }
}
