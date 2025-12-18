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

    // Minimal stubs to allow ILSpy.Shims to compile when WPF types are referenced.
    public enum MessageBoxButton { OK, OKCancel, YesNo, YesNoCancel }
    public enum MessageBoxImage { None, Hand, Question, Exclamation, Asterisk, Warning, Error, Information }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No }
    public enum MessageBoxOptions { None = 0 }

    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            // shim: no UI — return OK by default
            return MessageBoxResult.OK;
        }
        public static MessageBoxResult Show(string messageBoxText)
        {
            return MessageBoxResult.OK;
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return MessageBoxResult.OK;
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return MessageBoxResult.OK;
        }

        public static MessageBoxResult Show(object owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        {
            return defaultResult;
        }

        public static MessageBoxResult Show(object owner, string messageBoxText, string caption, MessageBoxButton button)
        {
            return MessageBoxResult.OK;
        }

        // Added overloads returning bool for older call-sites
        public static bool ShowYesNo(string messageBoxText)
        {
            return true;
        }

        public static bool ShowYesNo(string messageBoxText, string caption)
        {
            return true;
        }

		internal static MessageBoxResult Show(string assemblySaveCodeDirectoryNotEmpty, string assemblySaveCodeDirectoryNotEmptyTitle, MessageBoxButton yesNo, MessageBoxImage question, MessageBoxResult no)
		{
			throw new NotImplementedException();
		}
	}

    public class Window
    {
        public Window? Owner { get; set; }

        public bool? ShowDialog()
        {
            return null;
        }
    }

    // Minimal shim for FontWeights used by ILSpy code.
    public readonly struct FontWeight
    {
        public int Weight { get; }
        public FontWeight(int weight) => Weight = weight;
    }

    public static class FontWeights
    {
        public static Avalonia.Media.FontWeight Normal { get; } = Avalonia.Media.FontWeight.Normal;
        public static Avalonia.Media.FontWeight Bold { get; } = Avalonia.Media.FontWeight.Bold;
    }
}
