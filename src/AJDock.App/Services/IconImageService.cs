using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AJDock.App.Native;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class IconImageService
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private double _iconQuality = 0.8;

    public bool SetIconQuality(double iconQuality)
    {
        var normalized = Math.Clamp(iconQuality, 0.4, 1);
        if (Math.Abs(_iconQuality - normalized) < 0.01)
        {
            return false;
        }

        _iconQuality = normalized;
        _cache.Clear();
        return true;
    }

    public ImageSource GetIcon(PinnedApp app)
    {
        var iconPath = !string.IsNullOrWhiteSpace(app.CustomIconPath) && File.Exists(app.CustomIconPath)
            ? app.CustomIconPath
            : app.TargetPath;

        if (_cache.TryGetValue(iconPath, out var cached))
        {
            return cached;
        }

        var sourcePixelSize = GetSourcePixelSize();
        var image = TryLoadBitmap(iconPath, sourcePixelSize)
            ?? TryExtractShellItemImage(iconPath, sourcePixelSize)
            ?? TryExtractShellItemImage(iconPath, 1024)
            ?? TryExtractShellItemImage(iconPath, 768)
            ?? TryExtractShellItemImage(iconPath, 512)
            ?? TryExtractJumboShellIcon(iconPath)
            ?? TryExtractShellIcon(iconPath)
            ?? CreateFallbackIcon();
        image.Freeze();
        _cache[iconPath] = image;
        return image;
    }

    private int GetSourcePixelSize()
    {
        return Math.Clamp((int)Math.Round(2048 * _iconQuality), 512, 2048);
    }

    private static ImageSource? TryLoadBitmap(string path, int sourcePixelSize)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = sourcePixelSize;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryExtractShellIcon(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShFileInfo>(), NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon);
        if (result == nint.Zero || info.IconHandle == nint.Zero)
        {
            return null;
        }

        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                info.IconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(256, 256));
        }
        finally
        {
            NativeMethods.DestroyIcon(info.IconHandle);
        }
    }

    private static ImageSource? TryExtractShellItemImage(string path, int size)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var iid = typeof(NativeMethods.IShellItemImageFactory).GUID;
        if (NativeMethods.SHCreateItemFromParsingName(path, nint.Zero, ref iid, out var imageFactory) != 0)
        {
            return null;
        }

        var bitmapHandle = nint.Zero;
        try
        {
            var requestedSize = new NativeMethods.Size
            {
                Cx = size,
                Cy = size
            };
            var flags = NativeMethods.SiigbfBiggersizeok | NativeMethods.SiigbfIconOnly;
            if (imageFactory.GetImage(requestedSize, flags, out bitmapHandle) != 0 || bitmapHandle == nint.Zero)
            {
                return null;
            }

            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            if (bitmapHandle != nint.Zero)
            {
                NativeMethods.DeleteObject(bitmapHandle);
            }

            if (System.Runtime.InteropServices.Marshal.IsComObject(imageFactory))
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(imageFactory);
            }
        }
    }

    private static ImageSource? TryExtractJumboShellIcon(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShFileInfo>(), NativeMethods.ShgfiSysIconIndex);
        if (result == nint.Zero)
        {
            return null;
        }

        var iid = typeof(NativeMethods.IImageList).GUID;
        if (NativeMethods.SHGetImageList(NativeMethods.ShilJumbo, ref iid, out var imageList) != 0
            && NativeMethods.SHGetImageList(NativeMethods.ShilExtraLarge, ref iid, out imageList) != 0)
        {
            return null;
        }

        var iconHandle = nint.Zero;
        try
        {
            if (imageList.GetIcon(info.Icon, NativeMethods.IldTransparent, ref iconHandle) != 0 || iconHandle == nint.Zero)
            {
                return null;
            }

            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            if (iconHandle != nint.Zero)
            {
                NativeMethods.DestroyIcon(iconHandle);
            }
        }
    }

    private static ImageSource CreateFallbackIcon()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(36, 39, 45)), null, new Rect(0, 0, 64, 64), 14, 14);
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(120, 220, 255)), null, new Rect(16, 18, 32, 28));
        }

        return new DrawingImage(drawing);
    }
}
