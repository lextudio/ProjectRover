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
