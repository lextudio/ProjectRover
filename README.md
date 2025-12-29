# Project Rover

Project Rover is a cross-platform desktop UI for ILSpy, focused on giving macOS and Linux users a first-class experience (Windows too). It is the successor to the deprecated AvaloniaILSpy and keeps pace with upstream ILSpy through mechanisms like `ICSharpCode.ILSpyX`. The UI foundation was initially based on the CodeMerx decompiler app and gradually adapted to view models and other supporting files from ILSpy WPF.

This project is currently maintained by LeXtudio Inc, and not affiliated with or endorsed by the ILSpy team.

![Project Rover banner](projectrover-social-v6.png)

## Quick start

Download the latest release archive for your platform from the Releases page, extract it, and run the app.

### Windows

1. Extract the archive.
2. Start `ProjectRover.exe`.

### Linux

1. Extract the archive, for example:
   ```bash
   mkdir ProjectRover && tar -xzpf ./ProjectRover-linux-x64.tar.gz -C ProjectRover
   ```
2. Start the app using `./ProjectRover/ProjectRover`.

### macOS

1. Extract the archive for your architecture:
   - ARM64: `tar -xzpf ./ProjectRover-macos-arm64.tar.gz`
   - x64: `tar -xzpf ./ProjectRover-macos-x64.tar.gz`
2. Remove the quarantine attribute because the app is not signed and macOS will otherwise block it:
   ```bash
   xattr -d com.apple.quarantine ProjectRover.app
   ```
3. Start `ProjectRover.app`.

## Build from source

.NET 10 SDK is required. Then run:

```bash
git clone https://github.com/LeXtudio/ProjectRover.git
cd ProjectRover
git submodule update --init --recursive
cd src/ProjectRover
dotnet run
```

## Layout

- `src/ProjectRover` – Avalonia application, shims for view models and other supporting files.
- `src/AvaloniaEdit` – bundled text editor control.
- `src/ILSpy` - Original ILSpy source code, mostly unmodified.

## Status

- Active development, not production ready. Expect bugs and missing features.
- Current focus is on code reuse and keeping up with ILSpy.

## License

This project is [AGPL](COPYING) licensed. It depends on ILSpy (MIT); see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).

## Enabling Categorized Rover Logging

Please read [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for step-by-step instructions for shipped users and other debugging tips.
