using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ICSharpCode.ILSpy;

namespace ICSharpCode.ILSpy.Converters
{
    public class IconToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Images.LoadImage(value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
