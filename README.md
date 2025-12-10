# Project Rover

Project Rover is a cross-platform desktop UI for ILSpy, focused on giving macOS and Linux users a first-class experience (Windows too). It succeeds the now-deprecated AvaloniaILSpy with native packaging and a modern shell. The UI foundation originated in the CodeMerx decompiler app.

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
The UI opens assemblies and decompiles members via ILSpy's `CSharpDecompiler`. Project export and full-text search are stubbed out while the backend integration is completed.

## License
This project is [AGPL](COPYING) licensed. It uses the ILSpy decompiler via the `ICSharpCode.Decompiler` NuGet package, which is MIT licensed.
