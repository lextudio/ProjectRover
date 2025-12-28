# ApiDiff - Public API manifest exporter

A small CLI that exports a "public API" manifest from a .NET assembly using Mono.Cecil and compares API manifests with configurable mappings.

## Overview

This tool helps you compare the public API surface of two assemblies (for example, an Avalonia DataGrid fork vs WPF PresentationFramework) by:

- Exporting the public types and members of an assembly into a JSON manifest.
- Comparing two manifests with namespace mappings and type aliases to reduce false positives.
- Canonicalizing signatures (stripping parameter names) and normalizing property-like members (treating instance properties and dependency/styled-property fields as equivalent when name+type match).

## Contents

- `ApiDiff` CLI (C# / .NET): exports manifests and performs comparisons.
- `configs/datagrid-mappings.json`: JSON file with `namespaceMappings` and `typeAliases` used to normalize type names between the two APIs.
- `manifests/` (recommended): store exported `.api.json` manifests here.
- `reports/`: comparator output files (per-type diffs and summaries).
- `tmp/`: helper scripts (PowerShell) to summarize and count lines.

## Quick usage

1. Build the tool (if you changed code):

```powershell
dotnet build tools/ApiDiff/ApiDiff.csproj
```

2. Export an assembly to a manifest:

```powershell
# Example: export Avalonia DataGrid
ApiDiff export thirdparty/Avalonia.Controls.DataGrid/artifacts/bin/Avalonia.Controls.DataGrid/debug_net8.0/Avalonia.Controls.DataGrid.dll tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.api.json

# Example: export PresentationFramework (use the .NET 10 reference assembly)
ApiDiff export "C:\\Program Files\\dotnet\\packs\\Microsoft.WindowsDesktop.App.Ref\\10.0.1\\ref\\net10.0\\PresentationFramework.dll" tools/ApiDiff/manifests/PresentationFramework.net10.ref.api.json
```

3. Compare two manifests (member-diff-related):

```powershell
ApiDiff member-diff-related \
  tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.api.json \
  tools/ApiDiff/manifests/PresentationFramework.api.json \
  DataGrid,DataGridRow,DataGridColumn \
  tools/ApiDiff/configs/datagrid-mappings.json --full
```

The comparison uses the `datagrid-mappings.json` config to normalize namespaces and types.

## Where to find the Avalonia DataGrid assembly in this repo

The repository contains built/test artifacts for Avalonia DataGrid. Look for one of these paths (choose the one matching your target framework):

- `thirdparty/Avalonia.Controls.DataGrid/artifacts/bin/Avalonia.Controls.DataGrid/debug_net8.0/Avalonia.Controls.DataGrid.dll`
- `thirdparty/Avalonia.Controls.DataGrid/artifacts/bin/Avalonia.Controls.DataGrid/debug_net6.0/Avalonia.Controls.DataGrid.dll`
- `src/ProjectRover/bin/Debug/net10.0/Avalonia.Controls.DataGrid.dll` (project-local copy)

Prefer the `debug_net8.0` or the runtime that matches how you built `ApiDiff` (net8.0) to avoid surprising type variations.

Recommended generated manifest filenames (cached under `tools/ApiDiff/manifests/`):

- For the .NET 10 WPF ref assembly we use in this repo/workflow:
  - `tools/ApiDiff/manifests/PresentationFramework.net10.ref.api.json` (generated from the ref DLL at `C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\<version>\ref\net10.0\PresentationFramework.dll`)
- For Avalonia DataGrid (match the build you export):
  - `tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.debug_net8.0.api.json` (generated from `thirdparty/Avalonia.Controls.DataGrid/artifacts/bin/Avalonia.Controls.DataGrid/debug_net8.0/Avalonia.Controls.DataGrid.dll`)

Caching note: these manifests are the canonical, reusable snapshots of the public API surface. Generate them once per build/target and keep them in `tools/ApiDiff/manifests/` so subsequent comparisons are fast and reproducible.

## Mapping config

Edit `tools/ApiDiff/configs/datagrid-mappings.json` to add:

- `namespaceMappings`: prefix-style rewrites, e.g. `Avalonia.Controls.Primitives => System.Windows.Controls.Primitives`.
- `typeAliases`: exact fully-qualified type name mappings, e.g. `Avalonia.Controls.DataGridColumn => System.Windows.Controls.DataGridColumn`.

These allow the comparator to treat similarly-named but differently-namespaced types as the same.

## Normalizations performed

- Strip parameter names from method signatures (so parameter name differences are ignored).
- Replace namespace prefixes based on `namespaceMappings`.
- Replace exact type names using `typeAliases`.
- Normalize property-like members: instance properties (e.g., `Type Name { get; set; }`) and styled/dependency property fields (e.g., `StyledProperty` or `DependencyProperty` fields) are canonicalized to a common property form when the underlying property name and type match.

## Reports & summary

- Per-type human-readable diff files are saved under `tools/ApiDiff/reports/`.
- A PowerShell helper `tools/ApiDiff/tmp/make-summary.ps1` compares two report files and emits `member-diff-summary.json` with top-level stats:
  - `RelatedTypes`, `ImprovedTypes`, `WorseTypes`, `Same`, `TotalDeltaAdded`, `TotalDeltaRemoved`.

## Iteration suggestions

- If the comparator reports many differences that are actually equivalent types inside generics, consider adding more `typeAliases` or implementing token-aware alias replacement.
- For property-vs-styled-property mismatches, the current property normalization should remove many false positives; re-run after export to measure improvements.

## Example workflow (copyable)

```powershell
# 1) Export Avalonia DataGrid manifest
ApiDiff export thirdparty/Avalonia.Controls.DataGrid/artifacts/bin/Avalonia.Controls.DataGrid/debug_net8.0/Avalonia.Controls.DataGrid.dll tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.api.json

# 2) Export PresentationFramework manifest (if you have it)
ApiDiff export C:\\path\\to\\PresentationFramework.dll tools/ApiDiff/manifests/PresentationFramework.api.json

# 3) Run the member diff
ApiDiff member-diff-related \
  tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.api.json \
  tools/ApiDiff/manifests/PresentationFramework.api.json \
  DataGrid,DataGridRow,DataGridColumn \
  tools/ApiDiff/configs/datagrid-mappings.json --full

# 4) Summarize (adjust report filenames as created)
powershell -File tools/ApiDiff/tmp/make-summary.ps1 tools/ApiDiff/reports/member-diff-config-full.txt tools/ApiDiff/reports/member-diff-config-2.txt
```

## Where outputs land

- Manifests: `tools/ApiDiff/manifests/` (recommended)
- Reports: `tools/ApiDiff/reports/`
- Summary: `tools/ApiDiff/reports/member-diff-summary.json` (via the helper script)

Exact manifest filenames used by the example workflow above:

- `tools/ApiDiff/manifests/PresentationFramework.net10.ref.api.json`
- `tools/ApiDiff/manifests/Avalonia.Controls.DataGrid.debug_net8.0.api.json`

## Next steps I can do for you

- Export the Avalonia DataGrid manifest and save it to `tools/ApiDiff/manifests/` (I can do that now).
- Export a PresentationFramework manifest (if you provide the path) and run a compare.
- Add token-level aliasing if you want deeper normalization.

---

If you want me to proceed with a live comparison, tell me which PresentationFramework target or path to use and I'll run the export & compare and report numeric results.
