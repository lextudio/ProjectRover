using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Windows.Input;

namespace System.Windows
{
    public static class Clipboard
    {
        public static void SetText(string text)
        {
            try
            {
                var provider = Microsoft.Win32.DialogHelper.GetTopLevel(null)?.Clipboard;
                if (provider != null)
                {
                    // Avalonia clipboard is async; run synchronously via Dispatcher if needed
                    Microsoft.Win32.DialogHelper.RunSync(provider.SetTextAsync(text));
                }
            }
            catch
            {
                // swallow clipboard errors
            }
        }

        public static string GetText()
        {
            try
            {
                var provider = Microsoft.Win32.DialogHelper.GetTopLevel(null)?.Clipboard;
                if (provider != null)
                {
                    return Microsoft.Win32.DialogHelper.RunSync(provider.GetTextAsync()) ?? string.Empty;
                }
            }
            catch { }
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
        private static TopLevel? GetTopLevelFromOwner(object? owner)
        {
            try
            {
                // Prefer explicit Avalonia Window owner if provided
                if (owner is Avalonia.Controls.Window w) return TopLevel.GetTopLevel(w);
                // If owner is our shim Window type, try to get the current application's main window
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    return TopLevel.GetTopLevel(desktop.MainWindow);
            }
            catch { }
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime ? TopLevel.GetTopLevel(((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow) : null;
        }

        private static async Task<MessageBoxResult> ShowDialogAsync(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, object? owner)
        {
            var top = GetTopLevelFromOwner(owner) ?? Microsoft.Win32.DialogHelper.GetTopLevel(null);

            var tcs = new TaskCompletionSource<MessageBoxResult>();

            var dlg = new Avalonia.Controls.Window
            {
                Title = caption ?? string.Empty,
                Width = 560,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(12),
                }
            };

            var panel = (StackPanel)dlg.Content!;
            var textBlock = new TextBlock
            {
                Text = messageBoxText ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4),
                MaxWidth = 520
            };
            panel.Children.Add(textBlock);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4),
            };

            void AddButton(string label, MessageBoxResult result)
            {
                var btn = new Button { Content = label, Margin = new Thickness(6, 0) };
                btn.Click += (_, __) =>
                {
                    if (!tcs.Task.IsCompleted) tcs.SetResult(result);
                    dlg.Close();
                };
                buttonsPanel.Children.Add(btn);
            }

            switch (button)
            {
                case MessageBoxButton.OK:
                    AddButton("OK", MessageBoxResult.OK);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("OK", MessageBoxResult.OK);
                    AddButton("Cancel", MessageBoxResult.Cancel);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("Yes", MessageBoxResult.Yes);
                    AddButton("No", MessageBoxResult.No);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("Yes", MessageBoxResult.Yes);
                    AddButton("No", MessageBoxResult.No);
                    AddButton("Cancel", MessageBoxResult.Cancel);
                    break;
                default:
                    AddButton("OK", MessageBoxResult.OK);
                    break;
            }

            panel.Children.Add(buttonsPanel);

            dlg.Closed += (_, __) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(defaultResult);
            };

            // Show the dialog non-modally; completion is driven by button clicks.
            dlg.Show();

            return await tcs.Task.ConfigureAwait(true);
        }

        // Synchronous wrappers used by ILSpy code
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK, null));
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK, null));
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.None, null));
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, caption, button, icon, MessageBoxResult.None, null));
        }

        // 5-argument overload
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, caption, button, icon, defaultResult, null));
        }

        // 7-argument overload supporting owner/object
        public static MessageBoxResult Show(object owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        {
            return Microsoft.Win32.DialogHelper.RunSync(ShowDialogAsync(messageBoxText, caption, button, icon, defaultResult, owner));
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
