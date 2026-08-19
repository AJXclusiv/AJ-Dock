using System.IO;

namespace AJDock.App.Services;

public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "AJDock.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never prevent startup or shutdown.
        }
    }
}
