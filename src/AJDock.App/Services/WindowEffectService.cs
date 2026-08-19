using System.Windows;
using System.Windows.Interop;
using AJDock.App.Native;

namespace AJDock.App.Services;

public sealed class WindowEffectService
{
    public void Apply(Window window, bool useBackdrop = true)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        RemoveNativeFrame(handle);

        var corner = (int)NativeMethods.DwmWindowCornerPreference.DoNotRound;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var borderColor = NativeMethods.DwmColorNone;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaBorderColor, ref borderColor, sizeof(int));

        var captionColor = NativeMethods.DwmColorNone;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaCaptionColor, ref captionColor, sizeof(int));

        if (!useBackdrop)
        {
            return;
        }

        var backdrop = (int)NativeMethods.DwmSystemBackdropType.TransientWindow;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaSystemBackdropType, ref backdrop, sizeof(int));

        var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    private static void RemoveNativeFrame(nint handle)
    {
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlStyle).ToInt64();
        style &= ~NativeMethods.WsCaption;
        style &= ~NativeMethods.WsBorder;
        style &= ~NativeMethods.WsDlgFrame;
        style &= ~NativeMethods.WsThickFrame;

        _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlStyle, new nint(style));
        _ = NativeMethods.SetWindowPos(
            handle,
            nint.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoZOrder
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpFrameChanged);
    }
}
