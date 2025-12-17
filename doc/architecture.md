## **Code Sharing: ILSpy (WPF) ↔ ILSpyRover (Avalonia)**

Purpose: Document architecture, shared code, feature parity, current gaps, and prioritized epics/tasks to bring ILSpyRover to parity with ILSpy WPF while maximizing reuse of `ICSharpCode.ILSpyX`.

Scope: This file compares `ilspy/ILSpy` (WPF) to `ILSpyRover/src/ProjectRover` (Avalonia) and highlights shared libraries in the workspace (`ICSharpCode.Decompiler`, `AvaloniaEdit`).

---

## Quick summary

- WPF ILSpy: Mature WPF app with many platform-specific UI components and extensive user-facing features.
- ILSpyRover: Avalonia reimplementation focused on cross-platform UI; shares the decompiler engine and incrementally reuses `ICSharpCode.ILSpyX` adapters.

---

## 1) High-level Architecture Comparison

- ILSpy (WPF)
  - UI: WPF XAML, Dock panels, `SharpTreeNode`/SharpTreeView.
  - App logic: `ICSharpCode.ILSpyX` provides core behaviors, tree models and adapters.
  - Decompiler: `ICSharpCode.Decompiler` engine.

- ILSpyRover (Avalonia)
  - UI: Avalonia XAML, Dock.Avalonia, `AvaloniaEdit` (editor), Dock.Model.
  - App logic: `ProjectRover` viewmodels and `Node` hierarchy, progressively adopting `ICSharpCode.ILSpyX` adapters.
  - Decompiler: `ICSharpCode.Decompiler` (shared).

Shared pieces
- `ICSharpCode.Decompiler` (core engine)
- `ICSharpCode.ILSpyX` when available (Rover conditionally references it through `UsingLocalIlSpyX`)
- `AvaloniaEdit` (editor port)

---

## 2) Current Implementation Highlights

- `IlSpyBackend` — low-level ILSpy/ILSpyX interactions and decompilation wrappers (current responsibilities: load, decompile, analyzer invocation).
- `AssemblyTreeModel` — manages UI tree state, indexing of handles, and node lookup helpers (currently responsible for sync loading/indexing).
- `IlSpyXTreeAdapter` / `IlSpyXSearchAdapter` — adapters that convert ILSpyX `LoadedAssembly` and search providers into Rover `Node` and `SearchResult` objects.
- `NavigationService` — newly added small service that centralizes search-result resolution and assembly-load attempts. (Short-term shim before `AssemblyList`.)

---

## 3) Current Gaps & Next Steps (short-term prioritized)

1) `AssemblyList` and `LoadedAssembly` shim
- Status: Planned. Implement a small `AssemblyList` to centralize lifecycle, persistence, referenced-assembly resolution, and to enable async loads.

2) Resolver-based referenced-assembly loading
- Status: Not implemented. Important to match ILSpy behavior for references and Find Usages navigation.

3) Token/handle resolution robustness
- Status: Partial: Rover probes loaded assemblies but lacks the assembly-identity-first algorithm ILSpy uses. Plan to add token+MVID/name resolution and probe candidate assemblies.

4) Jump/Navigation orchestration
- Status: Not implemented. Plan: add `JumpToReferenceAsync` to `NavigationService` (or `AssemblyTreeModel`) that loads/indexes async, shows progress, and navigates.

5) Async loading and indexing
- Status: Not implemented. Plan: make assembly loads and indexing non-blocking and marshal tree updates to Avalonia dispatcher.

6) Node parity & lazy children
- Status: Partial. Continue migrating Rover nodes to thin adapters around `SharpTreeNode` where practical.

7) Settings & persistence parity
- Status: Partial. Continue mapping Rover settings to ILSpySettings for consistent user experience.

---

## 4) How to proceed safely

- Implement `AssemblyList` first as a compatibility shim. Keep it small and testable.
- Migrate `IlSpyBackend` surface to provide explicit `OpenAssembly` and `FindLoadedAssembly` APIs that can be reused by `AssemblyList`.
- Make `NavigationService.JumpToReferenceAsync` the orchestration point for Go-To/Find Usages navigation.
- Introduce async indexing with careful UI dispatcher marshaling.

---

References
- `docs/actions.md` for priorities and tasks.
- `src/ProjectRover/Services/Navigation/NavigationService.cs` (current small shim).
- `src/ProjectRover/ViewModels/AssemblyTreeModel.cs` (indexing and node-resolution logic).

