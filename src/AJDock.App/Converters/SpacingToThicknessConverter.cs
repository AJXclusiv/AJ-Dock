using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AJDock.App.Converters;

public sealed class SpacingToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var spacing = value is double number ? number : 8;
        return new Thickness(spacing / 2, 0, spacing / 2, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Thickness thickness ? thickness.Left + thickness.Right : 8d;
    }
}
