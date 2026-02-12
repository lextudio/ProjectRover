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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Avalonia.Controls.Documents;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Util;
using ICSharpCode.ILSpyX;

using ProjectRover;

using TomsToolbox.Essentials;

namespace ICSharpCode.ILSpy
{
    public static class ExtensionMethods
    {
        internal static string? FormatExceptions(this IList<App.ExceptionData> exceptions)
        {
            if (exceptions.Count == 0)
                return null;

            string delimiter = $"-------------------------------------------------{System.Environment.NewLine}";

            return string.Join(delimiter, exceptions.Select(FormatException));
        }

        private static string FormatException(App.ExceptionData item)
        {
            var output = new StringBuilder();

            if (!item.PluginName.IsNullOrEmpty())
                output.AppendLine("Error(s) loading plugin: " + item.PluginName);

            if (item.Exception is System.Reflection.ReflectionTypeLoadException exception)
            {
                foreach (var ex in exception.LoaderExceptions.ExceptNullItems())
                {
                    output.AppendLine(ex.ToString());
                    output.AppendLine();
                }
            }
            else
            {
                output.AppendLine(item.Exception.ToString());
            }

            return output.ToString();
        }



        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

		public static ICompilation? GetTypeSystemWithCurrentOptionsOrNull(this MetadataFile file, SettingsService settingsService, LanguageVersion languageVersion)
		{
			var decompilerSettings = settingsService.DecompilerSettings.Clone();
			if (!Enum.TryParse(languageVersion?.Version, out Decompiler.CSharp.LanguageVersion csharpLanguageVersion))
				csharpLanguageVersion = Decompiler.CSharp.LanguageVersion.Latest;
			decompilerSettings.SetLanguageVersion(csharpLanguageVersion);
			return file
				.GetLoadedAssembly()
				.GetTypeSystemOrNull(DecompilerTypeSystem.GetOptions(decompilerSettings));
		}
	}

    public static class BoldExtensions
    {
        extension(Bold bold)
        {
            public Bold Add(Inline inline)
            {
                bold.Inlines.Add(inline);
                return bold;
            }
        }
    }
}
