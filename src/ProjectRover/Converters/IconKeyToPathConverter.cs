using System;
using Avalonia;
using Avalonia.Data.Converters;

namespace ProjectRover.Converters;

public class IconKeyToPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string key && Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var path) == true)
        {
            return path;
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
