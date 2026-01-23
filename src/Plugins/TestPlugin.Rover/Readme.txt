TestPlugin.Rover

This is a minimal Rover-compatible test plugin.

Build and deploy
- Build: `dotnet build -c Release`
- Copy the output assembly `Test.Rover.Plugin.dll` from `bin/Release/net10.0/` to the Rover executable folder (next to the ILSpy/Rover exe). Rover will load `*.Plugin.dll` files at startup.

Notes
- This plugin intentionally has no WPF references so it is compatible with the Avalonia-based Rover host.
