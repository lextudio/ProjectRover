using ProjectRover.Settings;

namespace ProjectRover.Services;

public interface IRoverSettingsService
{
    RoverStartupSettings StartupSettings { get; }
    RoverSessionSettings SessionSettings { get; }
}
