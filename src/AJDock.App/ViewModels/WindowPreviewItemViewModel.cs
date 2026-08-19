using System.Windows.Media;
using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class WindowPreviewItemViewModel
{
    public WindowPreviewItemViewModel(RunningAppInfo app, ImageSource? previewImage, ImageSource fallbackIcon)
    {
        App = app;
        PreviewImage = previewImage;
        FallbackIcon = fallbackIcon;
    }

    public RunningAppInfo App { get; }
    public ImageSource? PreviewImage { get; }
    public ImageSource FallbackIcon { get; }
    public string Title => string.IsNullOrWhiteSpace(App.DisplayName) ? "Window" : App.DisplayName;
    public bool HasPreviewImage => PreviewImage is not null;
}
