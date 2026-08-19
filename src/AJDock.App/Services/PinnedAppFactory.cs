using System.IO;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class PinnedAppFactory
{
    private readonly ShortcutResolver _shortcutResolver;

    public PinnedAppFactory(ShortcutResolver shortcutResolver)
    {
        _shortcutResolver = shortcutResolver;
    }

    public PinnedApp? FromDroppedFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var shortcut = _shortcutResolver.Resolve(path);
        var targetPath = shortcut.TargetPath;
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            return null;
        }

        return new PinnedApp
        {
            DisplayName = Path.GetFileNameWithoutExtension(path),
            TargetPath = targetPath,
            Arguments = shortcut.Arguments,
            CustomIconPath = shortcut.IconPath
        };
    }
}
