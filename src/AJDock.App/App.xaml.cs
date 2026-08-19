using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AJDock.App.Services;
using AJDock.App.ViewModels;
using AJDock.App.Views;

namespace AJDock.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private TaskbarService? _taskbarService;
    private bool _ownsSingleInstanceMutex;
    private CancellationTokenSource? _singleInstanceCommandCancellation;
    private MainWindow? _dockWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Write("Startup requested.");
        var snapshotPath = TryGetSnapshotPath(e.Args);
        var settingsSnapshotPath = TryGetNamedPath(e.Args, "--snapshot-settings", "/snapshot-settings");
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\AJDock.SingleInstance", out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            AppLog.Write("Existing AJ Dock instance detected; signaling existing instance.");
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _taskbarService = new TaskbarService();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _taskbarService.RestoreIfNeeded();
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("Unhandled dispatcher exception.", args.Exception);
            _taskbarService.RestoreIfNeeded();
            MessageBox.Show(args.Exception.Message, "AJ Dock", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(settingsSnapshotPath))
            {
                var settingsService = new JsonSettingsService();
                var settings = settingsService.Load();
                var settingsWindow = new SettingsWindow(new SettingsViewModel(settings, () => settingsService.Save(settings)));
                MainWindow = settingsWindow;
                settingsWindow.Show();
                _ = CaptureSnapshotAndShutdownAsync(settingsWindow, settingsSnapshotPath);
                base.OnStartup(e);
                return;
            }

            var viewModel = new DockViewModel(
                new JsonSettingsService(),
                new ApplicationLauncher(),
                new RunningApplicationService(),
                new WindowActivationService(),
                new PinnedAppFactory(new ShortcutResolver()),
                new IconImageService(),
                new IconPickerService(),
                new StartupService(),
                _taskbarService,
                new NetworkStatusService(),
                new StartMenuAppService(new ShortcutResolver()),
                new WindowsStartService(),
                new BackgroundApplicationService(),
                new AudioVolumeService(),
                new WindowPreviewService(),
                new NotificationBadgeService(),
                new SystemMonitorService(),
                new WeatherService(),
                new ArtworkLookupService());

            var window = new MainWindow(viewModel, new WindowEffectService(), snapshotPath);
            _dockWindow = window;
            MainWindow = window;
            window.Show();
            StartSingleInstanceCommandServer(window);
            AppLog.Write("Main window shown.");
            base.OnStartup(e);
        }
        catch (Exception exception)
        {
            AppLog.Write("Startup failed.", exception);
            _taskbarService.RestoreIfNeeded();
            MessageBox.Show(exception.Message, "AJ Dock", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _taskbarService?.RestoreIfNeeded();
        _singleInstanceCommandCancellation?.Cancel();
        _singleInstanceCommandCancellation?.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void StartSingleInstanceCommandServer(MainWindow window)
    {
        _singleInstanceCommandCancellation = new CancellationTokenSource();
        var token = _singleInstanceCommandCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        "AJDock.Command",
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync(token);
                    if (command?.Equals("show-settings", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        AppLog.Write("Single-instance command received: show-settings.");
                        await Dispatcher.InvokeAsync(window.ActivateAndShowSettings, DispatcherPriority.Normal, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    AppLog.Write("Single-instance command server error.", exception);
                    await Task.Delay(500, token);
                }
            }
        }, token);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "AJDock.Command", PipeDirection.Out);
            client.Connect(750);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("show-settings");
        }
        catch (Exception exception)
        {
            AppLog.Write("Could not signal existing instance.", exception);
            MessageBox.Show(
                "AJ Dock is already running. Look near the bottom center of your screen, or close the existing AJDock.exe process and launch it again.",
                "AJ Dock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static string? TryGetSnapshotPath(string[] args)
    {
        return TryGetNamedPath(args, "--snapshot", "/snapshot");
    }

    private static string? TryGetNamedPath(string[] args, string longName, string slashName)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if ((args[index].Equals(longName, StringComparison.OrdinalIgnoreCase)
                    || args[index].Equals(slashName, StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static async Task CaptureSnapshotAndShutdownAsync(Window window, string path)
    {
        try
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            AppLog.Write($"Snapshot saved to {path}.");
        }
        catch (Exception exception)
        {
            AppLog.Write("Snapshot failed.", exception);
        }
        finally
        {
            Current.Shutdown();
        }
    }
}
