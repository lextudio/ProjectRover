Linux Installation Troubleshooting
==================================

If somehow neither the following commands launch this tool on your Linux distribution,

``` sh
./ProjectRover
dotnet ProjectRover.dll
```

Then you are likely hitting certain .NET runtime packaging issues and you should switch to a custom .NET runtime installation outside of the package system (Snap or another). To do so, you might refer to the example below,

``` sh
# install to ~/.dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
chmod +x ~/dotnet-install.sh
~/dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet

# Use it to run the app
~/.dotnet/dotnet --info
~/.dotnet/dotnet ~/Downloads/ProjectRover-linux-x64/ProjectRover.dll
```

Enabling Categorized Logging (for shipped binaries)
===================================================

This guide explains how end users of Project Rover (the packaged binaries produced by CI) can enable or adjust categorized logging without rebuilding the app.

Where Project Rover reads its logging configuration
--------------------------------------------------
- Project Rover uses Serilog through `ICSharpCode.ILSpy.Util.RoverLog`.
- `RoverLog` reads `appsettings.json` and `appsettings.Development.json` (if present) from the application's working folder at startup and watches them for changes (`reloadOnChange: true`).
- The process also writes `projectrover.log` into the working directory (configured in `Program.cs`) so you can inspect persistent logs.

Simple, supported methods for shipped users
------------------------------------------
1. Edit the shipped `appsettings.json`
   - Locate your installation folder (the folder that contains the ProjectRover executable or the published files installed by the release archive).
   - Edit `appsettings.json` found in the same folder as the executable and add or modify the values under `Serilog:MinimumLevel:Override`.
   - Example: enable debug logging for the Main menu and keep DataGrid quiet by default:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "MainMenu": "Debug",
        "DataGrid": "Warning"
      }
    }
  }
}
```

2. Or create an `appsettings.Development.json` (recommended for temporary debugging)
   - Instead of editing the shipped `appsettings.json`, create a file named `appsettings.Development.json` in the same folder as the executable with only the overrides you need. This makes it easy to revert by removing the file.
   - Example `appsettings.Development.json` (only overrides shown):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "DataGrid": "Debug",
        "DecompilerTextView": "Debug"
      }
    }
  }
}
```

3. Where to put the file inside macOS `.app` bundles
   - If you are using the macOS `.app` bundle, locate the executable inside the bundle (right-click the app → "Show Package Contents").
   - Place `appsettings.Development.json` next to the actual executable (typically under `ProjectRover.app/Contents/MacOS/`) so Rover can find it at startup.

4. Where to put the file for Windows and Linux
   - Place `appsettings.Development.json` next to `ProjectRover.exe` (Windows) or the `ProjectRover` binary (Linux) in the folder you extracted from the release archive.

Confirming the changes
----------------------
- `RoverLog` watches config files with `reloadOnChange`, so in many cases you can edit `appsettings.Development.json` while the app is running and Rider will pick up changes automatically. If not, restart the app.
- Check the console output and the `projectrover.log` file in the same folder for messages and verify that the log level changes took effect (look for e.g. debug messages from categories you enabled).

Common pitfalls and troubleshooting
---------------------------------
- JSON syntax errors: If `appsettings.json` or `appsettings.Development.json` contain invalid JSON, configuration will be ignored. Use a JSON validator.
- File location: Make sure the JSON file is in the same folder as the running executable. If placed elsewhere, Rover won't pick it up.
- Permissions: Ensure the user account running Rover has read access to the JSON files and write access to the folder (needed for `projectrover.log`).
- macOS bundle layout: Many users edit the visible `.app` package in Finder — ensure the file ends up next to the native executable inside the bundle, not only in the top-level `.app` directory.
- Overriding ILSpy logs: Upstream ILSpy code logs under `ICSharpCode.ILSpy` and related categories; these are explicitly set to `Warning` by default to keep release logs quieter. Add overrides for `ICSharpCode.ILSpy` if you need more detail from upstream code.

If you want us to provide a pre-made `appsettings.Development.json` with a sensible set of debug categories for your platform (Windows / macOS / Linux), tell me which categories you want enabled and I will add the example file ready to ship alongside the release.

Available Log Categories
------------------------
Below are the logging categories currently used in Project Rover. You can use these names in `Serilog:MinimumLevel:Override` to control per-category verbosity.

