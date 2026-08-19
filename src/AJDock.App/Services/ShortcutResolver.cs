using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AJDock.App.Services;

public sealed class ShortcutResolver
{
    public ShortcutInfo Resolve(string shortcutPath)
    {
        if (!File.Exists(shortcutPath) || !shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return new ShortcutInfo(shortcutPath, string.Empty, null);
        }

        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)Activator.CreateInstance(typeof(ShellLink))!;
            ((IPersistFile)link).Load(shortcutPath, 0);

            var target = new StringBuilder(260);
            link.GetPath(target, target.Length, nint.Zero, 0);

            var arguments = new StringBuilder(1024);
            link.GetArguments(arguments, arguments.Length);

            var iconPath = new StringBuilder(260);
            link.GetIconLocation(iconPath, iconPath.Length, out _);

            var cleanIconPath = Clean(iconPath);
            return new ShortcutInfo(Clean(target), Clean(arguments), string.IsNullOrWhiteSpace(cleanIconPath) ? null : cleanIconPath);
        }
        catch
        {
            return new ShortcutInfo(shortcutPath, string.Empty, null);
        }
        finally
        {
            if (link is not null)
            {
                Marshal.FinalReleaseComObject(link);
            }
        }
    }

    private static string Clean(StringBuilder value)
    {
        var text = value.ToString();
        var index = text.IndexOf('\0', StringComparison.Ordinal);
        return (index >= 0 ? text[..index] : text).Trim();
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, nint pfd, uint fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(nint hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}

public sealed record ShortcutInfo(string TargetPath, string Arguments, string? IconPath);
