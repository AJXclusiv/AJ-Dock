using System.Windows.Media;
using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class DockItemViewModel : ObservableObject
{
    private ImageSource _icon;
    private bool _isRunning;
    private bool _isPinned;
    private RunningAppInfo? _runningApp;
    private IReadOnlyList<RunningAppInfo> _runningApps = [];
    private int _stackCount = 1;
    private string _notificationBadgeText = string.Empty;

    public DockItemViewModel(PinnedApp app, ImageSource icon, bool isPinned = true)
    {
        App = app;
        _icon = icon;
        _isPinned = isPinned;
    }

    public PinnedApp App { get; }
    public ImageSource Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
    public string DisplayName => App.DisplayName;
    public string TargetPath => App.TargetPath;
    public string PinActionText => IsPinned ? "Unpin from Dock" : "Pin to Dock";
    public bool IsBrowser => ContainsAny("chrome", "msedge", "edge", "firefox", "brave", "opera");
    public bool IsVsCode => ContainsAny("visual studio code", "code.exe", "vscode", "code");
    public bool IsDiscord => ContainsAny("discord");
    public bool IsPlex => ContainsAny("plex", "app.plex.tv");
    public bool IsSpotify => ContainsAny("spotify");
    public bool SupportsMediaControls => IsSpotify || IsPlex;
    public bool HasMiniControls => IsBrowser || IsVsCode || IsDiscord || SupportsMediaControls;

    public bool IsPinned
    {
        get => _isPinned;
        private set
        {
            if (SetProperty(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(PinActionText));
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public RunningAppInfo? RunningApp
    {
        get => _runningApp;
        private set => SetProperty(ref _runningApp, value);
    }

    public IReadOnlyList<RunningAppInfo> RunningApps
    {
        get => _runningApps;
        private set => SetProperty(ref _runningApps, value);
    }

    public int StackCount
    {
        get => _stackCount;
        private set
        {
            if (SetProperty(ref _stackCount, value))
            {
                OnPropertyChanged(nameof(HasStack));
            }
        }
    }

    public bool HasStack => StackCount > 1;

    public string NotificationBadgeText
    {
        get => _notificationBadgeText;
        private set
        {
            if (SetProperty(ref _notificationBadgeText, value))
            {
                OnPropertyChanged(nameof(HasNotificationBadge));
            }
        }
    }

    public bool HasNotificationBadge => !string.IsNullOrWhiteSpace(NotificationBadgeText);

    public void SetPinned(bool isPinned)
    {
        IsPinned = isPinned;
    }

    public void UpdateRunningState(IReadOnlyList<RunningAppInfo> runningApps, int pinnedStackCount, string notificationBadgeText)
    {
        RunningApps = runningApps;
        RunningApp = runningApps.FirstOrDefault();
        IsRunning = RunningApp is not null;
        StackCount = Math.Max(pinnedStackCount, runningApps.Count);
        NotificationBadgeText = notificationBadgeText;
    }

    private bool ContainsAny(params string[] needles)
    {
        var haystack = $"{DisplayName} {TargetPath}".ToLowerInvariant();
        return needles.Any(haystack.Contains);
    }
}
