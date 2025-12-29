using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using System.Linq;
using System.Collections.Generic;
using ModifierKeys = Avalonia.Input.KeyModifiers;

namespace System.Windows.Input
{
    public class QueryCursorEventArgs
    {

    }

    public static class Cursors
    {
        public static Cursor Arrow { get; } = new Cursor(StandardCursorType.Arrow);
    }

    public static class Keyboard
    {
        public static FocusedElement FocusedElement => new FocusedElement();
    }

    public static class Mouse
    {
        public static MouseButtonState RightButton => MouseButtonState.Released; // TODO: implement properly
    }

    public enum MouseButtonState
    {
        Released,
        Pressed
    }

    public class FocusedElement
    {
        public IDisposable PreserveFocus() => PreserveFocus(true);

        public IDisposable PreserveFocus(bool preserve)
        {
            if (!preserve)
                return new DummyDisposable();
            return new FocusPreserver();
        }

        private class DummyDisposable : IDisposable
        {
            public void Dispose() { }
        }

        private class FocusPreserver : IDisposable
        {
            private readonly IInputElement? _previousFocus;

            public FocusPreserver()
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                    if (window != null)
                    {
                        _previousFocus = window.FocusManager?.GetFocusedElement();
                    }
                }
            }

            public void Dispose()
            {
                _previousFocus?.Focus();
            }
        }
    }

    public static class NavigationCommands
    {
        public static readonly RoutedCommand BrowseBack = Create("BrowseBack", new KeyGesture(Key.Left, ModifierKeys.Alt));
        public static readonly RoutedCommand BrowseForward = Create("BrowseForward", new KeyGesture(Key.Right, ModifierKeys.Alt));

        public static readonly RoutedCommand Refresh = Create("Refresh", new KeyGesture(Key.F5));
        public static readonly RoutedCommand Search = Create("Search", null); // ShowSearchCommand overrides gestures

        static RoutedCommand Create(string name, KeyGesture? defaultGesture)
        {
            var cmd = new RoutedCommand(name);
            if (defaultGesture != null)
            {
                cmd.InputGestures.Add(defaultGesture);
            }
            return cmd;
        }
    }

    // Very small RoutedCommand implementation for shimming purposes
    public class RoutedCommand : Avalonia.Labs.Input.RoutedCommand
    {
        // Expose an InputGestures collection to match WPF's RoutedCommand API
        public IList<KeyGesture> InputGestures { get; } = new List<KeyGesture>();

        public RoutedCommand() : base(string.Empty, new KeyGesture(Key.None)) { }
        public RoutedCommand(string name) : base(name, new KeyGesture(Key.None)) { }
    }

    // Minimal RoutedUICommand shim (WPF provides text/name/owner type overloads)
    public class RoutedUICommand : Avalonia.Labs.Input.RoutedCommand
    {
        public string Text { get; set; }

        // WPF provides InputGestures on RoutedUICommand as well
        public IList<KeyGesture> InputGestures { get; } = new List<KeyGesture>();

        public RoutedUICommand() : base(string.Empty, new KeyGesture(Key.None)) { }
        public RoutedUICommand(string text, string name, Type ownerType) : base(name, new KeyGesture(Key.None))
        {
            Text = text;
        }
    }
}
