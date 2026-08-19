using System.Text.RegularExpressions;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class NotificationBadgeService
{
    private static readonly Regex CountPattern = new(
        @"(?:^\((?<count>\d{1,3})\))|(?<count>\d{1,3})\s+(?:unread|notification|notifications|message|messages)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string GetBadgeText(IReadOnlyList<RunningAppInfo> runningApps)
    {
        var count = runningApps
            .Select(app => CountPattern.Match(app.DisplayName))
            .Where(match => match.Success)
            .Select(match => int.TryParse(match.Groups["count"].Value, out var value) ? value : 0)
            .Where(value => value > 0)
            .DefaultIfEmpty(0)
            .Sum();

        if (count > 0)
        {
            return count > 99 ? "99+" : count.ToString();
        }

        return string.Empty;
    }
}
