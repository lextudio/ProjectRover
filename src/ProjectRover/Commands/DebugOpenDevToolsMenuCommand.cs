using System;
using System.Composition;
using System.Windows.Input;
using ICSharpCode.ILSpy;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

#if DEBUG
[ExportMainMenuCommand(ParentMenuID = "_Help", Header = "Open DevTools", InputGestureText = "F12", MenuOrder = 1000)]
public class DebugOpenDevToolsMenuCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        try
        {
            var lifetime = global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            if (lifetime == null)
                return;

            // Pick the active window (or first) and send a KeyDown/KeyUp pair for F12
            global::Avalonia.Controls.Window? target = null;
            foreach (var w in lifetime.Windows)
            {
                if (w.IsActive)
                {
                    target = w;
                    break;
                }
            }
            if (target == null && lifetime.Windows.Count > 0)
                target = lifetime.Windows[0];

            if (target == null)
                return;

            try
            {
                var content = target.Content as global::Avalonia.Controls.Control;
                var argsDown = new global::Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = global::Avalonia.Input.InputElement.KeyDownEvent,
                    Key = global::Avalonia.Input.Key.F12
                };
                content?.RaiseEvent(argsDown);

                var argsUp = new global::Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = global::Avalonia.Input.InputElement.KeyUpEvent,
                    Key = global::Avalonia.Input.Key.F12
                };
                content?.RaiseEvent(argsUp);
            }
            catch
            {
                // If direct raise on the content control failed, there's not a safe portable
                // fallback available here. Swallow errors silently in debug helper.
            }
        }
        catch { }
    }
}
#endif
