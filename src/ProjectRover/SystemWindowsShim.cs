using System.Windows.Input;

namespace System.Windows
{
    public static class Clipboard
    {
        public static void SetText(string text)
        {
            // TODO: implement cross-platform clipboard support if needed
        }

        public static string GetText()
        {
            return string.Empty;
        }
    }

    public static class ApplicationCommands
    {
        // Provide commonly used RoutedUICommands as placeholders
        public static readonly RoutedUICommand Save = new("Save", "Save", typeof(ApplicationCommands));
        public static readonly RoutedUICommand Open = new("Open", "Open", typeof(ApplicationCommands));
        public static readonly RoutedUICommand Close = new("Close", "Close", typeof(ApplicationCommands));
    }

    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
    
    public class RoutedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }
}
