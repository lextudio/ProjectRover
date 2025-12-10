using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ProjectRover.Converters;

public class ApiVisibilityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var grayBrush = TryGetBrush("GrayTextBrush") ?? Brushes.Gray;
        var normalBrush = TryGetBrush("ThemeForegroundBrush") ?? Brushes.Black;

        return value is bool isPublic && !isPublic
            ? grayBrush
            : normalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush? TryGetBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) == true)
        {
            return resource as IBrush;
        }

        return null;
    }
}

public class ThemeEqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is null)
            return false;

        return ReferenceEquals(value, parameter);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
