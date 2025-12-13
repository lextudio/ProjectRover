using System;
using TomsToolbox.Composition;

namespace ICSharpCode.ILSpy;

internal static class App
{
    internal record ExceptionData(Exception Exception)
    {
        public string PluginName { get; init; }
    }

    public static IExportProvider ExportProvider { get; set; }

    static App()
    {
        ExportProvider = ProjectRover.App.Current?.ExportProvider;
    }
}