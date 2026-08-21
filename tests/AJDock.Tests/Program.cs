using AJDock.App.Services;
using AJDock.Core.Models;

var tests = new (string Name, Action Body)[]
{
    ("DockSettings.Normalize clamps values", DockSettingsNormalizeClampsValues),
    ("PinnedApp.NormalizePath is stable", PinnedAppNormalizePathIsStable),
    ("Running app matching uses normalized paths", RunningAppMatchingUsesNormalizedPaths),
    ("Notification badge parsing handles communicator titles", NotificationBadgeParsingHandlesCommunicatorTitles)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void DockSettingsNormalizeClampsValues()
{
    var settings = new DockSettings
    {
        IconSize = 4,
        IconQuality = 2,
        DockSize = 10,
        IconSpacing = 90,
        MagnificationAmount = 6,
        AnimationSpeed = 2,
        Transparency = -1,
        BlurAmount = 99,
        PinnedApps =
        [
            new PinnedApp { DisplayName = "Invalid", TargetPath = "" }
        ]
    };

    settings.Normalize();

    AssertEqual(DockSettings.MinIconSize, settings.IconSize);
    AssertEqual(1d, settings.IconQuality);
    AssertEqual(settings.IconSize + 8, settings.DockSize);
    AssertEqual(DockSettings.MaxSpacing, settings.IconSpacing);
    AssertEqual(DockSettings.MaxMagnification, settings.MagnificationAmount);
    AssertEqual(50, settings.AnimationSpeed);
    AssertEqual(0, settings.Transparency);
    AssertEqual(40, settings.BlurAmount);
    AssertEqual(0, settings.PinnedApps.Count);
}

static void PinnedAppNormalizePathIsStable()
{
    var normalized = PinnedApp.NormalizePath(@"C:\Windows\System32\notepad.exe");
    Assert(normalized.EndsWith(@"WINDOWS\SYSTEM32\NOTEPAD.EXE", StringComparison.Ordinal), normalized);
}

static void RunningAppMatchingUsesNormalizedPaths()
{
    var pinned = new PinnedApp { TargetPath = @"C:\Windows\System32\notepad.exe" };
    var running = new RunningAppInfo { ExecutablePath = @"c:\windows\system32\NOTEPAD.exe" };

    AssertEqual(pinned.NormalizedTargetPath, running.NormalizedExecutablePath);
}

static void NotificationBadgeParsingHandlesCommunicatorTitles()
{
    AssertEqual(3, NotificationBadgeService.ExtractCount("(3) Discord"));
    AssertEqual(12, NotificationBadgeService.ExtractCount("[12] Microsoft Teams"));
    AssertEqual(7, NotificationBadgeService.ExtractCount("Inbox - 7 unread messages - Outlook"));
    AssertEqual(2, NotificationBadgeService.ExtractCount("Microsoft Teams - 2 mentions"));
    AssertEqual(0, NotificationBadgeService.ExtractCount("Microsoft Teams"));
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}
