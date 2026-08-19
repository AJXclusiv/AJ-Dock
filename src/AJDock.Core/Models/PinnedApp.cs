namespace AJDock.Core.Models;

public sealed class PinnedApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? CustomIconPath { get; set; }
    public DateTimeOffset PinnedAt { get; set; } = DateTimeOffset.UtcNow;

    public string NormalizedTargetPath => NormalizePath(TargetPath);

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        }
        catch
        {
            return path.Trim().ToUpperInvariant();
        }
    }
}
