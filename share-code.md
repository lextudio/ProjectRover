# Code Sharing Strategy: ILSpy (WPF) and ILSpyRover (Avalonia)

## Executive Summary

This document outlines a strategy to maximize code sharing between the existing ILSpy (WPF) application and the new ILSpyRover (Avalonia) application. The primary goal is to leverage the existing `ICSharpCode.ILSpyX` library, which already encapsulates much of the core application logic, and to propose architectural alignments that allow `ILSpyRover` to benefit from the maturity of the ILSpy codebase with minimal changes to the existing WPF project.

## Current State Analysis

### ILSpy (WPF)
*   **Architecture**: MVVM (using `TomsToolbox.Wpf` and `System.Composition`).
*   **Core Logic**: Heavily relies on `ICSharpCode.ILSpyX` for assembly management, settings, and tree view models.
*   **Decompilation**: Uses `ICSharpCode.Decompiler`.
*   **UI**: WPF-specific Views and Controls (e.g., `Docking`, `SharpTreeView`).

### ILSpyRover (Avalonia)
*   **Architecture**: MVVM (using `CommunityToolkit.Mvvm`).
*   **Core Logic**: Currently re-implements much of the assembly management and state handling logic found in `ILSpyX` (e.g., `IlSpyBackend`, custom `Node` classes).
*   **Decompilation**: Uses `ICSharpCode.Decompiler` directly.
*   **UI**: Avalonia-specific Views and Controls.

## Proposed Architecture

The proposed architecture centers on elevating `ICSharpCode.ILSpyX` as the shared "Application Logic" layer. `ILSpyRover` should be refactored to consume `ILSpyX` instead of re-implementing its features.

```mermaid
graph TD
    subgraph "UI Layer (Platform Specific)"
        WPF[ILSpy (WPF)]
        Avalonia[ILSpyRover (Avalonia)]
    end

    subgraph "Shared Layers"
        ViewModels[Shared ViewModels (Optional/Future)]
        ILSpyX[ICSharpCode.ILSpyX (App Logic)]
        Decompiler[ICSharpCode.Decompiler (Core Engine)]
    end

    WPF --> ILSpyX
    Avalonia --> ILSpyX
    ILSpyX --> Decompiler
    WPF --> Decompiler
    Avalonia --> Decompiler
```

## Action Plan

### 1. Adopt `ICSharpCode.ILSpyX` in ILSpyRover

This is the most impactful step. `ILSpyRover` currently duplicates logic for managing loaded assemblies, settings, and the tree structure.

*   **Assembly Management**: Replace `IlSpyBackend` and local assembly management in `MainWindowViewModel` with `ICSharpCode.ILSpyX.AssemblyListManager` and `ICSharpCode.ILSpyX.LoadedAssembly`.
    *   *Benefit*: Automatic handling of assembly lists, references, and loading contexts.
*   **Settings**: Adopt `ICSharpCode.ILSpyX.Settings.ISettingsProvider` and related interfaces.
    *   *Benefit*: Compatible settings format with ILSpy.
*   **Tree Nodes**: Evaluate using `ICSharpCode.ILSpyX.TreeView.SharpTreeNode` as the base for `ILSpyRover` nodes.
    *   *Benefit*: Reuse of tree building logic and hierarchy management.

### 2. Align ViewModel Concepts

While sharing actual ViewModel classes might be difficult due to different MVVM frameworks (`TomsToolbox` vs `CommunityToolkit.Mvvm`), we can share the *logic* they contain.

*   **Service Abstraction**: Ensure `ILSpyRover` implements services that `ILSpyX` expects (if any), or uses the services provided by `ILSpyX`.
*   **Search Logic**: `ILSpyX` contains search abstractions. `ILSpyRover`'s `SearchService` should implement or utilize these to ensure consistent search behavior.

### 3. Shared "Core" UI Logic (Future)

If `ILSpyX` does not cover all platform-agnostic UI logic, consider creating a new .NET Standard library (e.g., `ICSharpCode.ILSpy.ViewModels`) that depends on `CommunityToolkit.Mvvm`.

