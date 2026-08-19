using System.Windows.Media;
using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class HiddenTrayItemViewModel
{
    public HiddenTrayItemViewModel(PinnedApp app, ImageSource icon)
    {
        App = app;
        Icon = icon;
    }

    public PinnedApp App { get; }
    public ImageSource Icon { get; }
    public string DisplayName => App.DisplayName;
}
