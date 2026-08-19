using System.Text;
using AJDock.App.Native;

namespace AJDock.App.Services;

public sealed class WindowsStartService
{
    private bool _startMenuOpenedByDock;
    private DateTimeOffset _lastToggle = DateTimeOffset.MinValue;

    public void ToggleStartMenu()
    {
        if (_startMenuOpenedByDock || IsStartMenuForeground())
        {
            PressKey(NativeMethods.VkEscape);
            _startMenuOpenedByDock = false;
            _lastToggle = DateTimeOffset.UtcNow;
            return;
        }

        PressKey(NativeMethods.VkLeftWindows);
        _startMenuOpenedByDock = true;
        _lastToggle = DateTimeOffset.UtcNow;
    }

    private static void PressKey(byte key)
    {
        NativeMethods.keybd_event(key, 0, 0, 0);
        NativeMethods.keybd_event(key, 0, NativeMethods.KeyeventfKeyUp, 0);
    }

    private static bool IsStartMenuForeground()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero)
        {
            return false;
        }

        var className = GetClassName(foreground);
        if (IsStartMenuClass(className))
        {
            return true;
        }

        var parent = NativeMethods.GetParent(foreground);
        while (parent != nint.Zero)
        {
            if (IsStartMenuClass(GetClassName(parent)))
            {
                return true;
            }

            parent = NativeMethods.GetParent(parent);
        }

        return false;
    }

    private static bool IsStartMenuClass(string className)
    {
        return className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase)
            || className.Equals("Windows.UI.Composition.DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Start", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClassName(nint hWnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hWnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    public void OpenStartMenu()
    {
        if (_startMenuOpenedByDock
            && DateTimeOffset.UtcNow - _lastToggle < TimeSpan.FromMilliseconds(750))
        {
            return;
        }

        PressKey(NativeMethods.VkLeftWindows);
        _startMenuOpenedByDock = true;
        _lastToggle = DateTimeOffset.UtcNow;
    }
}
