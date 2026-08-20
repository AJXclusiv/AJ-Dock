using System.Diagnostics;
using System.IO;
using AJDock.App.Native;

namespace AJDock.App.Services;

public enum CaffeineKeepAwakeMethod
{
    F15Key,
    ShiftKey,
    WindowsStayAwake,
    AllowScreensaver
}

public sealed class CaffeineService : IDisposable
{
    private const string ProcessName = "caffeine";
    private readonly string _appPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "caffeine.exe");
    private readonly Timer _pulseTimer;
    private readonly object _syncRoot = new();
    private DateTimeOffset? _stateChangeAt;
    private DateTimeOffset? _exitAt;
    private bool _isActive;
    private bool _disposed;

    public CaffeineService()
    {
        _pulseTimer = new Timer(_ => Pulse(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsInstalled => File.Exists(_appPath);

    public bool IsActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _isActive;
            }
        }
    }

    public int IntervalSeconds { get; private set; } = 59;

    public CaffeineKeepAwakeMethod Method { get; private set; } = CaffeineKeepAwakeMethod.F15Key;

    public bool IsOriginalRunning()
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
        SetActive(!IsActive);
    }

    public void SetActive(bool isActive)
    {
        lock (_syncRoot)
        {
            _isActive = isActive;
            _stateChangeAt = null;
        }

        ApplyTimerState();
    }

    public void ActiveFor(TimeSpan duration)
    {
        lock (_syncRoot)
        {
            _isActive = true;
            _stateChangeAt = DateTimeOffset.Now.Add(duration);
        }

        ApplyTimerState();
    }

    public void InactiveFor(TimeSpan duration)
    {
        lock (_syncRoot)
        {
            _isActive = false;
            _stateChangeAt = DateTimeOffset.Now.Add(duration);
        }

        ApplyTimerState();
    }

    public void ExitAfter(TimeSpan duration)
    {
        lock (_syncRoot)
        {
            _exitAt = DateTimeOffset.Now.Add(duration);
        }
    }

    public void ClearTimers()
    {
        lock (_syncRoot)
        {
            _stateChangeAt = null;
            _exitAt = null;
        }
    }

    public void SetInterval(int seconds)
    {
        IntervalSeconds = Math.Clamp(seconds, 15, 600);
        ApplyTimerState();
    }

    public void SetMethod(CaffeineKeepAwakeMethod method)
    {
        if (Method is CaffeineKeepAwakeMethod.WindowsStayAwake or CaffeineKeepAwakeMethod.AllowScreensaver)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
        }

        Method = method;
        ApplyTimerState();
    }

    public void LaunchOriginal(string arguments = "")
    {
        if (!File.Exists(_appPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_appPath, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_appPath) ?? string.Empty
        });
    }

    public void StopOriginal()
    {
        if (File.Exists(_appPath))
        {
            LaunchOriginal("-appexit");
            return;
        }

        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: false);
                    }
                }
                catch
                {
                    // Original Caffeine is optional; failures should not affect AJ Dock.
                }
            }
        }
    }

    public CaffeineSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return new CaffeineSnapshot(
                _isActive,
                Method,
                IntervalSeconds,
                _stateChangeAt,
                _exitAt,
                IsOriginalRunning());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pulseTimer.Dispose();
        NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
    }

    private void ApplyTimerState()
    {
        if (!IsActive)
        {
            _pulseTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
            return;
        }

        Pulse();
        _pulseTimer.Change(TimeSpan.FromSeconds(IntervalSeconds), TimeSpan.FromSeconds(IntervalSeconds));
    }

    private void Pulse()
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        lock (_syncRoot)
        {
            if (_exitAt is { } exitAt && now >= exitAt)
            {
                _isActive = false;
                _stateChangeAt = null;
                _exitAt = null;
                _pulseTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
                return;
            }

            if (_stateChangeAt is { } changeAt && now >= changeAt)
            {
                _isActive = !_isActive;
                _stateChangeAt = null;
                if (!_isActive)
                {
                    _pulseTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
                    return;
                }
            }

            if (!_isActive)
            {
                return;
            }
        }

        switch (Method)
        {
            case CaffeineKeepAwakeMethod.ShiftKey:
                NativeMethods.keybd_event(NativeMethods.VkShift, 0, 0, 0);
                NativeMethods.keybd_event(NativeMethods.VkShift, 0, NativeMethods.KeyeventfKeyUp, 0);
                break;
            case CaffeineKeepAwakeMethod.WindowsStayAwake:
                NativeMethods.SetThreadExecutionState(
                    NativeMethods.EsContinuous
                    | NativeMethods.EsSystemRequired
                    | NativeMethods.EsDisplayRequired);
                break;
            case CaffeineKeepAwakeMethod.AllowScreensaver:
                NativeMethods.SetThreadExecutionState(
                    NativeMethods.EsContinuous
                    | NativeMethods.EsSystemRequired);
                break;
            default:
                NativeMethods.keybd_event(NativeMethods.VkF15, 0, NativeMethods.KeyeventfKeyUp, 0);
                break;
        }
    }
}

public sealed record CaffeineSnapshot(
    bool IsActive,
    CaffeineKeepAwakeMethod Method,
    int IntervalSeconds,
    DateTimeOffset? StateChangeAt,
    DateTimeOffset? ExitAt,
    bool IsOriginalRunning);
