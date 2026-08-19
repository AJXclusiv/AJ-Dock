using System.Runtime.InteropServices;

namespace AJDock.App.Services;

public sealed class SystemMonitorService
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;

    public SystemMonitorSnapshot GetSnapshot()
    {
        return new SystemMonitorSnapshot(
            GetCpuPercentText(),
            GetMemoryText(),
            GetBatteryText());
    }

    private string GetCpuPercentText()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return "CPU --";
        }

        var idle = ToUInt64(idleTime);
        var kernel = ToUInt64(kernelTime);
        var user = ToUInt64(userTime);

        if (_lastKernel == 0 && _lastUser == 0)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            return "CPU --";
        }

        var idleDelta = idle - _lastIdle;
        var kernelDelta = kernel - _lastKernel;
        var userDelta = user - _lastUser;
        var total = kernelDelta + userDelta;

        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;

        if (total == 0)
        {
            return "CPU --";
        }

        var busy = Math.Clamp((total - idleDelta) * 100.0 / total, 0, 100);
        return $"CPU {busy:F0}%";
    }

    private static string GetMemoryText()
    {
        var status = new MemoryStatusEx();
        status.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            return "RAM --";
        }

        var used = status.TotalPhysical - status.AvailablePhysical;
        var percent = used * 100.0 / status.TotalPhysical;
        return $"RAM {percent:F0}%";
    }

    private static string GetBatteryText()
    {
        if (!GetSystemPowerStatus(out var status) || status.BatteryLifePercent > 100)
        {
            return "AC";
        }

        return status.ACLineStatus == 1
            ? $"BAT {status.BatteryLifePercent}%+"
            : $"BAT {status.BatteryLifePercent}%";
    }

    private static ulong ToUInt64(FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}

public sealed record SystemMonitorSnapshot(string CpuText, string MemoryText, string BatteryText);
