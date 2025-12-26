using System;
using System.Windows.Threading;

using ProjectRover.Services;

using TomsToolbox.Composition;

namespace ProjectRover
{
    public partial class App
    {
        public Dispatcher Dispatcher { get; } = new Dispatcher();

        internal record ExceptionData(Exception Exception)
        {
            public string PluginName { get; init; }
        }

        public Window MainWindow { get; set; } // TODO: proper shim

        public static void UnhandledException(Exception exception)
        {
            // TODO:
        }
    }
}

namespace ICSharpCode.ILSpy
{

    public static class Application
    {
        public static App Current { get; } = (App)Avalonia.Application.Current;
    }
}
