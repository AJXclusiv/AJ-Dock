namespace AJDock.Core.Models;

public sealed class RunningAppInfo
{
    public int ProcessId { get; init; }
    public nint MainWindowHandle { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;

    public string NormalizedExecutablePath => PinnedApp.NormalizePath(ExecutablePath);
}
