using ProjectRover.Settings;

namespace ProjectRover.Services;

public interface ISettingsService
{
    RoverStartupSettings StartupSettings { get; }
    RoverSessionSettings SessionSettings { get; }
}
