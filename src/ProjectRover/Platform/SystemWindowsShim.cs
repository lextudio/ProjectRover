
namespace System.Windows
{
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
}
