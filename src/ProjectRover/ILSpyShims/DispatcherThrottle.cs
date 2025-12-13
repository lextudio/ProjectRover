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
