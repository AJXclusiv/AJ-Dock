using System.Diagnostics;
using System.Text;
using AJDock.App.Native;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class RunningApplicationService
{
    public IReadOnlyList<RunningAppInfo> GetRunningApplications()
    {
        var results = new List<RunningAppInfo>();
        var currentProcessId = Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsOpenWindowCandidate(hWnd))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0 || processId == currentProcessId)
            {
                return true;
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                var executablePath = process.MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return true;
                }

                results.Add(new RunningAppInfo
                {
                    ProcessId = (int)processId,
                    MainWindowHandle = hWnd,
                    DisplayName = TryGetTitle(hWnd) ?? process.ProcessName,
                    ExecutablePath = executablePath
                });
            }
            catch
            {
                // Protected/elevated processes can deny module access. They are skipped for V1 matching.
            }

            return true;
        }, nint.Zero);

        return results;
    }

    public RunningAppInfo? FindRunning(PinnedApp app)
    {
        var target = app.NormalizedTargetPath;
        return GetRunningApplications().FirstOrDefault(process =>
            string.Equals(process.NormalizedExecutablePath, target, StringComparison.OrdinalIgnoreCase));
    }

    public void Close(RunningAppInfo app)
    {
        try
        {
            using var process = Process.GetProcessById(app.ProcessId);
            if (!process.CloseMainWindow())
            {
                process.Kill(entireProcessTree: false);
            }
        }
        catch
        {
            // Process may have exited between refresh and action.
        }
    }

    private static string? TryGetTitle(nint hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return null;
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static bool IsOpenWindowCandidate(nint hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
        {
            return false;
        }

        var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlExStyle).ToInt64();
        if ((exStyle & NativeMethods.WsExToolWindow) != 0 || (exStyle & NativeMethods.WsExNoActivate) != 0)
        {
            return false;
        }

        var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GwOwner);
        var rootOwner = NativeMethods.GetAncestor(hWnd, NativeMethods.GaRootOwner);
        var isExplicitAppWindow = (exStyle & NativeMethods.WsExAppWindow) != 0;
        if (owner != nint.Zero && rootOwner != hWnd && !isExplicitAppWindow)
        {
            return false;
        }

        if (IsShellWindowClass(TryGetClassName(hWnd)))
        {
            return false;
        }

        if (NativeMethods.IsIconic(hWnd))
        {
            return true;
        }

        if (IsCloaked(hWnd))
        {
            return false;
        }

        return HasRealWindowBounds(hWnd);
    }

    private static bool IsCloaked(nint hWnd)
    {
        return NativeMethods.DwmGetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaCloaked,
            out int cloaked,
            sizeof(int)) == 0 && cloaked != 0;
    }

    private static bool HasRealWindowBounds(nint hWnd)
    {
        var result = NativeMethods.DwmGetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaExtendedFrameBounds,
            out NativeMethods.Rect rect,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>());

        if (result != 0 && !NativeMethods.GetWindowRect(hWnd, out rect))
        {
            return false;
        }

        return rect.Width > 80 && rect.Height > 40;
    }

    private static string TryGetClassName(nint hWnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hWnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static bool IsShellWindowClass(string className)
    {
        return className is "Shell_TrayWnd"
            or "Shell_SecondaryTrayWnd"
            or "Progman"
            or "WorkerW"
            or "NotifyIconOverflowWindow"
            or "Windows.UI.Core.CoreWindow";
    }
}
