using Microsoft.Win32;

namespace AJDock.App.Services;

public sealed class IconPickerService : IIconPickerService
{
    public string? PickIconPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose custom icon",
            Filter = "Icon and image files|*.ico;*.png;*.jpg;*.jpeg;*.bmp|Executables|*.exe|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
