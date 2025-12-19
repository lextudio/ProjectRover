using System;
using System.ComponentModel;

using Avalonia;

namespace ProjectRover.Settings
{
    /// <summary>
    /// Registers a TypeConverter for Avalonia.Rect so ILSpy SessionSettings can parse persisted window bounds without modifying ILSpy sources.
    /// </summary>
    internal static class RectTypeConverterRegistration
    {
        private static bool registered;

        public static void Ensure()
        {
            if (registered)
                return;

            // IMPORTANT: This must be done before any SessionSettings are loaded that contain Rect properties.
            TypeDescriptor.AddAttributes(typeof(Rect), new TypeConverterAttribute(typeof(AvaloniaRectConverter)));
            registered = true;
        }

        private sealed class AvaloniaRectConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
            {
                if (value is string s)
                {
                    return Rect.Parse(s);
                }

                return base.ConvertFrom(context, culture, value);
            }

            public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
            {
                return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
            }

            public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
            {
                if (destinationType == typeof(string) && value is Rect rect)
                {
                    return rect.ToString();
                }

                return base.ConvertTo(context, culture, value, destinationType);
            }
        }
    }
}
