# Project Rover

Avalonia 11 UI carried over from the Codemerx decompiler app, now wired to the ILSpy decompiler backend. Project Rover keeps the modern Codemerx UI while gracefully replacing the JustDecompile engine with ILSpy.

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
- `extern/ILSpy` – ILSpy source pulled in as a submodule for reference.

## Status
The UI opens assemblies and decompiles members via ILSpy's `CSharpDecompiler`. Project export and full-text search are stubbed out while the backend integration is completed.

Currently, this project aims to replicate the core features of AvaloniaILSpy, with plans to expand functionality in the future.

## License
This project is [AGPL](COPYING) licensed. It includes [ILSpy](extern/ILSpy), which is MIT licensed.
