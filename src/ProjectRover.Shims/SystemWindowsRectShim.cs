using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace System.Windows
{
    // Minimal shim of System.Windows.Rect used by the ported code.
    // Provides constructor (x, y, width, height) and commonly used properties.
    public readonly struct Rect
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }

        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public override string ToString() => $"{X},{Y},{Width},{Height}";

        // Implicit conversions to/from Avalonia.Rect to ease interop in the ported code.
        public static implicit operator Avalonia.Rect(Rect r) => new(r.X, r.Y, r.Width, r.Height);
        public static implicit operator Rect(Avalonia.Rect r) => new(r.X, r.Y, r.Width, r.Height);
    }

    // Small wrapper for WindowState that converts to/from Avalonia.Controls.WindowState
    [TypeConverter(typeof(WindowStateTypeConverter))]
    public readonly struct WindowState : IEquatable<WindowState>
    {
        private readonly byte _value;
        private WindowState(byte v) => _value = v;

        public static readonly WindowState Normal = new(0);
        public static readonly WindowState Minimized = new(1);
        public static readonly WindowState Maximized = new(2);

        public override string ToString() => _value switch
        {
            1 => nameof(Minimized),
            2 => nameof(Maximized),
            _ => nameof(Normal)
        };

        public bool Equals(WindowState other) => _value == other._value;
        public override bool Equals(object? obj) => obj is WindowState other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();

        public static implicit operator Avalonia.Controls.WindowState(WindowState s)
            => s._value switch
            {
                1 => Avalonia.Controls.WindowState.Minimized,
                2 => Avalonia.Controls.WindowState.Maximized,
                _ => Avalonia.Controls.WindowState.Normal
            };

        public static implicit operator WindowState(Avalonia.Controls.WindowState s)
            => s switch
            {
                Avalonia.Controls.WindowState.Minimized => Minimized,
                Avalonia.Controls.WindowState.Maximized => Maximized,
                _ => Normal
            };

        public static WindowState FromAvalonia(Avalonia.Controls.WindowState s) => s switch
        {
            Avalonia.Controls.WindowState.Minimized => Minimized,
            Avalonia.Controls.WindowState.Maximized => Maximized,
            _ => Normal
        };
    }

    class WindowStateTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
        {
            if (value is string s)
            {
                if (Enum.TryParse<Avalonia.Controls.WindowState>(s, true, out var avalonia))
                    return WindowState.FromAvalonia(avalonia);
                // fallback
                return WindowState.Normal;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is WindowState ws)
                return ws.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    
}
