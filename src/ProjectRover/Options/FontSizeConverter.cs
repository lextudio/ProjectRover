using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ICSharpCode.ILSpy.Options;

public sealed class FontSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return Math.Round(d / 4 * 3).ToString(culture);

        if (value is int i)
            return i.ToString(culture);

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && double.TryParse(s, NumberStyles.Any, culture, out var d))
            return d * 4 / 3;

        return 11.0 * 4 / 3;
    }
}
