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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectRover.Settings
{
    internal static class RecentFontsCache
    {
        private const int MaxRecentFonts = 8;
        private const string CacheDirectoryName = "ProjectRover";
        private const string CacheFileName = "RecentFonts.txt";

        public static IReadOnlyList<string> Load()
        {
            var path = GetCachePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Array.Empty<string>();

            try
            {
                return File.ReadAllLines(path)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static IReadOnlyList<string> Update(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                return Load();

            var list = Load().ToList();
            list.RemoveAll(item => string.Equals(item, fontName, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, fontName);
            if (list.Count > MaxRecentFonts)
                list.RemoveRange(MaxRecentFonts, list.Count - MaxRecentFonts);

            Save(list);
            return list;
        }

        private static void Save(IReadOnlyList<string> fonts)
        {
            var path = GetCachePath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllLines(path, fonts);
            }
            catch
            {
                // Best-effort cache; ignore IO failures.
            }
        }

        private static string? GetCachePath()
        {
            try
            {
                var baseDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(baseDir))
                    baseDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);

                if (string.IsNullOrWhiteSpace(baseDir))
                    return null;

                return Path.Combine(baseDir, CacheDirectoryName, CacheFileName);
            }
            catch
            {
                return null;
            }
        }
    }
}
