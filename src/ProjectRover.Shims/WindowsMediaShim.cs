using Avalonia.Media;

namespace System.Windows.Media
{
    public static class Fonts
    {
        // Prefer Avalonia's font manager to enumerate installed fonts on the current platform.
        public static FontFamily[] SystemFontFamilies {
            get {
                try {
                    var fm = FontManager.Current;
                    if (fm != null) {
                        try {
                            var names = fm.SystemFonts;
                            if (names != null)
                                return names.ToArray();
                        }
                        catch {
                            // If the Avalonia font manager call fails for any reason, fall back below.
                        }
                    }
                }
                catch {
                    // fall through to fallback
                }

                // Fallback small sample list to keep UI working when Avalonia font manager is unavailable.
                return new[] { new FontFamily("Segoe UI"), new FontFamily("Courier New") };
            }
        }
    }

    public static class FontFamilyExtensions
    {
        extension(FontFamily fontFamily)
        {
            public string Source => fontFamily.Name;

            public IEnumerable<Typeface> GetTypefaces()
            {
                return fontFamily.FamilyTypefaces;
            }
        }

        extension(Typeface typeface)
        {
            public bool TryGetGlyphTypeface(out IGlyphTypeface glyphTypeface)
            {
                if (typeface.GlyphTypeface != null)
                {
                    glyphTypeface = typeface.GlyphTypeface;
                    return true;
                }
                glyphTypeface = null;
                return false;
            }
        }

        extension(IGlyphTypeface glyphTypeface)
        {
            public bool Symbol {
                get {
                    return false; // No reliable way yet to determine this in Avalonia
                }
            }
        }
    }
}
