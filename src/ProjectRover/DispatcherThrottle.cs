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
using System.Threading;
using Avalonia.Threading;

namespace ICSharpCode.ILSpy
{
    // DispatcherThrottle implementation that schedules a coalesced callback
    // via Avalonia's UI dispatcher.
    public sealed class DispatcherThrottle : IDisposable
    {
        private readonly Action callback;
        private readonly TimeSpan dueTime;
        private Timer? timer;
        private readonly object lockObj = new();

        public DispatcherThrottle(Action callback, int milliseconds = 100)
        {
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            this.dueTime = TimeSpan.FromMilliseconds(milliseconds);
        }

        public DispatcherThrottle(int priority, Action callback)
            : this(callback)
        {
            // priority argument is ignored; Avalonia DispatcherPriority mapping
            // is not required for current usage.
        }

        public DispatcherThrottle(Action callback, TimeSpan dueTime)
        {
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            this.dueTime = dueTime;
        }

        public void Start()
        {
            lock (lockObj)
            {
                timer?.Dispose();
                timer = new Timer(_ => OnTimer(), null, dueTime, Timeout.InfiniteTimeSpan);
            }
        }

        public void Stop()
        {
            lock (lockObj)
            {
                timer?.Dispose();
                timer = null;
            }
        }

        private void OnTimer()
        {
            try
            {
                // Post the callback to Avalonia's UI thread dispatcher.
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        callback();
                    }
                    catch
                    {
                        // swallow exceptions to mimic dispatcher invocation safety
                    }
                }, DispatcherPriority.Background);
            }
            catch
            {
                // swallow
            }
            finally
            {
                // ensure timer is cleared after firing once
                lock (lockObj)
                {
                    timer?.Dispose();
                    timer = null;
                }
            }
        }

        public void Tick()
        {
            OnTimer(); // TODO:
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
