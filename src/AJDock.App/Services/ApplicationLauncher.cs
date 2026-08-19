using System.Diagnostics;
using System.IO;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class ApplicationLauncher : IApplicationLauncher
{
    public void Launch(PinnedApp app)
    {
        Start(app, verb: null);
    }

    public void RunAsAdministrator(PinnedApp app)
    {
        Start(app, "runas");
    }

    public void OpenFileLocation(PinnedApp app)
    {
        if (string.IsNullOrWhiteSpace(app.TargetPath))
        {
            return;
        }

        if (File.Exists(app.TargetPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{app.TargetPath}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        var directory = Path.GetDirectoryName(app.TargetPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo(directory)
            {
                UseShellExecute = true
            });
        }
    }

    private static void Start(PinnedApp app, string? verb)
    {
        if (string.IsNullOrWhiteSpace(app.TargetPath))
        {
            return;
        }

        var info = new ProcessStartInfo(app.TargetPath)
        {
            UseShellExecute = true,
            WorkingDirectory = TryGetWorkingDirectory(app.TargetPath)
        };

        if (!string.IsNullOrWhiteSpace(app.Arguments))
        {
            info.Arguments = app.Arguments;
        }

        if (!string.IsNullOrWhiteSpace(verb))
        {
            info.Verb = verb;
        }

        Process.Start(info);
    }

    private static string? TryGetWorkingDirectory(string path)
    {
        try
        {
            return File.Exists(path) ? Path.GetDirectoryName(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
