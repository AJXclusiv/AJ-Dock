using System.Globalization;
using System.Windows.Data;

namespace AJDock.App.Converters;

public sealed class ScaleValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double source)
        {
            return value;
        }

        var scale = 1d;
        if (parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            scale = parsed;
        }

        return source * scale;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
