using System.ComponentModel;
using ICSharpCode.ILSpyX.Settings;
using ProjectRover.Settings;

namespace ProjectRover.Services;

public sealed class RoverSettingsService : SettingsServiceBase, IRoverSettingsService
{
    public RoverSettingsService()
        : base(LoadSettingsSafe())
    {
    }

    public RoverStartupSettings StartupSettings => GetSettings<RoverStartupSettings>();

    private static ILSpySettings LoadSettingsSafe()
    {
        if (ILSpySettings.SettingsFilePathProvider == null)
        {
            ILSpySettings.SettingsFilePathProvider = new RoverSettingsFilePathProvider();
        }

        return ILSpySettings.Load();
    }

    protected override void Section_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        base.Section_PropertyChanged(sender, e);

        if (sender is RoverStartupSettings section)
        {
            SpySettings.Update(root => SaveSection(section, root));
        }
    }
}
