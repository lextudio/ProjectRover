using System;

namespace System.Windows
{
    // Minimal shim for FontWeights used by ILSpy code.
    public readonly struct FontWeight
    {
        public int Weight { get; }
        public FontWeight(int weight) => Weight = weight;
    }

    public static class FontWeights
    {
        public static Avalonia.Media.FontWeight Normal { get; } = Avalonia.Media.FontWeight.Normal;
        public static Avalonia.Media.FontWeight Bold { get; } = Avalonia.Media.FontWeight.Bold;
    }
}
