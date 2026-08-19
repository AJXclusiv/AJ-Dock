using AJDock.App.Native;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class WindowActivationService
{
    public void Activate(RunningAppInfo app)
    {
        if (app.MainWindowHandle == nint.Zero)
        {
            return;
        }

        if (NativeMethods.IsIconic(app.MainWindowHandle))
        {
            NativeMethods.ShowWindowAsync(app.MainWindowHandle, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(app.MainWindowHandle);
    }

    public void ActivateOrMinimize(RunningAppInfo app)
    {
        if (app.MainWindowHandle == nint.Zero)
        {
            return;
        }

        if (NativeMethods.GetForegroundWindow() == app.MainWindowHandle && !NativeMethods.IsIconic(app.MainWindowHandle))
        {
            NativeMethods.ShowWindowAsync(app.MainWindowHandle, NativeMethods.SwMinimize);
            return;
        }

        if (NativeMethods.IsIconic(app.MainWindowHandle))
        {
            NativeMethods.ShowWindowAsync(app.MainWindowHandle, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(app.MainWindowHandle);
    }
}
