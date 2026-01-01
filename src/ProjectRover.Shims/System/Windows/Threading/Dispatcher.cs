namespace System.Windows.Threading
{
    /// <summary>
    /// Defines priorities at which operations can be invoked via a <see cref="Dispatcher"/>.
    /// </summary>
    [CLSCompliant(true)]
    public enum DispatcherPriority
    {
        /// <summary>
        /// Specifies that the operation is not important and can be performed after other non-idle operations are completed.
        /// </summary>
        Background,
        /// <summary>
        /// Specifies that the operation has normal importance.
        /// </summary>
        Normal,
        /// <summary>
        /// Specifies that the operation is to be performed when the layout system has finished loading.
        /// </summary>
        Loaded,
        // minimal set used by code
    }

    /// <summary>
    /// Provides services for managing the queue of work items for a thread.
    /// </summary>
    public class Dispatcher
    {
        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(DispatcherPriority, Action)
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="callback"></param>
        public void BeginInvoke(DispatcherPriority priority, Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback, Translate(priority));
        }

        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(DispatcherPriority, Action)
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="callback"></param>
        public void BeginInvoke<T>(DispatcherPriority priority, Action<T> callback, T args)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => callback.DynamicInvoke(args), Translate(priority));
        }

        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(DispatcherPriority, Delegate)
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="method"></param>
        public void BeginInvoke(DispatcherPriority priority, Delegate method)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => method.DynamicInvoke(), Translate(priority));
        }

        /// <summary>
        /// Returns the <see cref="Dispatcher"/> for the thread currently executing.
        /// </summary>
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();

        /// <summary>
        /// Translates WPF DispatcherPriority to Avalonia DispatcherPriority.
        /// </summary>
        /// <param name="priority"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static Avalonia.Threading.DispatcherPriority Translate(DispatcherPriority priority)
        {
            return priority switch
            {
                DispatcherPriority.Background => Avalonia.Threading.DispatcherPriority.Background,
                DispatcherPriority.Loaded => Avalonia.Threading.DispatcherPriority.Loaded,
                _ => Avalonia.Threading.DispatcherPriority.Normal,
            };
        }

        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(Action)
        /// </summary>
        /// <param name="callback"></param>
        public void BeginInvoke(Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback);
        }

        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(Func&lt;Task&gt;)
        /// </summary>
        /// <param name="callback"></param>
        public void BeginInvoke(Func<Task> callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => { await callback().ConfigureAwait(false); });
        }

        /// <summary>
        /// Invoke overload used in code: Invoke(Action)
        /// </summary>
        public void Invoke(Action callback)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(callback);
        }

        /// <summary>
        /// Invoke overload used in code: Invoke(DispatcherPriority, Action)
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="action"></param>
        public void Invoke(DispatcherPriority normal, Action action)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action, Translate(normal));
        }

        /// <summary>
        /// InvokeAsync overload used in code: InvokeAsync(Action, DispatcherPriority)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public async Task InvokeAsync(Action value, DispatcherPriority normal)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(value, Translate(normal));
        }

        /// <summary>
        /// InvokeAsync overload used in code: InvokeAsync(Action)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task InvokeAsync(Action value)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(value);
        }

        /// <summary>
        /// BeginInvoke overload used in code: BeginInvoke(Action, DispatcherPriority)
        /// </summary>
        /// <param name="action"></param>
        /// <param name="priority"></param>
        public void BeginInvoke(Action action, DispatcherPriority priority)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action, Translate(priority));
        }

        /// <summary>
        /// Verifies that the calling thread is the thread associated with this Dispatcher.
        /// </summary>
        public void VerifyAccess()
        {
            Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
        }
    }
}