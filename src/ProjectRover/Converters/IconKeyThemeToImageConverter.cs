using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ICSharpCode.ILSpy.Converters
{
    public class IconKeyThemeToImageConverter : IMultiValueConverter
    {
        // values[0] = iconKey (string)
        // values[1] = theme variant (ThemeVariant or string)
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count == 0) return null;

            var iconKey = values[0];
            if (iconKey is string s && string.IsNullOrWhiteSpace(s))
                return null;

            try
            {
                // Delegate to the Images shim which already handles theme-preferring logic.
                // Pass through the raw icon object (string key or composite icon) instead of forcing string.
                var img = ICSharpCode.ILSpy.Images.LoadImage(iconKey);
                return img as IImage;
            }
            catch
            {
                return null;
            }
        }

        // Not used
        public IList<object?>? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
