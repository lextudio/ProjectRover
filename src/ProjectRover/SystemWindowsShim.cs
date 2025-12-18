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

        // Minimal mock for DelegateCommand to allow ILSpy code to compile.
        // Minimal mock for DelegateCommand to allow ILSpy code to compile.
        public class DelegateCommand : ICommand
        {
            private readonly Action<object?>? _execute;
            private readonly Func<object?, bool>? _canExecute;

            // Support Action<object?>
            public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            // Support Action (parameterless)
            public DelegateCommand(Action execute, Func<bool>? canExecute = null)
            {
                _execute = execute != null ? new Action<object?>(_ => execute()) : null;
                _canExecute = canExecute != null ? new Func<object?, bool>(_ => canExecute()) : null;
            }

            // Support Func<bool> as first argument, Action as second (for ILSpy pattern)
            public DelegateCommand(Func<bool> canExecute, Action execute)
            {
                _canExecute = canExecute != null ? new Func<object?, bool>(_ => canExecute()) : null;
                _execute = execute != null ? new Action<object?>(_ => execute()) : null;
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
            public void Execute(object? parameter) => _execute?.Invoke(parameter);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }

        // Minimal generic DelegateCommand<T> for usages with type arguments
        public class DelegateCommand<T> : ICommand
        {
            private readonly Action<T?>? _execute;
            private readonly Func<T?, bool>? _canExecute;

            public DelegateCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
            public void Execute(object? parameter) => _execute?.Invoke((T?)parameter);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
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
        // Minimal overloads for ILSpy logic
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
            if (button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel)
                return MessageBoxResult.Yes;
            return MessageBoxResult.OK;
        }
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Show(messageBoxText, caption, button);
        }
        // 5-argument overload for ExtractPackageEntryContextMenuEntry, SaveCodeContextMenuEntry, etc.
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            // Return defaultResult for compatibility
            return defaultResult;
        }
        // 7-argument overload for ManageAssemblyListsViewModel
        public static MessageBoxResult Show(object owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        {
            return defaultResult;
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
