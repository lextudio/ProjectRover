using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ICSharpCode.ILSpy.Controls
{
    public class CultureSelectionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && parameter is string p)
                return string.Equals(s, p, StringComparison.OrdinalIgnoreCase);
            if (value == null && parameter == null)
                return true;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return parameter as string;
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
