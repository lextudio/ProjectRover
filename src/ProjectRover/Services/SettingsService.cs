using System.ComponentModel;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpyX.Settings;
using ProjectRover.Settings;

namespace ProjectRover.Services;

public sealed class SettingsService : SettingsServiceBase, ISettingsService
{
    public SettingsService()
        : base(LoadSettingsSafe())
    {
    }

    public RoverStartupSettings StartupSettings => GetSettings<RoverStartupSettings>();
    public RoverSessionSettings SessionSettings => GetSettings<RoverSessionSettings>();

    private static ILSpySettings LoadSettingsSafe()
    {
        if (ILSpySettings.SettingsFilePathProvider == null)
        {
            ILSpySettings.SettingsFilePathProvider = new ILSpySettingsFilePathProvider();
        }

        return ILSpySettings.Load();
    }

    protected override void Section_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        base.Section_PropertyChanged(sender, e);

        if (sender is RoverStartupSettings startupSection)
        {
            SpySettings.Update(root => SaveSection(startupSection, root));
        }
        else if (sender is RoverSessionSettings sessionSection)
        {
            SpySettings.Update(root => SaveSection(sessionSection, root));
        }
    }
}
