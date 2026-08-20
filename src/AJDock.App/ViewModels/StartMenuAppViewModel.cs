using System.Windows.Media;
using AJDock.Core.Models;

namespace AJDock.App.ViewModels;

public sealed class StartMenuAppViewModel : ObservableObject
{
    private ImageSource _icon;

    public StartMenuAppViewModel(PinnedApp app, ImageSource icon)
    {
        App = app;
        _icon = icon;
    }

    public PinnedApp App { get; }
    public ImageSource Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string DisplayName => App.DisplayName;
}
