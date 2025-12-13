using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System;
using System.Text;

namespace ICSharpCode.ILSpy
{
    public static class ExtensionMethods
    {
        internal static string? FormatExceptions(this IList<App.ExceptionData> exceptions)
        {
            if (exceptions.Count == 0)
                return null;

            string delimiter = $"-------------------------------------------------{Environment.NewLine}";

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

        public static IEnumerable<TSource> ExceptNullItems<TSource>(this IEnumerable<TSource?> source) where TSource : class
        {
            return source.Where(i => i != null)!;
        }

        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        {
            return string.IsNullOrEmpty(value);
        }
    }
}