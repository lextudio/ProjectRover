using System;

namespace ILSpyRover.Options;

public sealed class StartupOptions
{
    public bool RestoreAssemblies { get; set; } = true;
    public string[] LastAssemblies { get; set; } = Array.Empty<string>();
}