*   **Refactoring**: Move pure logic from ILSpy WPF ViewModels to this new library.
*   **Adoption**: `ILSpyRover` can use these ViewModels directly. `ILSpy` (WPF) can eventually migrate to them or wrap them.
*   *Note*: This requires changes to ILSpy WPF, so it is a lower priority per the "minimal changes" constraint.

## Specific Component Recommendations

| Component | ILSpy (WPF) | ILSpyRover (Avalonia) | Recommendation |
| :--- | :--- | :--- | :--- |
| **Decompiler Engine** | `ICSharpCode.Decompiler` | `ICSharpCode.Decompiler` | **Keep**. Already shared. |
| **Assembly List** | `ILSpyX.AssemblyListManager` | Custom `Dictionary<AssemblyNode, ...>` | **Refactor Rover** to use `ILSpyX.AssemblyListManager`. |
| **Tree Model** | `ILSpyX.SharpTreeNode` | Custom `Node` class | **Refactor Rover** to wrap or inherit `SharpTreeNode`. |
| **Settings** | `ILSpyX.Settings` | Custom JSON handling | **Refactor Rover** to use `ILSpyX` settings infrastructure. |
| **Search** | `ILSpy.Search` (WPF) | `SearchService` | **Extract** core search logic to `ILSpyX` if not present, then consume in Rover. |
| **MVVM Framework** | `TomsToolbox` | `CommunityToolkit.Mvvm` | **Keep Separate** for now. Focus on sharing logic via `ILSpyX`. |

## Conclusion

The immediate path forward is to refactor `ILSpyRover` to become a consumer of `ICSharpCode.ILSpyX`. This will delete a significant amount of duplicate "plumbing" code in `ILSpyRover` and ensure that both applications share the same behavior for assembly loading, management, and navigation.

## Action Items
- [x] Add ILSpyX as a project reference in Rover when the ILSpy repo is present, and route assembly loading through `AssemblyListManager`/`AssemblyList`.
- [x] Replace Rover’s custom tree-building (`Node` classes, `BuildTypes`, etc.) with adapters over ILSpyX `SharpTreeNode`/`LoadedAssembly` so the tree structure matches ILSpy.
- [x] Swap Rover’s search pipeline for ILSpyX’s search providers/results to align filtering (types/members/resources/assemblies) and formatting.
- [x] Move settings/startup state to `ILSpySettings` (or a Rover wrapper) to share list/history/config with ILSpy and reduce bespoke JSON/options.
- [ ] Decompilation/navigation: delegate to ILSpyX abstractions where available (language selection, tokens → nodes), keeping Avalonia UI rendering intact.
- [x] Adopt ILSpyX code mapping for editor references (token → tree node) to unify “Go To Definition” behavior with WPF ILSpy, including auto-loaded dependencies.
- [x] Reuse ILSpyX visibility/show flags (e.g., internal/CG members) instead of parallel Rover options so filtering matches WPF ILSpy.
- [x] Port ILSpyX reference-highlighting (hyperlink segments/tooltips) into the Avalonia editor to mirror ILSpy UX.
- [ ] Integrate ILSpyX analyzers/usage search where feasible to close feature parity gaps beyond decompilation and basic search.
- [ ] Audit Rover-only features added pre-ILSpyX adoption and re-enable or reimplement them on top of ILSpyX (e.g., ordering rules, icon variants, class/member grouping); drop only if ILSpyX provides an equivalent.
- [x] Preserve pre-ILSpyX UX polish (column sizing, tree ordering, constructor display, static/instance grouping) when consuming ILSpyX outputs; document any intentional deviations.
- [x] Align settings UI with ILSpyX sections (recent files/lists, language options) so both apps read/write the same settings nodes without duplication.
- [x] Reuse ILSpyX resource/reference explorers where possible, instead of bespoke resource viewers, to keep behavior consistent.
- [x] Validate icon theming (light/dark) matches ILSpy’s Visual Studio image library usage and avoid divergent resource keys.
- [x] Mirror WPF resource viewers by expanding `.resources`/`.resx` entries into child nodes with per-entry save options so Rover can preview and extract individual entries.
