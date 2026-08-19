using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AJDock.App.Native;
using AJDock.App.ViewModels;
using AJDock.Core.Models;

namespace AJDock.App.Services;

public sealed class WindowPreviewService
{
    public WindowPreviewItemViewModel CreatePreview(RunningAppInfo app, ImageSource fallbackIcon)
    {
        return new WindowPreviewItemViewModel(app, CaptureWindow(app.MainWindowHandle), fallbackIcon);
    }

    private static ImageSource? CaptureWindow(nint hWnd)
    {
        if (hWnd == nint.Zero || NativeMethods.IsIconic(hWnd))
        {
            return null;
        }

        if (!NativeMethods.GetWindowRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        var width = Math.Clamp(rect.Width, 120, 1920);
        var height = Math.Clamp(rect.Height, 80, 1080);

        var hdc = NativeMethods.CreateCompatibleDC(nint.Zero);
        if (hdc == nint.Zero)
        {
            return null;
        }

        var bitmapInfo = new NativeMethods.BitmapInfo
        {
            Header = new NativeMethods.BitmapInfoHeader
            {
                Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = NativeMethods.BiRgb
            }
        };

        var bitmapHandle = NativeMethods.CreateDIBSection(hdc, ref bitmapInfo, NativeMethods.DibRgbColors, out _, nint.Zero, 0);
        if (bitmapHandle == nint.Zero)
        {
            NativeMethods.DeleteDC(hdc);
            return null;
        }

        var oldObject = NativeMethods.SelectObject(hdc, bitmapHandle);
        try
        {
            if (!NativeMethods.PrintWindow(hWnd, hdc, NativeMethods.PwfRenderFullContent))
            {
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (oldObject != nint.Zero)
            {
                NativeMethods.SelectObject(hdc, oldObject);
            }

            NativeMethods.DeleteObject(bitmapHandle);
            NativeMethods.DeleteDC(hdc);
        }
    }
}
