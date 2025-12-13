using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia;

namespace ICSharpCode.ILSpy
{
    // Minimal shim for Images used by ILSpy WPF code. Returns placeholders or nulls.
    public static class Images
    {
        // Common image keys used by ILSpy UI
        public static readonly Bitmap ViewCode = CreatePlaceholderBitmap(16, 16);

        private static Bitmap CreatePlaceholderBitmap(int width, int height)
        {
            // Create a minimal 1x1 transparent PNG as placeholder
            var pngData = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, // IDAT chunk
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, // IEND chunk
                0x42, 0x60, 0x82
            };
            return new Bitmap(new MemoryStream(pngData));
        }

        public static Bitmap? Load(object owner, string resourceName)
        {
            return CreatePlaceholderBitmap(16, 16); // Avalonia will not use WPF ImageSource; return null as placeholder.
        }
    }
}
