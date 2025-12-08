# ILSpy Rover

Avalonia 11 UI carried over from the Codemerx decompiler app, now wired to the ILSpy decompiler backend. ILSpy Rover keeps the modern Codemerx UI while gracefully replacing the JustDecompile engine with ILSpy.

## Layout
- `src/ILSpyRover` – Avalonia application and view models.
- `src/AvaloniaEdit` – bundled text editor control.
- `extern/ILSpy` – ILSpy source pulled in as a submodule for reference.

## Getting started
```bash
git submodule update --init --recursive
dotnet restore
dotnet build ILSpyRover.sln
```

The current UI opens assemblies and decompiles members via ILSpy's `CSharpDecompiler`. Project export and full-text search are stubbed out while the backend integration is completed.

## Next steps
- Flesh out ILSpy-backed search and navigation.
- Map ILSpy decompilation metadata back to the editor for go-to-definition and highlighting.
- Re-enable project generation once an ILSpy-friendly flow is designed.
