using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AJDock.App.Services;

public sealed class AudioVolumeService
{
    public double GetVolumePercent()
    {
        try
        {
            using var endpoint = GetEndpointVolume();
            return endpoint.Value.GetMasterVolumeLevelScalar(out var level) == 0
                ? Math.Clamp(level * 100.0, 0, 100)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void SetVolumePercent(double percent)
    {
        try
        {
            using var endpoint = GetEndpointVolume();
            var level = (float)Math.Clamp(percent / 100.0, 0, 1);
            var eventContext = Guid.Empty;
            endpoint.Value.SetMasterVolumeLevelScalar(level, ref eventContext);
        }
        catch
        {
            // Keep the dock responsive even if Core Audio is temporarily unavailable.
        }
    }

    public double GetOutputPeakPercent()
    {
        try
        {
            using var meter = GetAudioMeter();
            return meter.Value.GetPeakValue(out var peak) == 0
                ? Math.Clamp(peak * 100d, 0, 100)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public IReadOnlyList<AudioSessionInfo> GetAppSessions()
    {
        try
        {
            using var manager = GetSessionManager();
            if (manager.Value.GetSessionEnumerator(out var sessionEnumerator) != 0)
            {
                return [];
            }

            try
            {
                if (sessionEnumerator.GetCount(out var count) != 0)
                {
                    return [];
                }

                var sessions = new List<AudioSessionInfo>();
                for (var index = 0; index < count; index++)
                {
                    if (sessionEnumerator.GetSession(index, out var sessionControl) != 0 || sessionControl is null)
                    {
                        continue;
                    }

                    try
                    {
                        if (sessionControl is not IAudioSessionControl2 sessionControl2
                            || sessionControl is not ISimpleAudioVolume simpleVolume
                            || sessionControl2.GetProcessId(out var processId) != 0
                            || processId == 0)
                        {
                            continue;
                        }

                        if (simpleVolume.GetMasterVolume(out var volume) != 0)
                        {
                            continue;
                        }

                        _ = sessionControl.GetState(out var state);
                        _ = simpleVolume.GetMute(out var muted);
                        sessions.Add(new AudioSessionInfo(
                            GetSessionId(sessionControl2, processId),
                            (int)processId,
                            GetSessionName(sessionControl, processId),
                            Math.Clamp(volume * 100d, 0, 100),
                            muted,
                            state == AudioSessionState.Active));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sessionControl);
                    }
                }

                return sessions
                    .GroupBy(session => session.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(session => session.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .Take(8)
                    .ToList();
            }
            finally
            {
                Marshal.ReleaseComObject(sessionEnumerator);
            }
        }
        catch
        {
            return [];
        }
    }

    public void SetSessionVolume(string sessionId, double percent)
    {
        WithSessionVolume(sessionId, volume =>
        {
            var level = (float)Math.Clamp(percent / 100d, 0, 1);
            var context = Guid.Empty;
            volume.SetMasterVolume(level, ref context);
        });
    }

    public void SetSessionMute(string sessionId, bool isMuted)
    {
        WithSessionVolume(sessionId, volume =>
        {
            var context = Guid.Empty;
            volume.SetMute(isMuted, ref context);
        });
    }

    private static ComReleaser<IAudioEndpointVolume> GetEndpointVolume()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))
            ?? throw new InvalidOperationException("Core Audio device enumerator is unavailable.");
        var enumerator = (IMMDeviceEnumerator)(Activator.CreateInstance(enumeratorType)
            ?? throw new InvalidOperationException("Core Audio device enumerator could not be created."));
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
            {
                throw new InvalidOperationException("No default audio endpoint is available.");
            }

            try
            {
                var endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref endpointVolumeId, ClsCtx.All, nint.Zero, out var endpoint) != 0)
                {
                    throw new InvalidOperationException("The default audio endpoint could not be activated.");
                }

                return new ComReleaser<IAudioEndpointVolume>((IAudioEndpointVolume)endpoint);
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static ComReleaser<IAudioMeterInformation> GetAudioMeter()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))
            ?? throw new InvalidOperationException("Core Audio device enumerator is unavailable.");
        var enumerator = (IMMDeviceEnumerator)(Activator.CreateInstance(enumeratorType)
            ?? throw new InvalidOperationException("Core Audio device enumerator could not be created."));
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
            {
                throw new InvalidOperationException("No default audio endpoint is available.");
            }

