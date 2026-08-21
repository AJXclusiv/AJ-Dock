using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class NotificationBadgeService
{
    private static readonly TimeSpan OutlookCacheDuration = TimeSpan.FromSeconds(20);
    private static readonly Regex CountPattern = new(
        @"(?:^|\s|[-–—|])(?:\(|\[)?(?<count>\d{1,4})(?:\)|\])?(?=\s*(?:unread|new|notification|notifications|message|messages|mention|mentions|activity|activities)\b)|^(?:\(|\[)(?<leading>\d{1,4})(?:\)|\])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AttentionPattern = new(
        @"\b(?:unread|new message|new notification|mention|activity|missed call)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private DateTimeOffset _outlookCacheExpiresAt;
    private int _outlookUnreadCache;

    public string GetBadgeText(PinnedApp app, IReadOnlyList<RunningAppInfo> runningApps)
    {
        var titles = runningApps
            .Select(runningApp => runningApp.DisplayName)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var count = titles
            .Select(ExtractCount)
            .Where(value => value > 0)
            .DefaultIfEmpty(0)
            .Sum();

        if (IsOutlook(app, runningApps))
        {
            count = Math.Max(count, GetOutlookUnreadCount());
        }

        if (count > 0)
        {
            return count > 99 ? "99+" : count.ToString();
        }

        if (IsNotificationCentricApp(app, runningApps) && titles.Any(title => AttentionPattern.IsMatch(title)))
        {
            return "!";
        }

        return string.Empty;
    }

    public string GetBadgeText(IReadOnlyList<RunningAppInfo> runningApps)
    {
        return GetBadgeText(new PinnedApp(), runningApps);
    }

    public static int ExtractCount(string title)
    {
        var match = CountPattern.Match(title);
        if (!match.Success)
        {
            return 0;
        }

        var value = match.Groups["count"].Success
            ? match.Groups["count"].Value
            : match.Groups["leading"].Value;
        return int.TryParse(value, out var count) ? count : 0;
    }

    private int GetOutlookUnreadCount()
    {
        if (DateTimeOffset.Now < _outlookCacheExpiresAt)
        {
            return _outlookUnreadCache;
        }

        _outlookCacheExpiresAt = DateTimeOffset.Now.Add(OutlookCacheDuration);
        _outlookUnreadCache = TryGetOutlookUnreadCount();
        return _outlookUnreadCache;
    }

    private static bool IsNotificationCentricApp(PinnedApp app, IReadOnlyList<RunningAppInfo> runningApps)
    {
        return ContainsAny(app, runningApps, "discord", "teams", "outlook", "slack", "whatsapp", "telegram", "signal");
    }

    private static bool IsOutlook(PinnedApp app, IReadOnlyList<RunningAppInfo> runningApps)
    {
        return ContainsAny(app, runningApps, "outlook");
    }

    private static bool ContainsAny(PinnedApp app, IReadOnlyList<RunningAppInfo> runningApps, params string[] needles)
    {
        var haystack = string.Join(
            ' ',
            [app.DisplayName, app.TargetPath, .. runningApps.SelectMany(runningApp => new[] { runningApp.DisplayName, runningApp.ExecutablePath })])
            .ToLowerInvariant();
        return needles.Any(haystack.Contains);
    }

    private static int TryGetOutlookUnreadCount()
    {
        object? outlook = null;
        object? session = null;
        object? inbox = null;
        try
        {
            if (CLSIDFromProgID("Outlook.Application", out var clsid) != 0)
            {
                return 0;
            }

            GetActiveObject(ref clsid, nint.Zero, out outlook);
            if (outlook is null)
            {
                return 0;
            }

            session = outlook.GetType().InvokeMember("Session", System.Reflection.BindingFlags.GetProperty, null, outlook, null);
            if (session is null)
            {
                return 0;
            }

            inbox = session.GetType().InvokeMember("GetDefaultFolder", System.Reflection.BindingFlags.InvokeMethod, null, session, [6]);
            var unread = inbox?.GetType().InvokeMember("UnReadItemCount", System.Reflection.BindingFlags.GetProperty, null, inbox, null);
            return unread is int count ? count : 0;
        }
        catch
        {
            return 0;
        }
        finally
        {
            ReleaseComObject(inbox);
            ReleaseComObject(session);
            ReleaseComObject(outlook);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.ReleaseComObject(instance);
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        nint reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object? activeObject);
}
