## Go-To / Find Usages Integration (ILSpyX mapping)

This document records the current implementation status, observed gaps compared to ILSpy WPF, and an incremental plan to reach parity for Go-To/Find Usages behavior in Rover (Avalonia).

Summary
- Implemented: basic search adapters, analyzer-based reference discovery, a navigation service, and synchronous assembly loading + indexing.
- Missing: robust token+assembly identity resolution, resolver-based referenced-assembly loading with prompt/auto-load UX, async navigation orchestration with progress, expanded indexing, and fallbacks.

Key files
- `src/ProjectRover/Services/Navigation/NavigationService.cs` — resolves `BasicSearchResult` to `Node` (sync). 
- `src/ProjectRover/ViewModels/AssemblyTreeModel.cs` — indexing, `ResolveNode`, `LoadAssemblies`, token -> node mapping.
- `src/ProjectRover/Services/IlSpyBackend.cs` — `AnalyzeSymbolReferences`, `DecompileMember`, ILSpyX `AssemblyList` integration.
- `src/ProjectRover/ViewModels/MainWindowViewModel.cs` — search pipeline, `FindUsages()`, wiring to navigation.

Observed behavior (what works now)
- Search adapters produce `BasicSearchResult` objects that include `DisplayAssembly` and optional `TargetNode`.
- `NavigationService.ResolveSearchResultTarget` will reindex existing assemblies or load an assembly by path (synchronously) and then try to find a node.
- `FindUsages()` runs ILSpy analyzers via `IlSpyBackend.AnalyzeSymbolReferences` and maps returned (handle, displayName, assemblyPath) tuples into search results; resolved nodes are attached when possible.

Gaps vs ILSpy WPF (detailed)
1. Token + Assembly identity resolution
- Rover relies on `(assemblyPath, EntityHandle)` map and a string-kind fallback. ILSpy WPF uses an algorithm that prefers exact filePath+token, then consults candidate assemblies by name/MVID and tests token presence in each candidate's metadata before fallback.

2. Resolver-based referenced-assembly loading and prompt/auto-load UX
- Rover currently loads assemblies by file path when requested; it does not resolve by assembly identity or offer a prompt/auto-load preference when analyzers or references point to unloaded assemblies.

3. Async orchestration and progress
- Current operations are synchronous and may block UI during assembly load / indexing / analyzer runs. ILSpy WPF uses async `JumpToReferenceAsync` flows with progress and marshals UI updates to the UI thread.

4. Indexing breadth and lazy strategies
- Rover indexes types and members when building the tree but doesn't index exported/forwarded types or support lazy/incremental indexing for large assemblies.

5. Analyzer result handling for unloaded assemblies
- `FindUsages()` lists analyzer findings but does not attempt automatic resolution/loading of referenced assemblies, nor does it present a direct user action to load unresolved assemblies from results.

Recommended incremental plan
1. Add `NavigationService.JumpToReferenceAsync(...)` that performs async resolution, optional assembly loads, and selection with UI dispatcher marshalling.
2. Add `AssemblyTreeModel.LoadAssembliesAsync(...)` wrapper and make indexing async; ensure `IndexAssemblyHandles` can run off the UI thread and update maps on the UI thread.
3. Implement resolver shim that queries `AssemblyList` for candidate assemblies by identity (simple name / MVID), and expose `ResolveAssemblyCandidates(name) -> filePaths`.
4. Implement token probing algorithm: for each candidate file path, open metadata (PEFile) and call `GetDefinition(...)` for the given handle kind to confirm presence before loading into tree.
5. Add `AutoLoadReferencedAssemblies` setting and a simple prompt UX when a navigation would load external assemblies.
6. Improve `FindUsages()` UX: unresolved results should show an action to load referenced assembly or attempt background resolution and update results when resolved.
7. Add tests: unit/integration tests that compare navigation results for a set of assemblies with ILSpy WPF.

Notes and trade-offs
- Start with small, reversible steps: add async wrappers and a feature-flag for resolver-based auto-load. Keep `IlSpyBackend` as the canonical place for IO and ILSpyX calls.
- Prioritize token+assembly resolution and resolver-based auto-load — these give the most immediate user-visible improvements for Find Usages and Go-To.

If you'd like, I can start by implementing `NavigationService.JumpToReferenceAsync` (prototype) and `AssemblyTreeModel.LoadAssembliesAsync` so navigation becomes non-blocking and can attempt background resolution.