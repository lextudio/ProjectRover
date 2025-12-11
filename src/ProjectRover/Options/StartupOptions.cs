using System;

namespace ProjectRover.Options;

public sealed class StartupOptions
{
    public bool RestoreAssemblies { get; set; } = true;
    public bool UseDebugSymbols { get; set; } = true;
    public bool ApplyWinRtProjections { get; set; } = false;
    public bool ShowCompilerGeneratedMembers { get; set; } = false;
    public bool ShowInternalApi { get; set; } = false;
}