            try
            {
                var meterId = typeof(IAudioMeterInformation).GUID;
                if (device.Activate(ref meterId, ClsCtx.All, nint.Zero, out var meter) != 0)
                {
                    throw new InvalidOperationException("The default audio meter could not be activated.");
                }

                return new ComReleaser<IAudioMeterInformation>((IAudioMeterInformation)meter);
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static ComReleaser<IAudioSessionManager2> GetSessionManager()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))
            ?? throw new InvalidOperationException("Core Audio device enumerator is unavailable.");
        var enumerator = (IMMDeviceEnumerator)(Activator.CreateInstance(enumeratorType)
            ?? throw new InvalidOperationException("Core Audio device enumerator could not be created."));
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
            {
                throw new InvalidOperationException("No default audio endpoint is available.");
            }

            try
            {
                var managerId = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref managerId, ClsCtx.All, nint.Zero, out var manager) != 0)
                {
                    throw new InvalidOperationException("The default audio session manager could not be activated.");
                }

                return new ComReleaser<IAudioSessionManager2>((IAudioSessionManager2)manager);
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void WithSessionVolume(string sessionId, Action<ISimpleAudioVolume> action)
    {
        try
        {
            using var manager = GetSessionManager();
            if (manager.Value.GetSessionEnumerator(out var sessionEnumerator) != 0)
            {
                return;
            }

            try
            {
                if (sessionEnumerator.GetCount(out var count) != 0)
                {
                    return;
                }

                for (var index = 0; index < count; index++)
                {
                    if (sessionEnumerator.GetSession(index, out var sessionControl) != 0 || sessionControl is null)
                    {
                        continue;
                    }

                    try
                    {
                        if (sessionControl is IAudioSessionControl2 sessionControl2
                            && sessionControl is ISimpleAudioVolume simpleVolume
                            && string.Equals(GetSessionId(sessionControl2, 0), sessionId, StringComparison.OrdinalIgnoreCase))
                        {
                            action(simpleVolume);
                            return;
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sessionControl);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(sessionEnumerator);
            }
        }
        catch
        {
            // Keep the dock responsive if a session disappears while the user drags a slider.
        }
    }

    private static string GetSessionId(IAudioSessionControl2 sessionControl, uint fallbackProcessId)
    {
        try
        {
            if (sessionControl.GetSessionInstanceIdentifier(out var instanceId) == 0
                && !string.IsNullOrWhiteSpace(instanceId))
            {
                return instanceId;
            }
        }
        catch
        {
            // Fall back to the process id below.
        }

        if (fallbackProcessId == 0 && sessionControl.GetProcessId(out var processId) == 0)
        {
            fallbackProcessId = processId;
        }

        return fallbackProcessId.ToString();
    }

    private static string GetSessionName(IAudioSessionControl sessionControl, uint processId)
    {
        try
        {
            if (sessionControl.GetDisplayName(out var displayName) == 0
                && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }
        catch
        {
            // Process name fallback below.
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.MainWindowTitle)
                ? process.ProcessName
                : process.MainWindowTitle;
        }
        catch
        {
            return $"App {processId}";
        }
    }

    private sealed class ComReleaser<T> : IDisposable
        where T : class
    {
        public ComReleaser(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public void Dispose()
        {
            Marshal.ReleaseComObject(Value);
        }
    }

    private enum EDataFlow
    {
        Render = 0
    }

    private enum ERole
    {
        Multimedia = 1
    }

    [Flags]
    private enum ClsCtx : uint
    {
        All = 0x17
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out object devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, ClsCtx classContext, nint activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(nint notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(nint notify);

        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig]
        int GetPeakValue(out float peak);

        [PreserveSig]
        int GetMeteringChannelCount(out uint channelCount);

        [PreserveSig]
        int GetChannelsPeakValues(uint channelCount, [Out] float[] peakValues);

        [PreserveSig]
        int QueryHardwareSupport(out uint hardwareSupportMask);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig]
        int GetAudioSessionControl(nint audioSessionGuid, uint streamFlags, out object sessionControl);

        [PreserveSig]
        int GetSimpleAudioVolume(nint audioSessionGuid, uint streamFlags, out object audioVolume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);

        [PreserveSig]
        int RegisterSessionNotification(nint sessionNotification);

        [PreserveSig]
        int UnregisterSessionNotification(nint sessionNotification);

        [PreserveSig]
        int RegisterDuckNotification(string sessionId, nint duckNotification);

        [PreserveSig]
        int UnregisterDuckNotification(nint duckNotification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int sessionCount);

        [PreserveSig]
        int GetSession(int sessionIndex, out IAudioSessionControl sessionControl);
    }

    private enum AudioSessionState
    {
        Inactive = 0,
        Active = 1,
        Expired = 2
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig]
        int GetState(out AudioSessionState state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid groupingId);

        [PreserveSig]
        int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int UnregisterAudioSessionNotification(nint newNotifications);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out AudioSessionState state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid groupingId);

        [PreserveSig]
        int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int UnregisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionId);

        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceId);

        [PreserveSig]
        int GetProcessId(out uint processId);

        [PreserveSig]
        int IsSystemSoundsSession();

        [PreserveSig]
        int SetDuckingPreference(bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolume(out float level);

        [PreserveSig]
        int SetMute(bool isMuted, ref Guid eventContext);

        [PreserveSig]
        int GetMute(out bool isMuted);
    }
}

public sealed record AudioSessionInfo(
    string Id,
    int ProcessId,
    string DisplayName,
    double VolumePercent,
    bool IsMuted,
    bool IsActive);
