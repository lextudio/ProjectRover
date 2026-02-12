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
using System.Windows.Threading;

namespace ICSharpCode.ILSpy
{
    public static class DispatcherExtensions
    {
        extension(Avalonia.Threading.Dispatcher dispatcher)
        {
            public Task InvokeAsync(Action action, DispatcherPriority priority)
            {
                dispatcher.Post(action, Translate(priority));
                return Task.CompletedTask;
            }

            public void BeginInvoke(DispatcherPriority priority, Action action)
            {
                dispatcher.Post(action, Translate(priority));
            }

            public void BeginInvoke(DispatcherPriority priority, Delegate action)
            {
                dispatcher.Post(() => action.DynamicInvoke(), Translate(priority));
            }

            public void BeginInvoke(Action action)
            {
                dispatcher.Post(action);
            }

            private static Avalonia.Threading.DispatcherPriority Translate(DispatcherPriority priority)
            {
                return priority switch
                {
                    DispatcherPriority.Normal => Avalonia.Threading.DispatcherPriority.Normal,
                    DispatcherPriority.Background => Avalonia.Threading.DispatcherPriority.Background,
                    DispatcherPriority.Loaded => Avalonia.Threading.DispatcherPriority.Loaded,
                };
            }
        }
    }
}
