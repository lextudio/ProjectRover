namespace ICSharpCode.ILSpy;

internal static class App
{
    public static ProjectRover.App.IExportProvider ExportProvider { get; set; }

    static App()
    {
        ExportProvider = ProjectRover.App.Current?.ExportProvider;
    }
}