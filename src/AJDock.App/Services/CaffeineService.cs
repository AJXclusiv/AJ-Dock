using System.Diagnostics;
using System.IO;

namespace AJDock.App.Services;

public sealed class CaffeineService
{
    private const string ProcessName = "caffeine";
    private readonly string _appPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "caffeine.exe");

    public bool IsInstalled => File.Exists(_appPath);

    public bool IsRunning()
    {
        return Process.GetProcessesByName(ProcessName).Any(process =>
        {
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
            finally
            {
                process.Dispose();
            }
        });
    }

    public void Toggle()
    {
        if (IsRunning())
        {
            Stop();
            return;
        }

        Start();
    }

    private void Start()
    {
        if (!File.Exists(_appPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_appPath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_appPath) ?? string.Empty
        });
    }

    private static void Stop()
    {
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    if (process.CloseMainWindow())
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: false);
                }
                catch
                {
                    // Caffeine is optional; failures here should never interrupt the dock.
                }
            }
        }
    }
}
