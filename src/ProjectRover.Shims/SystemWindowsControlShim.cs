using Avalonia.VisualTree;
using Avalonia;

namespace System.Windows.Controls
{
    /// <summary>
    /// Avalonia replacement for WPF visual tree helper FindVisualChild used by linked ILSpy metadata code.
    /// </summary>
    internal static class ExtensionMethodsAvalonia
    {
        public static T? FindVisualChild<T>(this Visual? parent) where T : class
        {
            if (parent == null)
                return null;
            foreach (var child in parent.GetVisualChildren())
            {
                if (child is T match)
                    return match;
                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }
            return null;
        }
    }
}
