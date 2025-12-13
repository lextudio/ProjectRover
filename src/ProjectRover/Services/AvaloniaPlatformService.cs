using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia;
using ICSharpCode.ILSpy;

namespace ProjectRover.Services
{
    public class AvaloniaPlatformService : IPlatformService
    {
        public ICSharpCode.ILSpy.Docking.IDockWorkspace? DockWorkspace { get; private set; }

        public AvaloniaDockWorkspace? AvaloniaDock { get; }

        public AvaloniaPlatformService()
        {
        }

        public AvaloniaPlatformService(ICSharpCode.ILSpy.Docking.IDockWorkspace dockWorkspace)
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
    }
}
