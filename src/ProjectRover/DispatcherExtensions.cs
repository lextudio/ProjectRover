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
