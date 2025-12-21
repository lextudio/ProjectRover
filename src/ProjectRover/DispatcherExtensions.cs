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
                dispatcher.Post(action, Dispatcher.Translate(priority));
                return Task.CompletedTask;
            }

            public void BeginInvoke(DispatcherPriority priority, Action action)
            {
                dispatcher.Post(action, Dispatcher.Translate(priority));
            }

            public void BeginInvoke(DispatcherPriority priority, Delegate action)
            {
                dispatcher.Post(() => action.DynamicInvoke(), Dispatcher.Translate(priority));
            }

            public void BeginInvoke(Action action)
            {
                dispatcher.Post(action);
            }
        }
    }
}
