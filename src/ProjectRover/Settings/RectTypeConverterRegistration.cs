// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