- **App**: app startup and general lifecycle messages. See `src/ProjectRover/Program.cs` and `src/ProjectRover/App.axaml.cs`.
- **DataGrid**: DataGrid internal/scrolling diagnostics. See `src/ProjectRover/App.axaml.cs` where the DataGrid scroll diagnostics are wired.
- **Commands**: command execution and command wrapper traces. See `src/ProjectRover/Commands/CommandWrapper.cs`.
- **Theme**: theme command handling. See `src/ProjectRover/Commands/ThemeCommands.cs`.
- **ThemeManager**: theme management/shim traces. See `src/ProjectRover/Themes/ThemeManagerShim.cs`.
- **Dock**: docking workspace and tab management. See `src/ProjectRover/Docking/DockWorkspace.avalonia.cs`.
- **MainMenu**: main menu construction and native menu conversion (macOS) traces. See `src/ProjectRover/Controls/MainMenu.axaml.cs`.
- **Session**: main window / session lifecycle events. See `src/ProjectRover/MainWindow.axaml.cs`.
- **Updates**: update-checking and update service. See `src/ProjectRover/Updates/AppUpdateService.cs` and `src/ProjectRover/Updates/UpdateService.cs`.
- **Images**: image loading and themed icon handling. See `src/ProjectRover/Images/ImagesShim.cs`.
- **DecompilerTextView**: decompiler text view and editing UI traces. See `src/ProjectRover/TextView/DecompilerTextView.axaml.cs`.
- **ICSharpCode.ILSpy**: upstream ILSpy categories — set to `Warning` by default in the shipped config; add overrides if you need more detail from upstream modules.

OS-specific placement details (concrete steps)
---------------------------------------------
The precise place you must put `appsettings.json` or `appsettings.Development.json` depends on how you unpacked or installed the release. Below are step-by-step instructions for common packaging formats we produce in CI.

1) Windows (zip / publish folder)

- Typical layout when you extract the release archive (example `ProjectRover-windows-x64`):

  ProjectRover/\
    ProjectRover.exe
    appsettings.json
    projectrover.log
    other runtime files...

- To enable debug overrides, copy `appsettings.Development.json` into the same folder as `ProjectRover.exe`:

  PowerShell example:

  ```powershell
  # from folder containing your override file
  Copy-Item .\appsettings.Development.json -Destination 'C:\path\to\ProjectRover\'
  ```

- If you installed into `C:\Program Files\` or another system directory you may need to run PowerShell as Administrator to write files there.

2) macOS (.app bundle created by CI)

- CI produces a `.app` bundle which is archived into `ProjectRover-macos-x64.tar.gz` or similar. After expanding the tar, you'll have `ProjectRover.app`.
- The actual native executable lives inside the bundle at `ProjectRover.app/Contents/MacOS/ProjectRover`.
- Place `appsettings.Development.json` next to that executable. Example commands (run from the folder that contains `ProjectRover.app`):

  ```bash
  # copy override into the bundle
  cp appsettings.Development.json ProjectRover.app/Contents/MacOS/

  # verify
  ls -l ProjectRover.app/Contents/MacOS/appsettings.Development.json
  ```

- If you prefer not to modify the bundle, you can create a small wrapper script that sets the working directory and launches the app; however adding the JSON inside `Contents/MacOS/` is the simplest approach for ad-hoc debugging.

3) Linux (tar archive / AppImage / extracted folder)

- If you unpacked `ProjectRover-linux-x64.tar.gz` you will typically have a folder with the `ProjectRover` binary and `appsettings.json` beside it.
- Copy `appsettings.Development.json` into that folder:

  ```bash
  cp appsettings.Development.json /path/to/ProjectRover-folder/
  ```

- If you installed system-wide (for example under `/opt/ProjectRover`), you may require sudo privileges to copy files there.

4) Verifying the file is used by the running app

- After placing the file, either restart Project Rover or (if running and the platform supports it) wait briefly – `RoverLog` uses `reloadOnChange` and usually detects config edits automatically.
- Check `projectrover.log` (in the same folder as the executable) or the console where you started the app — you should see additional debug messages from categories you enabled.
- If no change appears, check for JSON syntax errors (the app will silently ignore invalid JSON) and file permissions.

If you want, I can also add a small `appsettings.Development.json.example` into `src/ProjectRover` and update the CI packaging to include it next to published artifacts so end users get a ready-made file with instructions.*** End Patch
