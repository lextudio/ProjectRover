# ILSpy Rover

Avalonia 11 UI carried over from the Codemerx decompiler app, now wired to the ILSpy decompiler backend. ILSpy Rover keeps the modern Codemerx UI while gracefully replacing the JustDecompile engine with ILSpy.

## Quick start

Download the latest release archive for your platform from the Releases page, extract it, and run the app.

### Windows
1. Extract the archive.
2. Start `ILSpyRover.exe`.

### Linux
1. Extract the archive, for example:
   ```bash
   mkdir ILSpyRover && tar -xzpf ./ILSpyRover-linux-x64.tar.gz -C ILSpyRover
   ```
2. Start the app using `./ILSpyRover/ILSpyRover`.

### macOS
1. Extract the archive for your architecture:
   - ARM64: `tar -xzpf ./ILSpyRover-macos-arm64.tar.gz`
   - x64: `tar -xzpf ./ILSpyRover-macos-x64.tar.gz`
2. Remove the quarantine attribute because the app is not signed and macOS will otherwise block it:
   ```bash
   xattr -d com.apple.quarantine ILSpyRover.app
   ```
3. Start `ILSpyRover.app`.

## Build from source
```bash
git submodule update --init --recursive
dotnet restore
dotnet build ILSpyRover.sln
```

## Layout
- `src/ILSpyRover` – Avalonia application and view models.
- `src/AvaloniaEdit` – bundled text editor control.
- `extern/ILSpy` – ILSpy source pulled in as a submodule for reference.

## Status
The UI opens assemblies and decompiles members via ILSpy's `CSharpDecompiler`. Project export and full-text search are stubbed out while the backend integration is completed.

## License
This project is [AGPL](COPYING) licensed. It includes [ILSpy](extern/ILSpy) and [Mono.Cecil](https://github.com/jbevain/cecil) which are MIT licensed.
