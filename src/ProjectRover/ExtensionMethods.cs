using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

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
}

namespace ICSharpCode.ILSpy.Properties
{

}

namespace ICSharpCode.ILSpy.Analyzers
{

}

namespace ICSharpCode.ILSpy.Search
{

}

namespace System.Windows.Controls
{
    
}

namespace System.Windows.Controls.Primitives
{

}

namespace System.Windows.Data
{
    
}

namespace System.Windows.Media
{
    
}

namespace System.Windows.Navigation
{

}

namespace System.Windows.Documents
{
    
}

namespace System.Windows.Media.Animation
{
    
}

namespace ICSharpCode.AvalonEdit.Highlighting
{}

namespace ICSharpCode.AvalonEdit.Document
{
    
}

namespace ICSharpCode.AvalonEdit.Editing
{
    
}

namespace ICSharpCode.AvalonEdit.Folding
{
    
}

namespace ICSharpCode.AvalonEdit.Rendering
{
    
}
