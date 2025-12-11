# Project Rover

Project Rover is a cross-platform desktop UI for ILSpy, focused on giving macOS and Linux users a first-class experience (Windows too). It is the successor to the deprecated AvaloniaILSpy and keeps pace with upstream ILSpy through `ICSharpCode.ILSpyX`. The UI foundation came from the CodeMerx decompiler app and is now maintained by LeXtudio Inc.

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
```bash
git submodule update --init --recursive
dotnet restore
dotnet build ProjectRover.sln
```

## Layout
- `src/ProjectRover` – Avalonia application and view models.
- `src/AvaloniaEdit` – bundled text editor control.

## Status
- Assemblies, tree navigation, and search are powered by ILSpyX/`ICSharpCode.Decompiler`.
- Project export is still disabled while we complete the ILSpyX-based pipeline.

## License
This project is [AGPL](COPYING) licensed. It depends on ILSpyX/`ICSharpCode.Decompiler` (MIT); see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
