// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
