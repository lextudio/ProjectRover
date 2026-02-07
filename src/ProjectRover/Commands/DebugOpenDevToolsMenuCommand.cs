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
using System.Composition;
using System.Windows.Input;
using ICSharpCode.ILSpy;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

#if DEBUG
[ExportMainMenuCommand(ParentMenuID = "_Help", Header = "Open DevTools", InputGestureText = "F12", MenuOrder = 1000)]
#endif
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
