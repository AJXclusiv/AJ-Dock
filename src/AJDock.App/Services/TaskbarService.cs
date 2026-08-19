using System.Text;
using System.Windows.Threading;
using AJDock.App.Native;

namespace AJDock.App.Services;

public sealed class TaskbarService
{
    private readonly DispatcherTimer _enforcementTimer;
    private bool _hiddenByDock;

    public TaskbarService()
    {
        _enforcementTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _enforcementTimer.Tick += (_, _) => ForceTaskbarsHidden();
    }

    public void SetHidden(bool hidden)
    {
        _hiddenByDock = hidden;
        if (hidden)
        {
            ForceTaskbarsHidden();
            _enforcementTimer.Start();
            return;
        }

        _enforcementTimer.Stop();
        SetTaskbarsVisible();
    }

    public void RestoreIfNeeded()
    {
        _enforcementTimer.Stop();
        if (_hiddenByDock)
        {
            SetTaskbarsVisible();
            _hiddenByDock = false;
        }
    }

    private static void ForceTaskbarsHidden()
    {
        foreach (var taskbar in GetTaskbarWindows())
        {
            NativeMethods.ShowWindowAsync(taskbar, NativeMethods.SwHide);
            NativeMethods.SetWindowPos(
                taskbar,
                nint.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove
                    | NativeMethods.SwpNoSize
                    | NativeMethods.SwpNoZOrder
                    | NativeMethods.SwpNoActivate
                    | NativeMethods.SwpHideWindow);
        }
    }

    private static void SetTaskbarsVisible()
    {
        foreach (var taskbar in GetTaskbarWindows())
        {
            NativeMethods.SetWindowPos(
                taskbar,
                nint.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove
                    | NativeMethods.SwpNoSize
                    | NativeMethods.SwpNoZOrder
                    | NativeMethods.SwpNoActivate
                    | NativeMethods.SwpShowWindow);
            NativeMethods.ShowWindowAsync(taskbar, NativeMethods.SwShow);
        }
    }

    private static IReadOnlyList<nint> GetTaskbarWindows()
    {
        var handles = new List<nint>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            var className = GetClassName(hWnd);
            if (IsTaskbarClass(className))
            {
                handles.Add(hWnd);
            }

            return true;
        }, nint.Zero);

        var primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (primary != nint.Zero && !handles.Contains(primary))
        {
            handles.Add(primary);
        }

        var secondary = nint.Zero;
        while ((secondary = NativeMethods.FindWindowEx(nint.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != nint.Zero)
        {
            if (!handles.Contains(secondary))
            {
                handles.Add(secondary);
            }
        }

        return handles;
    }

    private static bool IsTaskbarClass(string className)
    {
        return className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)
            || className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClassName(nint hWnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hWnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }
}
