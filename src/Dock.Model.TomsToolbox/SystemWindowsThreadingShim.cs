using System;
using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Threading;

namespace System.Windows.Threading
{
    public enum DispatcherPriority
    {
        Background,
        Normal,
        Loaded,
        // minimal set used by code
    }

    public class Dispatcher
    {
        // BeginInvoke overload used in code: BeginInvoke(DispatcherPriority, Action)
        public void BeginInvoke(DispatcherPriority priority, Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback, Translate(priority));
        }

        public void BeginInvoke(DispatcherPriority priority, Delegate method)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => method.DynamicInvoke(), Translate(priority));
        }

        // // Support Task-returning delegates (async methods) passed to BeginInvoke
        // public void BeginInvoke(DispatcherPriority priority, Func<Task> callback)
        // {
        //     // Post a fire-and-forget wrapper to the UI thread; preserve priority translation
        //     Avalonia.Threading.Dispatcher.UIThread.Post(async () => { await callback().ConfigureAwait(false); }, Translate(priority));
        // }

        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();

		private static Avalonia.Threading.DispatcherPriority Translate(DispatcherPriority priority)
		{
            return priority switch
            {
                DispatcherPriority.Background => Avalonia.Threading.DispatcherPriority.Background,
                DispatcherPriority.Loaded => Avalonia.Threading.DispatcherPriority.Loaded,
                _ => Avalonia.Threading.DispatcherPriority.Normal,
            };
		}

		public void BeginInvoke(Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback);
        }

        // Support Task-returning delegates without explicit priority
        public void BeginInvoke(Func<Task> callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => { await callback().ConfigureAwait(false); });
        }

        public void Invoke(Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback);
        }

        public void Invoke(DispatcherPriority normal, Action action)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action, Translate(normal));
        }

        public async Task InvokeAsync(Action value, DispatcherPriority normal)
		{
			await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(value, Translate(normal));
		}

		public async Task InvokeAsync(Action value)
		{
			await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(value);
		}

		public void BeginInvoke(Action action, DispatcherPriority priority)
		{
			Avalonia.Threading.Dispatcher.UIThread.Post(action, Translate(priority));
		}

        public void VerifyAccess()
        {
            // TODO: implement if needed
        }
    }
}
