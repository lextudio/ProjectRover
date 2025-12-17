using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia;
using ICSharpCode.ILSpy;
using Avalonia.Input.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Application = Avalonia.Application;

namespace ProjectRover.Services
{
    public class AvaloniaPlatformService
    {
        public DockWorkspace? DockWorkspace { get; private set; }

        public AvaloniaPlatformService()
        {
        }

        public AvaloniaPlatformService(ICSharpCode.ILSpy.Docking.DockWorkspace dockWorkspace)
        {
            DockWorkspace = dockWorkspace;
        }
        public void InvokeOnUI(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        public Task InvokeOnUIAsync(Func<Task> action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                return action();
            var tcs = new TaskCompletionSource<object?>();
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await action().ConfigureAwait(false);
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public bool TryFindResource(object key, out object? value)
        {
            if (Application.Current == null)
            {
                value = null;
                return false;
            }

            // Avalonia's resource lookup exposes TryGetValue on Resources
            if (Application.Current.Resources?.TryGetValue(key, out var v) == true)
            {
                value = v;
                return true;
            }

            value = null;
            return false;
        }

        public async Task SetTextClipboardAsync(string text)
        {
            var clipboard = GetClipboard();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private static IClipboard? GetClipboard()
        {
            var app = Application.Current;
            if (app == null)
                return null;

            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
                return window.Clipboard;

            if (app.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
            {
                var root = mainView.GetVisualRoot() as TopLevel;
                return root?.Clipboard;
            }

            return null;
        }
    }
}
