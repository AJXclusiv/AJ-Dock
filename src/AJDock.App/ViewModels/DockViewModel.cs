using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AJDock.App.Native;
using AJDock.App.Services;
using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class DockViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IApplicationLauncher _applicationLauncher;
    private readonly RunningApplicationService _runningApplicationService;
    private readonly WindowActivationService _windowActivationService;
    private readonly PinnedAppFactory _pinnedAppFactory;
    private readonly IconImageService _iconImageService;
    private readonly IIconPickerService _iconPickerService;
    private readonly IStartupService _startupService;
    private readonly TaskbarService _taskbarService;
    private readonly NetworkStatusService _networkStatusService;
    private readonly StartMenuAppService _startMenuAppService;
    private readonly WindowsStartService _windowsStartService;
    private readonly BackgroundApplicationService _backgroundApplicationService;
    private readonly AudioVolumeService _audioVolumeService;
    private readonly WindowPreviewService _windowPreviewService;
    private readonly NotificationBadgeService _notificationBadgeService;
    private readonly SystemMonitorService _systemMonitorService;
    private readonly WeatherService _weatherService;
    private readonly ArtworkLookupService _artworkLookupService;
    private readonly MediaSessionService _mediaSessionService;
    private readonly CaffeineService _caffeineService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _systemStatusTimer;
    private readonly DispatcherTimer _weatherTimer;
    private readonly DispatcherTimer _hiddenTrayAutoCloseTimer;
    private readonly DispatcherTimer _volumeAutoCloseTimer;
    private string _clockText = string.Empty;
    private string _connectionName = "Network";
    private string _networkAdapterName = "No active connection";
    private string _downloadRateText = "0 B/s";
    private string _uploadRateText = "0 B/s";
    private string _calendarMonthText = string.Empty;
    private bool _isSystemPopoverOpen;
    private bool _isWifiPopoverOpen;
    private bool _isHiddenTrayOpen;
    private bool _isVolumePopoverOpen;
    private bool _isPreviewOpen;
    private bool _isStartMenuOpen;
    private bool _isEditMode;
    private double _volumePercent;
    private string _previewTitle = string.Empty;
    private string _cpuText = "CPU --";
    private string _memoryText = "RAM --";
    private string _batteryText = "AC";
    private string _weatherText = "☁ --°";
    private string _londonTimeText = string.Empty;
    private string _mumbaiTimeText = string.Empty;
    private string _wifiSpeedTestText = "Run a speed test";
    private string _wirelessAdapterName = "Wi-Fi";
    private bool _isWirelessEnabled;
    private bool _isCaffeineRunning;
    private bool _isOriginalCaffeineRunning;
    private string _caffeineModeText = "F15 every 59s";
    private string _caffeineTimerText = string.Empty;
    private bool _isSpeedTestRunning;
    private bool _isSpotifyActive;
    private string _spotifyTrackText = "Spotify";
    private string _spotifyArtistText = "Spotify";
    private string _spotifyAlbumText = "Now playing";
    private string _spotifyLyricsText = "Spotify has synced lyrics for many tracks. Open Spotify to view the live lyrics panel.";
    private string _spotifySearchQuery = "Spotify lyrics";
    private ImageSource? _spotifyArtwork;
    private bool _isMediaPlaying;
    private bool _isMediaStateRefreshRunning;
    private bool _isSpotifyMetadataRefreshRunning;
    private string _spotifyArtworkLookupKey = string.Empty;
    private RunningAppInfo? _spotifyWindow;
    private string _startMenuSearchText = string.Empty;
    private List<StartMenuAppViewModel> _allStartMenuApps = [];

    public DockViewModel(
        ISettingsService settingsService,
        IApplicationLauncher applicationLauncher,
        RunningApplicationService runningApplicationService,
        WindowActivationService windowActivationService,
        PinnedAppFactory pinnedAppFactory,
        IconImageService iconImageService,
        IIconPickerService iconPickerService,
        IStartupService startupService,
        TaskbarService taskbarService,
        NetworkStatusService networkStatusService,
        StartMenuAppService startMenuAppService,
        WindowsStartService windowsStartService,
        BackgroundApplicationService backgroundApplicationService,
        AudioVolumeService audioVolumeService,
        WindowPreviewService windowPreviewService,
        NotificationBadgeService notificationBadgeService,
        SystemMonitorService systemMonitorService,
        WeatherService weatherService,
        ArtworkLookupService artworkLookupService,
        MediaSessionService mediaSessionService,
        CaffeineService caffeineService)
    {
        _settingsService = settingsService;
        _applicationLauncher = applicationLauncher;
        _runningApplicationService = runningApplicationService;
        _windowActivationService = windowActivationService;
        _pinnedAppFactory = pinnedAppFactory;
        _iconImageService = iconImageService;
        _iconPickerService = iconPickerService;
        _startupService = startupService;
        _taskbarService = taskbarService;
        _networkStatusService = networkStatusService;
        _startMenuAppService = startMenuAppService;
        _windowsStartService = windowsStartService;
        _backgroundApplicationService = backgroundApplicationService;
        _audioVolumeService = audioVolumeService;
        _windowPreviewService = windowPreviewService;
        _notificationBadgeService = notificationBadgeService;
        _systemMonitorService = systemMonitorService;
        _weatherService = weatherService;
        _artworkLookupService = artworkLookupService;
        _mediaSessionService = mediaSessionService;
        _caffeineService = caffeineService;

        Settings = _settingsService.Load();
        Settings.StartWithWindows = _startupService.IsEnabled();
        _iconImageService.SetIconQuality(Settings.IconQuality);

        Items = new ObservableCollection<DockItemViewModel>(
            Settings.PinnedApps
                .GroupBy(StackKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var representative = group.OrderBy(app => string.IsNullOrWhiteSpace(app.Arguments) ? 0 : 1).First();
                    return new DockItemViewModel(representative, _iconImageService.GetIcon(representative), isPinned: true);
                }));

        OpenItemCommand = new RelayCommand(OpenItem, parameter => parameter is DockItemViewModel);
        RunAsAdminCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => _applicationLauncher.RunAsAdministrator(item.App)));
        OpenFileLocationCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => _applicationLauncher.OpenFileLocation(item.App)));
        ChooseCustomIconCommand = new RelayCommand(parameter => ExecuteForItem(parameter, ChooseCustomIcon));
        TogglePinCommand = new RelayCommand(parameter => ExecuteForItem(parameter, TogglePin));
        CloseApplicationCommand = new RelayCommand(parameter => ExecuteForItem(parameter, CloseApplication), parameter => parameter is DockItemViewModel item && item.IsRunning);
        OpenSettingsCommand = new RelayCommand(_ => RequestOpenSettings?.Invoke(this, EventArgs.Empty));
        ToggleSystemPopoverCommand = new RelayCommand(_ => ToggleSystemPopover());
        ToggleWifiPopoverCommand = new RelayCommand(_ => ToggleWifiPopover());
        RunWifiSpeedTestCommand = new RelayCommand(async _ => await RunWifiSpeedTestAsync(), _ => !IsSpeedTestRunning);
        OpenWifiSettingsCommand = new RelayCommand(_ => OpenWifiSettings());
        OpenAvailableNetworksCommand = new RelayCommand(_ => OpenAvailableNetworks());
        ToggleWifiPowerCommand = new RelayCommand(_ => ToggleWifiPower());
        ConnectWifiNetworkCommand = new RelayCommand(ConnectWifiNetwork, parameter => parameter is WifiNetworkViewModel);
        OpenSoundSettingsCommand = new RelayCommand(_ => OpenSoundSettings());
        ToggleVolumePopoverCommand = new RelayCommand(_ => ToggleVolumePopover());
        ToggleCaffeineCommand = new RelayCommand(_ => ToggleCaffeine());
        SetCaffeineActiveCommand = new RelayCommand(SetCaffeineActive);
        SetCaffeineActiveForCommand = new RelayCommand(parameter => SetCaffeineActiveFor(parameter));
        SetCaffeineInactiveForCommand = new RelayCommand(parameter => SetCaffeineInactiveFor(parameter));
        SetCaffeineExitAfterCommand = new RelayCommand(parameter => SetCaffeineExitAfter(parameter));
        SetCaffeineMethodCommand = new RelayCommand(parameter => SetCaffeineMethod(parameter));
        SetCaffeineIntervalCommand = new RelayCommand(parameter => SetCaffeineInterval(parameter));
        ClearCaffeineTimersCommand = new RelayCommand(_ => ClearCaffeineTimers());
        LaunchOriginalCaffeineCommand = new RelayCommand(_ => LaunchOriginalCaffeine());
        StopOriginalCaffeineCommand = new RelayCommand(_ => StopOriginalCaffeine());
        ToggleHiddenTrayCommand = new RelayCommand(_ => ToggleHiddenTray());
        OpenHiddenTrayItemCommand = new RelayCommand(OpenHiddenTrayItem, parameter => parameter is HiddenTrayItemViewModel);
        ToggleStartMenuCommand = new RelayCommand(_ => ToggleStartMenu());
        OpenWindowsStartCommand = new RelayCommand(_ => OpenWindowsStart());
        OpenStartMenuAppCommand = new RelayCommand(OpenStartMenuApp, parameter => parameter is StartMenuAppViewModel);
        OpenWindowsAppsCommand = new RelayCommand(_ => OpenWindowsApps());
        ActivatePreviewWindowCommand = new RelayCommand(ActivatePreviewWindow, parameter => parameter is WindowPreviewItemViewModel);
        ClosePreviewWindowCommand = new RelayCommand(ClosePreviewWindow, parameter => parameter is WindowPreviewItemViewModel);
        MinimizePreviewWindowCommand = new RelayCommand(MinimizePreviewWindow, parameter => parameter is WindowPreviewItemViewModel);
        RestorePreviewWindowCommand = new RelayCommand(RestorePreviewWindow, parameter => parameter is WindowPreviewItemViewModel);
        SnapPreviewLeftCommand = new RelayCommand(parameter => SnapPreviewWindow(parameter, DockSnapSide.Left), parameter => parameter is WindowPreviewItemViewModel);
        SnapPreviewRightCommand = new RelayCommand(parameter => SnapPreviewWindow(parameter, DockSnapSide.Right), parameter => parameter is WindowPreviewItemViewModel);
        PinPreviewWindowCommand = new RelayCommand(PinPreviewWindow, parameter => parameter is WindowPreviewItemViewModel);
        ToggleEditModeCommand = new RelayCommand(_ => ToggleEditMode());
        RemoveDockItemCommand = new RelayCommand(parameter => ExecuteForItem(parameter, RemoveDockItem));
        SpotifyPreviousCommand = new RelayCommand(_ => SendMediaKey(NativeMethods.VkMediaPreviousTrack));
        SpotifyPlayPauseCommand = new RelayCommand(_ => ToggleMediaPlayback());
        SpotifyNextCommand = new RelayCommand(_ => SendMediaKey(NativeMethods.VkMediaNextTrack));
        OpenSpotifyLyricsCommand = new RelayCommand(_ => { });
        OpenBrowserWindowCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => LaunchWithArguments(item, "--new-window")), parameter => parameter is DockItemViewModel item && item.IsBrowser);
        OpenBrowserPrivateCommand = new RelayCommand(parameter => ExecuteForItem(parameter, OpenBrowserPrivate), parameter => parameter is DockItemViewModel item && item.IsBrowser);
        OpenVsCodeWindowCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => LaunchWithArguments(item, "-n")), parameter => parameter is DockItemViewModel item && item.IsVsCode);
        DiscordMuteCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => SendAppHotkey(item, 0x4D)), parameter => parameter is DockItemViewModel item && item.IsDiscord);
        DiscordDeafenCommand = new RelayCommand(parameter => ExecuteForItem(parameter, item => SendAppHotkey(item, 0x44)), parameter => parameter is DockItemViewModel item && item.IsDiscord);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _refreshTimer.Tick += (_, _) => RefreshRunningApplications();
        _refreshTimer.Start();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => RefreshClock();
        _clockTimer.Start();

        _systemStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _systemStatusTimer.Tick += (_, _) => RefreshSystemStatus();
        _systemStatusTimer.Start();

        _weatherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10)
        };
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _weatherTimer.Start();

        _hiddenTrayAutoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _hiddenTrayAutoCloseTimer.Tick += (_, _) =>
        {
            _hiddenTrayAutoCloseTimer.Stop();
            IsHiddenTrayOpen = false;
        };

        _volumeAutoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _volumeAutoCloseTimer.Tick += (_, _) =>
        {
            _volumeAutoCloseTimer.Stop();
            IsVolumePopoverOpen = false;
        };

        CalendarDays = new ObservableCollection<CalendarDayViewModel>();
        ApplySettings();
        RefreshRunningApplications();
        RefreshClock();
        RefreshCalendar();
        RefreshSystemStatus();
        RefreshVolume();
        _ = RefreshWeatherAsync();
    }

    public event EventHandler? RequestLayoutUpdate;
    public event EventHandler? RequestOpenSettings;

    public DockSettings Settings { get; }
    public ObservableCollection<DockItemViewModel> Items { get; }
    public RelayCommand OpenItemCommand { get; }
    public RelayCommand RunAsAdminCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand ChooseCustomIconCommand { get; }
    public RelayCommand TogglePinCommand { get; }
    public RelayCommand CloseApplicationCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ToggleSystemPopoverCommand { get; }
    public RelayCommand ToggleWifiPopoverCommand { get; }
    public RelayCommand RunWifiSpeedTestCommand { get; }
    public RelayCommand OpenWifiSettingsCommand { get; }
    public RelayCommand OpenAvailableNetworksCommand { get; }
    public RelayCommand ToggleWifiPowerCommand { get; }
    public RelayCommand ConnectWifiNetworkCommand { get; }
    public RelayCommand OpenSoundSettingsCommand { get; }
    public RelayCommand ToggleVolumePopoverCommand { get; }
    public RelayCommand ToggleCaffeineCommand { get; }
    public RelayCommand SetCaffeineActiveCommand { get; }
    public RelayCommand SetCaffeineActiveForCommand { get; }
    public RelayCommand SetCaffeineInactiveForCommand { get; }
    public RelayCommand SetCaffeineExitAfterCommand { get; }
    public RelayCommand SetCaffeineMethodCommand { get; }
    public RelayCommand SetCaffeineIntervalCommand { get; }
    public RelayCommand ClearCaffeineTimersCommand { get; }
    public RelayCommand LaunchOriginalCaffeineCommand { get; }
    public RelayCommand StopOriginalCaffeineCommand { get; }
    public RelayCommand ToggleHiddenTrayCommand { get; }
    public RelayCommand OpenHiddenTrayItemCommand { get; }
    public RelayCommand ToggleStartMenuCommand { get; }
    public RelayCommand OpenWindowsStartCommand { get; }
    public RelayCommand OpenStartMenuAppCommand { get; }
    public RelayCommand OpenWindowsAppsCommand { get; }
    public RelayCommand ActivatePreviewWindowCommand { get; }
    public RelayCommand ClosePreviewWindowCommand { get; }
    public RelayCommand MinimizePreviewWindowCommand { get; }
    public RelayCommand RestorePreviewWindowCommand { get; }
    public RelayCommand SnapPreviewLeftCommand { get; }
    public RelayCommand SnapPreviewRightCommand { get; }
    public RelayCommand PinPreviewWindowCommand { get; }
    public RelayCommand ToggleEditModeCommand { get; }
    public RelayCommand RemoveDockItemCommand { get; }
    public RelayCommand SpotifyPreviousCommand { get; }
    public RelayCommand SpotifyPlayPauseCommand { get; }
    public RelayCommand SpotifyNextCommand { get; }
    public RelayCommand OpenSpotifyLyricsCommand { get; }
    public RelayCommand OpenBrowserWindowCommand { get; }
    public RelayCommand OpenBrowserPrivateCommand { get; }
    public RelayCommand OpenVsCodeWindowCommand { get; }
    public RelayCommand DiscordMuteCommand { get; }
    public RelayCommand DiscordDeafenCommand { get; }
    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; }
    public ObservableCollection<HiddenTrayItemViewModel> HiddenTrayItems { get; } = [];
    public ObservableCollection<WifiNetworkViewModel> AvailableWifiNetworks { get; } = [];
    public ObservableCollection<AudioSessionViewModel> AudioSessions { get; } = [];
    public ObservableCollection<StartMenuAppViewModel> StartMenuApps { get; } = [];
    public ObservableCollection<WindowPreviewItemViewModel> WindowPreviews { get; } = [];

    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public Visibility ClockVisibility => Settings.ClockDisplayMode == ClockDisplayMode.Hidden
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string ConnectionName
    {
        get => _connectionName;
        private set => SetProperty(ref _connectionName, value);
    }

    public string NetworkAdapterName
    {
        get => _networkAdapterName;
        private set => SetProperty(ref _networkAdapterName, value);
    }

    public string DownloadRateText
    {
        get => _downloadRateText;
        private set => SetProperty(ref _downloadRateText, value);
    }

    public string UploadRateText
    {
        get => _uploadRateText;
        private set => SetProperty(ref _uploadRateText, value);
    }

    public string WirelessAdapterName
    {
        get => _wirelessAdapterName;
        private set => SetProperty(ref _wirelessAdapterName, value);
    }

    public bool IsWirelessEnabled
    {
        get => _isWirelessEnabled;
        private set
        {
            if (SetProperty(ref _isWirelessEnabled, value))
            {
                OnPropertyChanged(nameof(WifiPowerActionText));
            }
        }
    }

    public string WifiPowerActionText => IsWirelessEnabled ? "Turn Wi-Fi Off" : "Turn Wi-Fi On";

    public string WifiSpeedTestText
    {
        get => _wifiSpeedTestText;
        private set => SetProperty(ref _wifiSpeedTestText, value);
    }

    public bool IsCaffeineRunning
    {
        get => _isCaffeineRunning;
        private set
        {
            if (SetProperty(ref _isCaffeineRunning, value))
            {
                OnPropertyChanged(nameof(CaffeineStatusText));
                OnPropertyChanged(nameof(IsCaffeineKeepingAwake));
            }
        }
    }

    public bool IsOriginalCaffeineRunning
    {
        get => _isOriginalCaffeineRunning;
        private set
        {
            if (SetProperty(ref _isOriginalCaffeineRunning, value))
            {
                OnPropertyChanged(nameof(CaffeineStatusText));
                OnPropertyChanged(nameof(IsCaffeineKeepingAwake));
            }
        }
    }

    public bool IsCaffeineKeepingAwake => IsCaffeineRunning || IsOriginalCaffeineRunning;

    public string CaffeineModeText
    {
        get => _caffeineModeText;
        private set => SetProperty(ref _caffeineModeText, value);
    }

    public string CaffeineTimerText
    {
        get => _caffeineTimerText;
        private set
        {
            if (SetProperty(ref _caffeineTimerText, value))
            {
                OnPropertyChanged(nameof(CaffeineStatusText));
            }
        }
    }

    public string CaffeineStatusText
    {
        get
        {
            if (IsCaffeineRunning)
            {
                return string.IsNullOrWhiteSpace(CaffeineTimerText)
                    ? $"AJ Caffeine active - {CaffeineModeText}"
                    : $"AJ Caffeine active - {CaffeineTimerText}";
            }

            if (IsOriginalCaffeineRunning)
            {
                return "Original Caffeine is running";
            }

            return "Start AJ Caffeine";
        }
    }

    public bool IsSpotifyActive
    {
        get => _isSpotifyActive;
        private set => SetProperty(ref _isSpotifyActive, value);
    }

    public string SpotifyTrackText
    {
        get => _spotifyTrackText;
        private set => SetProperty(ref _spotifyTrackText, value);
    }

    public string SpotifyArtistText
    {
        get => _spotifyArtistText;
        private set => SetProperty(ref _spotifyArtistText, value);
    }

    public string SpotifyAlbumText
    {
        get => _spotifyAlbumText;
        private set => SetProperty(ref _spotifyAlbumText, value);
    }

    public string SpotifyLyricsText
    {
        get => _spotifyLyricsText;
        private set => SetProperty(ref _spotifyLyricsText, value);
    }

    public ImageSource? SpotifyArtwork
    {
        get => _spotifyArtwork;
        private set => SetProperty(ref _spotifyArtwork, value);
    }

    public bool IsMediaPlaying
    {
        get => _isMediaPlaying;
        private set
        {
            if (SetProperty(ref _isMediaPlaying, value))
            {
                OnPropertyChanged(nameof(MediaPlayPauseGlyph));
                OnPropertyChanged(nameof(MediaPlayPauseToolTip));
            }
        }
    }

    public string MediaPlayPauseGlyph => IsMediaPlaying ? "\uE769" : "\uE768";

    public string MediaPlayPauseToolTip => IsMediaPlaying ? "Pause" : "Play";

    public bool IsSpeedTestRunning
    {
        get => _isSpeedTestRunning;
        private set
        {
            if (SetProperty(ref _isSpeedTestRunning, value))
            {
                OnPropertyChanged(nameof(SpeedTestButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SpeedTestButtonText => IsSpeedTestRunning ? "Testing..." : "Speed Test";

    public string CalendarMonthText
    {
        get => _calendarMonthText;
        private set => SetProperty(ref _calendarMonthText, value);
    }

    public string LondonTimeText
    {
        get => _londonTimeText;
        private set => SetProperty(ref _londonTimeText, value);
    }

    public string MumbaiTimeText
    {
        get => _mumbaiTimeText;
        private set => SetProperty(ref _mumbaiTimeText, value);
    }

    public string CpuText
    {
        get => _cpuText;
        private set => SetProperty(ref _cpuText, value);
    }

    public string MemoryText
    {
        get => _memoryText;
        private set => SetProperty(ref _memoryText, value);
    }

    public string BatteryText
    {
        get => _batteryText;
        private set => SetProperty(ref _batteryText, value);
    }

    public string WeatherText
    {
        get => _weatherText;
        private set => SetProperty(ref _weatherText, value);
    }

    public bool IsSystemPopoverOpen
    {
        get => _isSystemPopoverOpen;
        set => SetProperty(ref _isSystemPopoverOpen, value);
    }

    public bool IsWifiPopoverOpen
    {
        get => _isWifiPopoverOpen;
        set => SetProperty(ref _isWifiPopoverOpen, value);
    }

    public bool IsHiddenTrayOpen
    {
        get => _isHiddenTrayOpen;
        set
        {
            if (SetProperty(ref _isHiddenTrayOpen, value))
            {
                OnPropertyChanged(nameof(HiddenTrayToggleText));
                ResetAutoCloseTimer(_hiddenTrayAutoCloseTimer, value);
            }
        }
    }

    public string HiddenTrayToggleText => IsHiddenTrayOpen ? ">" : "^";

    public bool IsVolumePopoverOpen
    {
        get => _isVolumePopoverOpen;
        set
        {
            if (SetProperty(ref _isVolumePopoverOpen, value))
            {
                ResetAutoCloseTimer(_volumeAutoCloseTimer, value);
            }
        }
    }

    public bool IsPreviewOpen
    {
        get => _isPreviewOpen;
        set => SetProperty(ref _isPreviewOpen, value);
    }

    public string PreviewTitle
    {
        get => _previewTitle;
        private set => SetProperty(ref _previewTitle, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(EditModeText));
                IsPreviewOpen = false;
            }
        }
    }

    public string EditModeText => IsEditMode ? "Finish Editing" : "Edit Dock";

    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (Math.Abs(_volumePercent - normalized) < 0.1)
            {
                return;
            }

            SetProperty(ref _volumePercent, normalized);
            OnPropertyChanged(nameof(VolumePercentText));
            OnPropertyChanged(nameof(VolumeDialAngle));
            _audioVolumeService.SetVolumePercent(normalized);
            ResetAutoCloseTimer(_volumeAutoCloseTimer, IsVolumePopoverOpen);
        }
    }

    public string VolumePercentText => $"{VolumePercent:F0}%";
    public double VolumeDialAngle => -135 + (VolumePercent * 2.7);

    public double AudioVisualizerSensitivity
    {
        get => Settings.AudioVisualizerSensitivity;
        set
        {
            var normalized = Math.Clamp(value, 0.2, 1.5);
            if (Math.Abs(Settings.AudioVisualizerSensitivity - normalized) < 0.01)
            {
                return;
            }

            Settings.AudioVisualizerSensitivity = normalized;
            Settings.Normalize();
            _settingsService.Save(Settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioVisualizerSensitivityText));
            ResetAutoCloseTimer(_volumeAutoCloseTimer, IsVolumePopoverOpen);
        }
    }

    public string AudioVisualizerSensitivityText => $"{AudioVisualizerSensitivity:P0}";

    public bool IsStartMenuOpen
    {
        get => _isStartMenuOpen;
        set => SetProperty(ref _isStartMenuOpen, value);
    }

    public string StartMenuSearchText
    {
        get => _startMenuSearchText;
        set
        {
            if (SetProperty(ref _startMenuSearchText, value))
            {
                RefreshStartMenuFilter();
            }
        }
    }

    public void PinDroppedFile(string filePath)
    {
        var app = _pinnedAppFactory.FromDroppedFile(filePath);
        if (app is null)
        {
            return;
        }

        if (Settings.PinnedApps.Any(pinned => StackKey(pinned) == StackKey(app)))
        {
            return;
        }

        Settings.PinnedApps.Add(app);
        Items.Add(new DockItemViewModel(app, _iconImageService.GetIcon(app), isPinned: true));
        SaveAndApply();
        RefreshRunningApplications();
    }

    public void SaveAndApply()
    {
        _settingsService.Save(Settings);
        ApplySettings();
        RefreshClock();
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(ClockVisibility));
        RequestLayoutUpdate?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshRunningApplications()
    {
        var running = _runningApplicationService.GetRunningApplications()
            .GroupBy(app => app.NormalizedExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RunningAppInfo>)group.ToList(), StringComparer.OrdinalIgnoreCase);
        RefreshSpotifyStatus(running.Values.SelectMany(apps => apps));
        _ = RefreshMediaPlaybackStateAsync();

        var pinnedCounts = Settings.PinnedApps
            .GroupBy(StackKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in running)
        {
            if (string.IsNullOrWhiteSpace(group.Key) || Items.Any(item => string.Equals(StackKey(item.App), group.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var representative = group.Value.First();
            var app = new PinnedApp
            {
                DisplayName = CleanRunningAppName(representative),
                TargetPath = representative.ExecutablePath,
                Arguments = string.Empty
            };
            Items.Add(new DockItemViewModel(app, _iconImageService.GetIcon(app), isPinned: false));
        }

        foreach (var item in Items.ToList())
        {
            var key = StackKey(item.App);
            running.TryGetValue(key, out var runningApps);
            var isPinned = pinnedCounts.TryGetValue(key, out var pinnedCount);

            if (!isPinned && runningApps is null)
            {
                Items.Remove(item);
                continue;
            }

            item.SetPinned(isPinned);
            var appWindows = runningApps ?? [];
            var badgeText = _notificationBadgeService.GetBadgeText(appWindows);
            item.UpdateRunningState(appWindows, isPinned ? Math.Max(1, pinnedCount) : 1, badgeText);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public void ShowWindowPreview(DockItemViewModel item)
    {
        WindowPreviews.Clear();
        if (!item.IsRunning || item.RunningApps.Count == 0)
        {
            IsPreviewOpen = false;
            return;
        }

        PreviewTitle = item.DisplayName;
        foreach (var app in item.RunningApps.Take(4))
        {
            WindowPreviews.Add(_windowPreviewService.CreatePreview(app, item.Icon));
        }

        IsPreviewOpen = WindowPreviews.Count > 0;
    }

    public void HideWindowPreview()
    {
        IsPreviewOpen = false;
    }

    public double GetAudioPeakPercent()
    {
        return _audioVolumeService.GetOutputPeakPercent();
    }

    public void MoveDockItem(DockItemViewModel source, DockItemViewModel target)
    {
        if (source == target)
        {
            return;
        }

        var oldIndex = Items.IndexOf(source);
        var newIndex = Items.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Items.Move(oldIndex, newIndex);
        PersistPinnedOrder();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _clockTimer.Stop();
        _systemStatusTimer.Stop();
        _weatherTimer.Stop();
        _hiddenTrayAutoCloseTimer.Stop();
        _volumeAutoCloseTimer.Stop();
        _weatherService.Dispose();
        _artworkLookupService.Dispose();
        _caffeineService.Dispose();
        _taskbarService.RestoreIfNeeded();
    }

    private void OpenItem(object? parameter)
    {
        ExecuteForItem(parameter, item =>
        {
            if (IsEditMode)
            {
                RemoveDockItem(item);
                return;
            }

            if (item.RunningApp is not null)
            {
                _windowActivationService.ActivateOrMinimize(item.RunningApp);
            }
            else
            {
                _applicationLauncher.Launch(item.App);
            }
        });
    }

    private void ToggleEditMode()
    {
        IsStartMenuOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsHiddenTrayOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        IsEditMode = !IsEditMode;
    }

    private void ToggleStartMenu()
    {
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsHiddenTrayOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        IsStartMenuOpen = !IsStartMenuOpen;
        if (IsStartMenuOpen)
        {
            EnsureStartMenuAppsLoaded();
            RefreshStartMenuFilter();
        }
    }

    private void OpenWindowsStart()
    {
        IsStartMenuOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsHiddenTrayOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        _windowsStartService.ToggleStartMenu();
    }

    private void ToggleSystemPopover()
    {
        IsStartMenuOpen = false;
        IsHiddenTrayOpen = false;
        IsWifiPopoverOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        IsSystemPopoverOpen = !IsSystemPopoverOpen;
    }

    private void ToggleWifiPopover()
    {
        IsStartMenuOpen = false;
        IsHiddenTrayOpen = false;
        IsSystemPopoverOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        RefreshSystemStatus();
        IsWifiPopoverOpen = !IsWifiPopoverOpen;
        if (IsWifiPopoverOpen)
        {
            _ = RefreshAvailableWifiNetworksAsync();
        }
    }

    private void OpenSoundSettings()
    {
        IsStartMenuOpen = false;
        IsHiddenTrayOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;

        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Windows settings URI failed; leave the dock running quietly.
        }
    }

    private void ToggleCaffeine()
    {
        IsStartMenuOpen = false;
        IsHiddenTrayOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        _caffeineService.Toggle();
        RefreshCaffeineStatus();
    }

    private void SetCaffeineActive(object? parameter)
    {
        var isActive = !bool.TryParse(parameter?.ToString(), out var parsed) || parsed;
        _caffeineService.SetActive(isActive);
        RefreshCaffeineStatus();
    }

    private void SetCaffeineActiveFor(object? parameter)
    {
        if (TryGetMinutes(parameter, out var minutes))
        {
            _caffeineService.ActiveFor(TimeSpan.FromMinutes(minutes));
            RefreshCaffeineStatus();
        }
    }

    private void SetCaffeineInactiveFor(object? parameter)
    {
        if (TryGetMinutes(parameter, out var minutes))
        {
            _caffeineService.InactiveFor(TimeSpan.FromMinutes(minutes));
            RefreshCaffeineStatus();
        }
    }

    private void SetCaffeineExitAfter(object? parameter)
    {
        if (TryGetMinutes(parameter, out var minutes))
        {
            _caffeineService.ExitAfter(TimeSpan.FromMinutes(minutes));
            RefreshCaffeineStatus();
        }
    }

    private void SetCaffeineMethod(object? parameter)
    {
        if (parameter is CaffeineKeepAwakeMethod method)
        {
            _caffeineService.SetMethod(method);
        }
        else if (parameter is string name && Enum.TryParse<CaffeineKeepAwakeMethod>(name, out var parsed))
        {
            _caffeineService.SetMethod(parsed);
        }

        RefreshCaffeineStatus();
    }

    private void SetCaffeineInterval(object? parameter)
    {
        if (TryGetMinutesOrSeconds(parameter, out var seconds))
        {
            _caffeineService.SetInterval(seconds);
            RefreshCaffeineStatus();
        }
    }

    private void ClearCaffeineTimers()
    {
        _caffeineService.ClearTimers();
        RefreshCaffeineStatus();
    }

    private void LaunchOriginalCaffeine()
    {
        _caffeineService.LaunchOriginal();
        RefreshCaffeineStatus();
    }

    private void StopOriginalCaffeine()
    {
        _caffeineService.StopOriginal();
        RefreshCaffeineStatus();
    }

    private async Task RunWifiSpeedTestAsync()
    {
        IsSpeedTestRunning = true;
        WifiSpeedTestText = "Testing download speed...";
        try
        {
            WifiSpeedTestText = await _networkStatusService.RunDownloadSpeedTestAsync();
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    private void OpenWifiSettings()
    {
        OpenWindowsUri("ms-settings:network-wifi");
    }

    private void OpenAvailableNetworks()
    {
        if (!OpenWindowsUri("ms-availablenetworks:"))
        {
            OpenWifiSettings();
        }
    }

    private void ToggleWifiPower()
    {
        var adapter = string.IsNullOrWhiteSpace(WirelessAdapterName)
            ? "Wi-Fi"
            : WirelessAdapterName;
        var state = IsWirelessEnabled ? "disabled" : "enabled";

        try
        {
            Process.Start(new ProcessStartInfo("netsh", $"interface set interface name=\"{adapter}\" admin={state}")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch
        {
            OpenWifiSettings();
        }
    }

    private static bool OpenWindowsUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ToggleVolumePopover()
    {
        IsStartMenuOpen = false;
        IsHiddenTrayOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsPreviewOpen = false;
        RefreshVolume();
        RefreshAudioSessions();
        IsVolumePopoverOpen = !IsVolumePopoverOpen;
    }

    private void ToggleHiddenTray()
    {
        IsStartMenuOpen = false;
        IsSystemPopoverOpen = false;
        IsWifiPopoverOpen = false;
        IsVolumePopoverOpen = false;
        IsPreviewOpen = false;
        IsHiddenTrayOpen = !IsHiddenTrayOpen;
    }

    private void ActivatePreviewWindow(object? parameter)
    {
        if (parameter is not WindowPreviewItemViewModel preview)
        {
            return;
        }

        _windowActivationService.ActivateOrMinimize(preview.App);
        IsPreviewOpen = false;
    }

    private void ClosePreviewWindow(object? parameter)
    {
        if (parameter is not WindowPreviewItemViewModel preview)
        {
            return;
        }

        _runningApplicationService.Close(preview.App);
        IsPreviewOpen = false;
        RefreshRunningApplications();
    }

    private void MinimizePreviewWindow(object? parameter)
    {
        if (parameter is WindowPreviewItemViewModel preview)
        {
            NativeMethods.ShowWindowAsync(preview.App.MainWindowHandle, NativeMethods.SwMinimize);
            IsPreviewOpen = false;
        }
    }

    private void RestorePreviewWindow(object? parameter)
    {
        if (parameter is WindowPreviewItemViewModel preview)
        {
            NativeMethods.ShowWindowAsync(preview.App.MainWindowHandle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(preview.App.MainWindowHandle);
            IsPreviewOpen = false;
        }
    }

    private void SnapPreviewWindow(object? parameter, DockSnapSide side)
    {
        if (parameter is not WindowPreviewItemViewModel preview)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        var width = (int)Math.Round(work.Width / 2);
        var height = (int)Math.Round(work.Height);
        var x = side == DockSnapSide.Left ? (int)Math.Round(work.Left) : (int)Math.Round(work.Left + width);
        var y = (int)Math.Round(work.Top);
        NativeMethods.ShowWindowAsync(preview.App.MainWindowHandle, NativeMethods.SwRestore);
        NativeMethods.SetWindowPos(preview.App.MainWindowHandle, nint.Zero, x, y, width, height, NativeMethods.SwpNoZOrder);
        NativeMethods.SetForegroundWindow(preview.App.MainWindowHandle);
        IsPreviewOpen = false;
    }

    private void PinPreviewWindow(object? parameter)
    {
        if (parameter is not WindowPreviewItemViewModel preview)
        {
            return;
        }

        if (Settings.PinnedApps.Any(app => string.Equals(StackKey(app), preview.App.NormalizedExecutablePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var app = new PinnedApp
        {
            DisplayName = CleanRunningAppName(preview.App),
            TargetPath = preview.App.ExecutablePath,
            Arguments = string.Empty,
            PinnedAt = DateTimeOffset.UtcNow
        };
        Settings.PinnedApps.Add(app);
        SaveAndApply();
        RefreshRunningApplications();
    }

    private void OpenHiddenTrayItem(object? parameter)
    {
        if (parameter is not HiddenTrayItemViewModel item)
        {
            return;
        }

        _applicationLauncher.Launch(item.App);
        IsHiddenTrayOpen = false;
    }

    private void ConnectWifiNetwork(object? parameter)
    {
        if (parameter is not WifiNetworkViewModel network)
        {
            return;
        }

        _networkStatusService.ConnectWifiNetwork(network.Ssid);
        IsWifiPopoverOpen = false;
    }

    private void OpenStartMenuApp(object? parameter)
    {
        if (parameter is not StartMenuAppViewModel item)
        {
            return;
        }

        _applicationLauncher.Launch(item.App);
        IsStartMenuOpen = false;
    }

    private void OpenWindowsApps()
    {
        _applicationLauncher.Launch(new PinnedApp
        {
            DisplayName = "Windows Apps",
            TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
            Arguments = "shell:AppsFolder"
        });
        IsStartMenuOpen = false;
    }

    private void TogglePin(DockItemViewModel item)
    {
        if (item.IsPinned)
        {
            RemoveDockItem(item);
            return;
        }

        item.App.PinnedAt = DateTimeOffset.UtcNow;
        Settings.PinnedApps.Add(item.App);
        item.SetPinned(true);
        SaveAndApply();
        RefreshRunningApplications();
    }

    private void RemoveDockItem(DockItemViewModel item)
    {
        if (!item.IsPinned)
        {
            if (!item.IsRunning)
            {
                Items.Remove(item);
            }

            return;
        }

        Settings.PinnedApps.RemoveAll(app => string.Equals(StackKey(app), StackKey(item.App), StringComparison.OrdinalIgnoreCase));
        item.SetPinned(false);
        if (!item.IsRunning)
        {
            Items.Remove(item);
        }

        SaveAndApply();
        RefreshRunningApplications();
    }

    private void CloseApplication(DockItemViewModel item)
    {
        if (item.RunningApp is not null)
        {
            _runningApplicationService.Close(item.RunningApp);
        }
    }

    private void ChooseCustomIcon(DockItemViewModel item)
    {
        var path = _iconPickerService.PickIconPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        item.App.CustomIconPath = path;
        item.Icon = _iconImageService.GetIcon(item.App);
        SaveAndApply();
    }

    private void ApplySettings()
    {
        Settings.Normalize();
        var iconQualityChanged = _iconImageService.SetIconQuality(Settings.IconQuality);
        _startupService.SetEnabled(Settings.StartWithWindows);
        _taskbarService.SetHidden(Settings.HideWindowsTaskbar);
        if (iconQualityChanged)
        {
            RefreshIconImages();
        }
    }

    private void RefreshIconImages()
    {
        foreach (var item in Items)
        {
            item.Icon = _iconImageService.GetIcon(item.App);
        }

        foreach (var app in _allStartMenuApps)
        {
            app.Icon = _iconImageService.GetCompactIcon(app.App);
        }

        foreach (var item in HiddenTrayItems)
        {
            item.Icon = _iconImageService.GetCompactIcon(item.App);
        }
    }

    private void EnsureStartMenuAppsLoaded()
    {
        if (_allStartMenuApps.Count == 0)
        {
            LoadStartMenuApps();
        }
    }

    private void LoadStartMenuApps()
    {
        _allStartMenuApps = _startMenuAppService.GetApplications()
            .Select(app => new StartMenuAppViewModel(app, _iconImageService.GetCompactIcon(app)))
            .ToList();
        RefreshStartMenuFilter();
    }

    private void RefreshStartMenuFilter()
    {
        var query = StartMenuSearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allStartMenuApps.Take(36)
            : _allStartMenuApps
                .Where(app => app.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Take(48);

        StartMenuApps.Clear();
        foreach (var app in filtered)
        {
            StartMenuApps.Add(app);
        }
    }

    private void RefreshHiddenTray(IReadOnlySet<string> openWindowExecutableKeys)
    {
        var hiddenApps = _backgroundApplicationService.GetBackgroundApplications(openWindowExecutableKeys);
        var existingKeys = HiddenTrayItems
            .Select(item => item.App.NormalizedTargetPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextKeys = hiddenApps
            .Select(entry => entry.App.NormalizedTargetPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in HiddenTrayItems.Where(item => !nextKeys.Contains(item.App.NormalizedTargetPath)).ToList())
        {
            HiddenTrayItems.Remove(item);
        }

        foreach (var entry in hiddenApps.Where(entry => !existingKeys.Contains(entry.App.NormalizedTargetPath)))
        {
            HiddenTrayItems.Add(new HiddenTrayItemViewModel(entry.App, entry.Icon ?? _iconImageService.GetCompactIcon(entry.App)));
        }
    }

    private void RefreshClock()
    {
        if (Settings.ClockDisplayMode == ClockDisplayMode.Hidden)
        {
            ClockText = string.Empty;
            return;
        }

        var now = DateTime.Now;
        var date = now.ToString(Settings.DateFormat, CultureInfo.CurrentCulture);
        var timePattern = Settings.Use24HourClock
            ? Settings.ShowSeconds ? "HH:mm:ss" : "HH:mm"
            : Settings.ShowSeconds ? "h:mm:ss tt" : "h:mm tt";
        var time = now.ToString(timePattern, CultureInfo.CurrentCulture);

        ClockText = Settings.ClockDisplayMode switch
        {
            ClockDisplayMode.TimeOnly => time,
            ClockDisplayMode.DateOnly => date,
            ClockDisplayMode.DateAndTime => $"{date}{SeparatorText(Settings.ClockSeparator)}{time}",
            _ => string.Empty
        };
        LondonTimeText = FormatZoneTime("GMT Standard Time");
        MumbaiTimeText = FormatZoneTime("India Standard Time");
    }

    private void RefreshSystemStatus()
    {
        var snapshot = _networkStatusService.GetSnapshot();
        ConnectionName = snapshot.ConnectionName;
        NetworkAdapterName = snapshot.AdapterName;
        DownloadRateText = snapshot.DownloadRate;
        UploadRateText = snapshot.UploadRate;
        WirelessAdapterName = snapshot.WirelessAdapterName;
        IsWirelessEnabled = snapshot.IsWirelessEnabled;

        var system = _systemMonitorService.GetSnapshot();
        CpuText = system.CpuText;
        MemoryText = system.MemoryText;
        BatteryText = system.BatteryText;
        RefreshCaffeineStatus();

        if (CalendarMonthText != DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture))
        {
            RefreshCalendar();
        }
    }

    private void RefreshCaffeineStatus()
    {
        var snapshot = _caffeineService.GetSnapshot();
        IsCaffeineRunning = snapshot.IsActive;
        IsOriginalCaffeineRunning = snapshot.IsOriginalRunning;
        CaffeineModeText = snapshot.Method switch
        {
            CaffeineKeepAwakeMethod.ShiftKey => $"Shift every {snapshot.IntervalSeconds}s",
            CaffeineKeepAwakeMethod.WindowsStayAwake => "Windows stay-awake",
            CaffeineKeepAwakeMethod.AllowScreensaver => "Prevent sleep, allow screensaver",
            _ => $"F15 every {snapshot.IntervalSeconds}s"
        };
        CaffeineTimerText = FormatCaffeineTimer(snapshot);
    }

    private static string FormatCaffeineTimer(CaffeineSnapshot snapshot)
    {
        var now = DateTimeOffset.Now;
        var parts = new List<string>();
        if (snapshot.StateChangeAt is { } stateChangeAt)
        {
            var remaining = FormatRemaining(stateChangeAt - now);
            parts.Add(snapshot.IsActive ? $"active for {remaining}" : $"inactive for {remaining}");
        }

        if (snapshot.ExitAt is { } exitAt)
        {
            parts.Add($"exit timer {FormatRemaining(exitAt - now)}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "moments";
        }

        return remaining.TotalHours >= 1
            ? $"{Math.Ceiling(remaining.TotalHours):F0}h"
            : $"{Math.Ceiling(remaining.TotalMinutes):F0}m";
    }

    private static bool TryGetMinutes(object? parameter, out int minutes)
    {
        return int.TryParse(parameter?.ToString(), CultureInfo.InvariantCulture, out minutes)
            && minutes > 0;
    }

    private static bool TryGetMinutesOrSeconds(object? parameter, out int seconds)
    {
        return int.TryParse(parameter?.ToString(), CultureInfo.InvariantCulture, out seconds)
            && seconds > 0;
    }

    private async Task RefreshWeatherAsync()
    {
        WeatherText = await _weatherService.GetCurrentWeatherTextAsync();
    }

    private void RefreshVolume()
    {
        var volume = _audioVolumeService.GetVolumePercent();
        if (Math.Abs(_volumePercent - volume) < 0.1)
        {
            return;
        }

        SetProperty(ref _volumePercent, volume, nameof(VolumePercent));
        OnPropertyChanged(nameof(VolumePercentText));
        OnPropertyChanged(nameof(VolumeDialAngle));
    }

    private void RefreshAudioSessions()
    {
        RefreshAudioSessions(_audioVolumeService.GetAppSessions());
    }

    private void RefreshAudioSessions(IReadOnlyList<AudioSessionInfo> sessions)
    {
        AudioSessions.Clear();
        foreach (var session in sessions)
        {
            AudioSessions.Add(new AudioSessionViewModel(session, _audioVolumeService));
        }
    }

    private void RefreshSpotifyStatus(IEnumerable<RunningAppInfo> runningApps)
    {
        var runningAppList = runningApps.ToList();
        var spotifyWindows = runningAppList
            .Where(IsSpotifyWindow)
            .ToList();
        var plexWindows = runningAppList
            .Where(IsPlexWindow)
            .ToList();

        IsSpotifyActive = spotifyWindows.Count > 0 || plexWindows.Count > 0;
        if (!IsSpotifyActive)
        {
            _spotifyWindow = null;
            IsMediaPlaying = false;
            SpotifyTrackText = "Spotify";
            SpotifyArtistText = "Spotify";
            SpotifyAlbumText = "Not playing";
            SpotifyLyricsText = "Start Spotify playback to use Spotify's built-in lyrics view.";
            _spotifySearchQuery = "Spotify lyrics";
            _spotifyArtworkLookupKey = string.Empty;
            SpotifyArtwork = null;
            return;
        }

        if (spotifyWindows.Count == 0)
        {
            _spotifyWindow = plexWindows.FirstOrDefault();
            var plexTitle = plexWindows
                .Select(app => app.DisplayName)
                .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));
            SpotifyTrackText = CleanPlexTitle(plexTitle ?? string.Empty);
            SpotifyArtistText = "Plex";
            SpotifyAlbumText = "Media controls";
            _spotifySearchQuery = string.Empty;
            _spotifyArtworkLookupKey = string.Empty;
            SpotifyArtwork = null;
            return;
        }

        _spotifyWindow = spotifyWindows
            .OrderByDescending(app => !string.Equals(app.DisplayName, "Spotify", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        var trackTitle = spotifyWindows
            .Select(app => app.DisplayName)
            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title)
                && !title.Equals("Spotify", StringComparison.OrdinalIgnoreCase)
                && !title.Contains("Spotify Free", StringComparison.OrdinalIgnoreCase)
                && !title.Contains("Spotify Premium", StringComparison.OrdinalIgnoreCase));

        var cleaned = string.IsNullOrWhiteSpace(trackTitle)
            ? string.Empty
            : CleanSpotifyTitle(trackTitle);
        var parsed = ParseSpotifyTitle(cleaned);
        SpotifyTrackText = parsed.Track;
        SpotifyArtistText = parsed.Artist;
        SpotifyAlbumText = parsed.Album;
        _spotifySearchQuery = parsed.HasSpecificTrack
            ? $"{parsed.Artist} {parsed.Track} lyrics"
            : "Spotify current song lyrics";

        if (!parsed.HasSpecificTrack)
        {
            _spotifyArtworkLookupKey = string.Empty;
            SpotifyArtwork = null;
            return;
        }

        var artworkKey = $"{parsed.Artist}::{parsed.Track}";
        if (!artworkKey.Equals(_spotifyArtworkLookupKey, StringComparison.OrdinalIgnoreCase))
        {
            _spotifyArtworkLookupKey = artworkKey;
            SpotifyArtwork = null;
            _ = RefreshSpotifyArtworkAsync(parsed, artworkKey);
        }
    }

    private async Task RefreshSpotifyArtworkAsync(SpotifyDisplayInfo track, string artworkKey)
    {
        if (_isSpotifyMetadataRefreshRunning)
        {
            return;
        }

        _isSpotifyMetadataRefreshRunning = true;
        try
        {
            var artwork = await _artworkLookupService.FindArtworkAsync(track.Artist, track.Track);
            if (artwork is not null
                && artworkKey.Equals(_spotifyArtworkLookupKey, StringComparison.OrdinalIgnoreCase))
            {
                SpotifyArtwork = artwork;
            }
        }
        finally
        {
            _isSpotifyMetadataRefreshRunning = false;
        }
    }

    private async Task RefreshMediaPlaybackStateAsync()
    {
        if (_isMediaStateRefreshRunning)
        {
            return;
        }

        _isMediaStateRefreshRunning = true;
        try
        {
            IsMediaPlaying = IsSpotifyActive && await _mediaSessionService.IsAnyMediaPlayingAsync();
        }
        finally
        {
            _isMediaStateRefreshRunning = false;
        }
    }

    private static bool IsSpotifyWindow(RunningAppInfo app)
    {
        return app.ExecutablePath.Contains("Spotify", StringComparison.OrdinalIgnoreCase)
            || app.DisplayName.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlexWindow(RunningAppInfo app)
    {
        return app.ExecutablePath.Contains("Plex", StringComparison.OrdinalIgnoreCase)
            || app.DisplayName.Contains("Plex", StringComparison.OrdinalIgnoreCase)
            || app.DisplayName.Contains("app.plex.tv", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanSpotifyTitle(string title)
    {
        return title
            .Replace("- Spotify", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Spotify", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '-', '—');
    }

    private static string CleanPlexTitle(string title)
    {
        var cleaned = title
            .Replace("- Plex", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Plex", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("- Google Chrome", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("- Microsoft Edge", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '-', '—');
        return string.IsNullOrWhiteSpace(cleaned) ? "Plex" : cleaned;
    }

    private static SpotifyDisplayInfo ParseSpotifyTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new SpotifyDisplayInfo("Waiting for song title", "Spotify", "Now playing", false);
        }

        var separators = new[] { " - ", " – ", " — " };
        foreach (var separator in separators)
        {
            var parts = title.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
            {
                return new SpotifyDisplayInfo(parts[1], parts[0], "From Spotify", true);
            }
        }

        return new SpotifyDisplayInfo(title, "Spotify", "From Spotify", true);
    }

    private static void SendMediaKey(byte virtualKey)
    {
        NativeMethods.keybd_event(virtualKey, 0, 0, 0);
        NativeMethods.keybd_event(virtualKey, 0, NativeMethods.KeyeventfKeyUp, 0);
    }

    private void ToggleMediaPlayback()
    {
        SendMediaKey(NativeMethods.VkMediaPlayPause);
        IsMediaPlaying = !IsMediaPlaying;
        _ = Task.Run(async () =>
        {
            await Task.Delay(450);
            var refreshTask = await Application.Current.Dispatcher.InvokeAsync(RefreshMediaPlaybackStateAsync);
            await refreshTask;
        });
    }

    private void LaunchWithArguments(DockItemViewModel item, string arguments)
    {
        _applicationLauncher.Launch(new PinnedApp
        {
            DisplayName = item.App.DisplayName,
            TargetPath = item.App.TargetPath,
            Arguments = arguments
        });
    }

    private void OpenBrowserPrivate(DockItemViewModel item)
    {
        var target = $"{item.DisplayName} {item.TargetPath}";
        var argument = target.Contains("firefox", StringComparison.OrdinalIgnoreCase)
            ? "-private-window"
            : target.Contains("msedge", StringComparison.OrdinalIgnoreCase) || target.Contains("edge", StringComparison.OrdinalIgnoreCase)
                ? "--inprivate"
                : "--incognito";
        LaunchWithArguments(item, argument);
    }

    private void SendAppHotkey(DockItemViewModel item, byte virtualKey)
    {
        if (item.RunningApp is not null)
        {
            _windowActivationService.Activate(item.RunningApp);
        }

        NativeMethods.keybd_event(0x11, 0, 0, 0);
        NativeMethods.keybd_event(0x10, 0, 0, 0);
        NativeMethods.keybd_event(virtualKey, 0, 0, 0);
        NativeMethods.keybd_event(virtualKey, 0, NativeMethods.KeyeventfKeyUp, 0);
        NativeMethods.keybd_event(0x10, 0, NativeMethods.KeyeventfKeyUp, 0);
        NativeMethods.keybd_event(0x11, 0, NativeMethods.KeyeventfKeyUp, 0);
    }

    private async Task RefreshAvailableWifiNetworksAsync()
    {
        var networks = await _networkStatusService.GetAvailableWifiNetworksAsync();
        AvailableWifiNetworks.Clear();
        foreach (var network in networks)
        {
            AvailableWifiNetworks.Add(new WifiNetworkViewModel(network.Ssid, network.Signal, network.Security));
        }
    }

    private void PersistPinnedOrder()
    {
        var pinnedByKey = Settings.PinnedApps
            .GroupBy(StackKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var ordered = new List<PinnedApp>();
        foreach (var item in Items)
        {
            var key = StackKey(item.App);
            if (pinnedByKey.Remove(key, out var pinnedApps))
            {
                ordered.AddRange(pinnedApps);
            }
        }

        ordered.AddRange(pinnedByKey.Values.SelectMany(apps => apps));
        Settings.PinnedApps.Clear();
        Settings.PinnedApps.AddRange(ordered);
        SaveAndApply();
    }

    private static void ResetAutoCloseTimer(DispatcherTimer timer, bool shouldRun)
    {
        timer.Stop();
        if (shouldRun)
        {
            timer.Start();
        }
    }

    private void RefreshCalendar()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
        var leadingDays = (int)firstOfMonth.DayOfWeek;
        var firstVisibleDay = firstOfMonth.AddDays(-leadingDays);

        CalendarMonthText = today.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        CalendarDays.Clear();
        for (var index = 0; index < 42; index++)
        {
            var date = firstVisibleDay.AddDays(index);
            CalendarDays.Add(new CalendarDayViewModel
            {
                Day = date.Day,
                IsCurrentMonth = date.Month == today.Month,
                IsToday = date.Date == today
            });
        }
    }

    private static string SeparatorText(ClockSeparator separator)
    {
        return separator switch
        {
            ClockSeparator.Pipe => "  |  ",
            ClockSeparator.Dash => "  -  ",
            ClockSeparator.Space => "   ",
            ClockSeparator.None => " ",
            _ => "  •  "
        };
    }

    private static string FormatZoneTime(string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, zone);
            return localTime.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }
        catch
        {
            return "--";
        }
    }

    private static void ExecuteForItem(object? parameter, Action<DockItemViewModel> action)
    {
        if (parameter is DockItemViewModel item)
        {
            action(item);
        }
    }

    private static string StackKey(PinnedApp app)
    {
        return app.NormalizedTargetPath;
    }

    private static string CleanRunningAppName(RunningAppInfo app)
    {
        if (!string.IsNullOrWhiteSpace(app.DisplayName))
        {
            return app.DisplayName;
        }

        var fileName = Path.GetFileNameWithoutExtension(app.ExecutablePath);
        return string.IsNullOrWhiteSpace(fileName) ? "Running app" : fileName;
    }
}

internal enum DockSnapSide
{
    Left,
    Right
}

internal sealed record SpotifyDisplayInfo(string Track, string Artist, string Album, bool HasSpecificTrack);
