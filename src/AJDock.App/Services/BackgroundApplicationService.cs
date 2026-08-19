using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AJDock.App.Native;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed record BackgroundApplication(PinnedApp App, ImageSource? Icon);

public sealed class BackgroundApplicationService
{
    public IReadOnlyList<BackgroundApplication> GetBackgroundApplications(IReadOnlySet<string> excludedExecutableKeys)
    {
        return TryGetExplorerOverflowTrayApplications(excludedExecutableKeys);
    }

    private static IReadOnlyList<BackgroundApplication> TryGetExplorerOverflowTrayApplications(IReadOnlySet<string> excludedExecutableKeys)
    {
        var toolbarHandles = GetTrayToolbarHandles(preferOverflow: true);
        var apps = toolbarHandles
            .SelectMany(ReadToolbarApplications)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.App.TargetPath))
            .Where(entry => !excludedExecutableKeys.Contains(entry.App.NormalizedTargetPath))
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.App.NormalizedTargetPath) ? entry.App.DisplayName : entry.App.NormalizedTargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(16)
            .ToList();

        return apps;
    }

    private static IReadOnlyList<nint> GetTrayToolbarHandles(bool preferOverflow)
    {
        var handles = new List<nint>();

        if (preferOverflow)
        {
            var overflow = NativeMethods.FindWindow("NotifyIconOverflowWindow", null);
            if (overflow != nint.Zero)
            {
                handles.AddRange(FindChildToolbars(overflow));
            }
        }

        if (handles.Count > 0)
        {
            return handles;
        }

        var shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (shell != nint.Zero)
        {
            handles.AddRange(FindChildToolbars(shell).Where(IsNotificationToolbar));
        }

        return handles;
    }

    private static IEnumerable<nint> FindChildToolbars(nint root)
    {
        var toolbars = new List<nint>();
        NativeMethods.EnumChildWindows(root, (hWnd, _) =>
        {
            if (GetClassName(hWnd).Equals("ToolbarWindow32", StringComparison.OrdinalIgnoreCase))
            {
                toolbars.Add(hWnd);
            }

            return true;
        }, nint.Zero);

        return toolbars;
    }

    private static bool IsNotificationToolbar(nint hWnd)
    {
        var parent = NativeMethods.GetParent(hWnd);
        while (parent != nint.Zero)
        {
            var className = GetClassName(parent);
            if (className.Equals("TrayNotifyWnd", StringComparison.OrdinalIgnoreCase)
                || className.Equals("NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            parent = NativeMethods.GetParent(parent);
        }

        return false;
    }

    private static string GetClassName(nint hWnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hWnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static IReadOnlyList<BackgroundApplication> ReadToolbarApplications(nint toolbarHandle)
    {
        NativeMethods.GetWindowThreadProcessId(toolbarHandle, out var processId);
        if (processId == 0)
        {
            return [];
        }

        var processHandle = NativeMethods.OpenProcess(
            NativeMethods.ProcessVmOperation | NativeMethods.ProcessVmRead | NativeMethods.ProcessVmWrite,
            false,
            processId);
        if (processHandle == nint.Zero)
        {
            return [];
        }

        var remoteButton = nint.Zero;
        var remoteText = nint.Zero;
        try
        {
            var buttonCount = NativeMethods.SendMessage(toolbarHandle, NativeMethods.TbButtonCount, nint.Zero, nint.Zero).ToInt32();
            if (buttonCount <= 0)
            {
                return [];
            }

            var buttonSize = nint.Size == 8 ? 32 : 20;
            var imageListHandle = NativeMethods.SendMessage(toolbarHandle, NativeMethods.TbGetImageList, nint.Zero, nint.Zero);
            remoteButton = NativeMethods.VirtualAllocEx(processHandle, nint.Zero, (nuint)buttonSize, NativeMethods.MemCommit, NativeMethods.PageReadWrite);
            remoteText = NativeMethods.VirtualAllocEx(processHandle, nint.Zero, 1024, NativeMethods.MemCommit, NativeMethods.PageReadWrite);
            if (remoteButton == nint.Zero || remoteText == nint.Zero)
            {
                return [];
            }

            var apps = new List<BackgroundApplication>();
            for (var index = 0; index < buttonCount; index++)
            {
                if (NativeMethods.SendMessage(toolbarHandle, NativeMethods.TbGetButton, index, remoteButton) == nint.Zero)
                {
                    continue;
                }

                var buttonBytes = new byte[buttonSize];
                if (!NativeMethods.ReadProcessMemory(processHandle, remoteButton, buttonBytes, (nuint)buttonBytes.Length, out _))
                {
                    continue;
                }

                var imageIndex = BitConverter.ToInt32(buttonBytes, 0);
                var idCommand = BitConverter.ToInt32(buttonBytes, 4);
                var dwData = ReadNativeInt(buttonBytes, nint.Size == 8 ? 16 : 12);
                var text = ReadToolbarButtonText(toolbarHandle, processHandle, idCommand, remoteText);
                var executablePath = TryGetTrayExecutablePath(processHandle, dwData);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var app = new PinnedApp
                {
                    DisplayName = CleanTrayText(text, executablePath),
                    TargetPath = executablePath
                };
                apps.Add(new BackgroundApplication(app, TryExtractToolbarIcon(imageListHandle, imageIndex)));
            }

            return apps;
        }
        finally
        {
            if (remoteButton != nint.Zero)
            {
                NativeMethods.VirtualFreeEx(processHandle, remoteButton, 0, NativeMethods.MemRelease);
            }

            if (remoteText != nint.Zero)
            {
                NativeMethods.VirtualFreeEx(processHandle, remoteText, 0, NativeMethods.MemRelease);
            }

            NativeMethods.CloseHandle(processHandle);
        }
    }

    private static string ReadToolbarButtonText(nint toolbarHandle, nint processHandle, int idCommand, nint remoteText)
    {
        _ = NativeMethods.SendMessage(toolbarHandle, NativeMethods.TbGetButtonTextW, idCommand, remoteText);
        var buffer = new byte[1024];
        if (!NativeMethods.ReadProcessMemory(processHandle, remoteText, buffer, (nuint)buffer.Length, out _))
        {
            return string.Empty;
        }

        var text = Encoding.Unicode.GetString(buffer);
        var terminator = text.IndexOf('\0', StringComparison.Ordinal);
        return (terminator >= 0 ? text[..terminator] : text).Trim();
    }

    private static string TryGetTrayExecutablePath(nint processHandle, nint trayDataPointer)
    {
        if (trayDataPointer == nint.Zero)
        {
            return string.Empty;
        }

        var buffer = new byte[nint.Size];
        if (!NativeMethods.ReadProcessMemory(processHandle, trayDataPointer, buffer, (nuint)buffer.Length, out _))
        {
            return string.Empty;
        }

        var ownerWindow = ReadNativeInt(buffer, 0);
        if (ownerWindow == nint.Zero)
        {
            return string.Empty;
        }

        NativeMethods.GetWindowThreadProcessId(ownerWindow, out var ownerProcessId);
        if (ownerProcessId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)ownerProcessId);
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ImageSource? TryExtractToolbarIcon(nint imageListHandle, int imageIndex)
    {
        if (imageListHandle == nint.Zero || imageIndex < 0)
        {
            return null;
        }

        var iconHandle = NativeMethods.ImageList_GetIcon(imageListHandle, imageIndex, NativeMethods.IldTransparent);
        if (iconHandle == nint.Zero)
        {
            return null;
        }

        try
        {
            var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            image.Freeze();
            return image;
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }

    private static nint ReadNativeInt(byte[] bytes, int offset)
    {
        return nint.Size == 8
            ? new nint(BitConverter.ToInt64(bytes, offset))
            : new nint(BitConverter.ToInt32(bytes, offset));
    }

    private static string CleanTrayText(string text, string executablePath)
    {
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            return firstLine;
        }

        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        return string.IsNullOrWhiteSpace(fileName) ? "Tray icon" : fileName;
    }

}
