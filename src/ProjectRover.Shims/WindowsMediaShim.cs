using System;
using System.Collections.Generic;
using System.Linq;

namespace System.Windows.Media
{
    // Minimal shim for FontFamily used by DisplaySettingsViewModel.
    public class FontFamily
    {
        public string Source { get; }
        public string Name => Source;
        public FontFamily(string source) => Source = source;
        public override string ToString() => Source;
        public IEnumerable<Typeface> GetTypefaces() => new[] { new Typeface() };
        public static implicit operator Avalonia.Media.FontFamily(FontFamily f) => new Avalonia.Media.FontFamily(f?.Source ?? string.Empty);
    }

    public class Typeface {
        public bool TryGetGlyphTypeface(out GlyphTypeface glyph)
        {
            glyph = new GlyphTypeface();
            return true;
        }
    }

    public class GlyphTypeface { public bool Symbol => false; }

    public static class Fonts
    {
        // Return a small sample list to keep UI working.
        public static FontFamily[] SystemFontFamilies => new[] { new FontFamily("Segoe UI"), new FontFamily("Courier New") };
    }

    public class TypefaceInfo { }
}

// Note: MessageBox and other System.Windows shims are provided in SystemWindowsShim.cs
