See [Find Usages Integration](navigation-find-usages.md) for details on Go-To/Find Usages behavior in Rover.
## Summary

This document tracks prioritized epics for bringing Project Rover closer to ILSpy WPF behavior. Below you'll find a concise feature matrix (what ILSpy WPF offers vs. Rover) followed by updated epics and task statuses based on a code review of the workspace.

Note: Project Rover reuses many ILSpy WPF view-models and supporting files directly (not just ILSpyX APIs). This gives Rover better parity for UX and behavior than a pure ILSpyX-only integration — many WPF view models, commands, tree nodes and helpers are linked into the Rover project and adapted for Avalonia where necessary.

---

## Feature Matrix (ILSpy WPF -> Project Rover)

- **Assembly list & lifecycle:** Implemented in Rover (uses ILSpy's AssemblyList/LoadedAssembly).
	- Evidence: AssemblyList and LoadedAssembly are present: [src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs](src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs#L40), [src/ILSpy/ICSharpCode.ILSpyX/LoadedAssembly.cs](src/ILSpy/ICSharpCode.ILSpyX/LoadedAssembly.cs#L56).
- **Resolver-based referenced-assembly loading & AutoLoad preference:** Partially implemented — Rover exposes `AutoLoadReferencedAssemblies` and ILSpy decompiler settings are wired, but some UX (prompts/confirmation) and deep integration remain to polish.
	- Evidence: Rover settings: [src/ProjectRover/Settings/RoverStartupSettings.cs](src/ProjectRover/Settings/RoverStartupSettings.cs#L1). Decompiler setting exists: [src/ILSpy/ICSharpCode.Decompiler/DecompilerSettings.cs](src/ILSpy/ICSharpCode.Decompiler/DecompilerSettings.cs#L2058).
- **Token + assembly identity resolution (EntityHandle -> tree node):** Largely implemented — ILSpy's Jump/resolve flow (token probing + DecompilerTypeSystem lookup) is present and used by Rover.
	- Evidence: Resolver + token probe in JumpToReferenceAsync: [src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs](src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs#L610) and token handling: [src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs](src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs#L624).
- **Jump-to / Navigation (JumpToReferenceAsync):** Implemented. Rover's text view uses MessageBus to request navigation and the assembly tree handles async resolution.
	- Evidence: Text view sends navigate message: [src/ProjectRover/TextView/DecompilerTextView.axaml.cs](src/ProjectRover/TextView/DecompilerTextView.axaml.cs#L1008), and navigation handler: [src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs](src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs#L597).
- **Token probing across candidate assemblies:** Implemented (probe by token before loading candidate assemblies).
	- Evidence: token probing in JumpToReferenceAsync and LoadedAssembly resolver logic: [src/ILSpy/ILSpy/EntityReference.cs](src/ILSpy/ILSpy/EntityReference.cs#L57) and [src/ILSpy/ICSharpCode.ILSpyX/LoadedAssembly.cs](src/ILSpy/ICSharpCode.ILSpyX/LoadedAssembly.cs#L594).
- **ResolveAssemblyCandidates helper (resolve by simple name/MVID):** Functionality available via `AssemblyList` APIs (Open/Load/AssemblyListManager), though naming may differ.
	- Evidence: [src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs](src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs#L266) and [src/ILSpy/ICSharpCode.ILSpyX/AssemblyListManager.cs](src/ILSpy/ICSharpCode.ILSpyX/AssemblyListManager.cs#L70).
- **Indexing (exported/forwarded types, module-level constructs, lazy indexing):** Partial — basic indexing and search are present, but expansion to exported/forwarded types and lazy strategies needs work.
 - **Indexing (exported/forwarded types, module-level constructs, lazy indexing):** Future (deferred) — basic indexing and search are present; expansion to exported/forwarded types and lazy strategies is deferred. (Deferred until ILSpy WPF is retired and Rover becomes the official UI.)
	- Evidence: Search pane uses AssemblyList and indexing: [src/ILSpy/ILSpy/Search/SearchPane.xaml.cs](src/ILSpy/ILSpy/Search/SearchPane.xaml.cs#L288).
- **Settings and session persistence mapping:** Mostly implemented — ILSpy settings file path provider is wired and Rover startup settings exist; some session persistence mapping remains to be fully validated.
	- Evidence: Settings file provider: [src/ProjectRover/Services/ILSpySettingsFilePathProvider.cs](src/ProjectRover/Services/ILSpySettingsFilePathProvider.cs#L1), Rover startup settings: [src/ProjectRover/Settings/RoverStartupSettings.cs](src/ProjectRover/Settings/RoverStartupSettings.cs#L1).
- **Reference highlighting in editor (AvaloniaEdit):** Partially implemented — Rover's `DecompilerTextView` includes local reference marks and jump behavior, but some ILSpy reference-highlighting behaviors are still TODO.
	- Evidence: local reference marks and jump: [src/ProjectRover/TextView/DecompilerTextView.axaml.cs](src/ProjectRover/TextView/DecompilerTextView.axaml.cs#L944).
- **Analyzers & exposure in UI:** ILSpy analyzers are included in the project files; Rover has analyzer tree nodes available but UI polish may be pending.
	- Evidence: Analyzers linked into ProjectRover project: [src/ProjectRover/ProjectRover.csproj](src/ProjectRover/ProjectRover.csproj#L91).
- **Resource node expansion (.resources/.resx):** Partial — resource node factories and handlers exist but the specific UX for expansion may need verifying.
	- Evidence: resource node interfaces linked: [src/ProjectRover/ProjectRover.csproj](src/ProjectRover/ProjectRover.csproj#L586).
- **Theming, icons, tree ordering:** Partially implemented — theme shims and some icon resources exist; consolidation and exact ILSpy parity remain work items.
	- Evidence: theme shim: [src/ProjectRover/Themes/ThemeManagerShim.cs](src/ProjectRover/Themes/ThemeManagerShim.cs#L1).
- **Plugin/add-in compatibility & shims:** Inventory and shims present (several ILSpy add-ins linked); basic compatibility implemented.
	- Evidence: add-ins inventory and shims referenced in actions and project links: [doc/actions.md] (this file) references and `src/ProjectRover/ProjectRover.csproj` includes ILSpy modules.

## Verified Status (checked against this workspace)

The following checklist is a concise, actionable verification of the feature-matrix items based on the current repository contents.

- [x] **Assembly list & lifecycle** — confirmed: `ICSharpCode.ILSpyX/AssemblyList.cs`, `LoadedAssembly.cs` present and linked.
- [x] **Resolver-based referenced-assembly loading & AutoLoad preference** — partially confirmed: settings and resolver hooks exist; UX prompts still pending.
- [x] **Token + assembly identity resolution (EntityHandle -> tree node)** — confirmed: `AssemblyTree/AssemblyTreeModel.cs` contains Jump/resolve flow.
- [x] **Jump-to / Navigation (JumpToReferenceAsync)** — confirmed: navigation messages and handlers present (`DecompilerTextView.axaml.cs`, `AssemblyTreeModel.cs`).
- [x] **Token probing across candidate assemblies** — confirmed: `EntityReference.cs` and `LoadedAssembly.cs` contain probing logic.
- [x] **ResolveAssemblyCandidates helper** — confirmed: `AssemblyList` APIs provide candidate resolution helpers.
- [ ] **Indexing (exported/forwarded types, lazy strategies)** — partial: search/indexing exists (`SearchPane.xaml.cs`) but advanced indexing features remain TODO.
- [x] **Settings and session persistence mapping** — mostly confirmed: `ILSpySettingsFilePathProvider` and `RoverStartupSettings` are implemented; full audit pending.
- [ ] **Reference highlighting in editor (AvaloniaEdit)** — partial: local marks and jump behavior exist, more ILSpy parity needed.
- [x] **Analyzers & exposure in UI** — confirmed: analyzer code is linked into the project; UI polish remains.
- [ ] **Resource node expansion (.resources/.resx)** — partial: handlers exist but UX verification needed.
- [ ] **Theming, icons, tree ordering** — partial: theme shims exist (`ThemeManagerShim.cs`), but consolidation and parity work remain.
- [x] **Plugin/add-in compatibility & shims** — confirmed: many add-ins and shims are present and inventoried in `ProjectRover.csproj`.

Notes: "confirmed" means the repository contains the linked source and evidence for the feature; "partial" means some implementation exists but further work or verification is required before marking fully complete.

---

## UI Parity — View / ViewModel Inventory

Below is a concise comparison of important ILSpy WPF views, controls and view-models and their Project Rover status. This focuses on what has been ported (or linked) vs. what still needs an Avalonia view/shim.

- **Counts (rough):**
	- **ILSpy WPF ViewModels found:** 9
	- **ViewModels linked into ProjectRover (compiled):** 8
	- **Avalonia view/view-model implementations present in ProjectRover:** 10+ (axaml/.avalonia.cs or adapted shims)
	- **Estimated ViewModel TODOs (no Avalonia view/port):** ~6
	- **Estimated View TODOs (WPF XAML templates/controls missing or shim-needed):** ~4

- **Representative per-file status:**
| Feature | ILSpy | Rover | Notes |
|---|:---:|:---:|---|
| MainWindowViewModel | ✓ | ✓ | `MainWindowViewModel.cs` ported/implemented in Rover (`src/ProjectRover/MainWindowViewModel.cs`). |
| CompareView (view) | ✓ | ✓ | Ported to Avalonia: `src/ProjectRover/Views/CompareView.axaml`. |
| DecompilerTextView (view) | ✓ | ✓ | Avalonia port present: `src/ProjectRover/TextView/DecompilerTextView.axaml`. |
| MetadataTableViews (view) | ✓ | ✓ | Ported: `src/ProjectRover/Metadata/MetadataTableViews.axaml`. |
| AnalyzerTreeView (view) | ✓ | Partial | ILSpy has XAML; Rover uses `src/ProjectRover/Analyzers/AnalyzerTreeView.cs` (C# shim). |
| AnalyzerTreeViewModel | ✓ | ✓ | ViewModel linked into Rover (`Analyzers/AnalyzerTreeViewModel.cs`). |
| ManageAssemblyListsViewModel | ✓ | Partial | ViewModel linked; `ManageAssemblyListsDialog.axaml` exists in Rover. |
| UpdatePanelViewModel | ✓ | ✓ | Linked + `UpdatePanel.axaml` in Rover. |
| OptionsDialogViewModel / Settings VMs | ✓ | ✓ | Settings VMs and Avalonia options panels are present and wired via DataTemplates (OptionsDialog.axaml / OptionsDialog.axaml.cs). |
| ZoomScrollViewer (control template) | ✓ | ✗ | WPF control/template not ported; needs Avalonia replacement or shim. |
| SharpTreeView (control + templates) | ✓ | Partial | `SharpTreeViewShim.cs` exists; full templates and automation peers not ported. |
| SortableGridViewColumn / GridView helpers | ✓ | ✗ | WPF GridView helpers need Avalonia equivalents. |
| CollapsiblePanel | ✓ | ✗ | WPF control; missing in Rover (TODO). |

**Notes:**
- Table rows map a conceptual feature (view/viewmodel/control) to its presence in ILSpy WPF and ProjectRover. Use the CSV in `doc/ui-parity-inventory.csv` for a per-file export.
- A mark of "Partial" typically means the viewmodel or underlying logic is linked into Rover but the view is implemented as a shim or is missing a full `.axaml` equivalent.


**Notes:**
- The `ProjectRover.csproj` links many ILSpy view-model C# sources directly; in many cases the WPF view-model logic compiles in and is reused, while the actual view (XAML) is implemented or shimmed in Avalonia.
- "Done" means there is an Avalonia `.axaml` (or `.avalonia.cs`) equivalent and the viewmodel is either linked or present. "Partial" means behaviour is available via a shim or only the viewmodel is linked (no axaml). "TODO" indicates missing Avalonia UI or shim.

---

## File-based comparison

The table below is a file-level comparison exported from the workspace inventory. It maps ILSpy WPF files to ProjectRover equivalents and status.

| File (ILSpy) | Type | Linked in ProjectRover.csproj | Rover file (if present) | Status |
|---|---:|:---:|---|---|
| `src/ILSpy/ILSpy/MainWindowViewModel.cs` | ViewModel | No | `src/ProjectRover/MainWindowViewModel.cs` | Done |
| `src/ILSpy/ILSpy/Views/CompareView.xaml` | View | Yes | `src/ProjectRover/Views/CompareView.axaml` | Done |
| `src/ILSpy/ILSpy/TextView/DecompilerTextView.xaml` | View | Yes | `src/ProjectRover/TextView/DecompilerTextView.axaml` | Done |
| `src/ILSpy/ILSpy/Metadata/MetadataTableViews.xaml` | View | Yes | `src/ProjectRover/Metadata/MetadataTableViews.axaml` | Done |
| `src/ILSpy/ILSpy/Analyzers/AnalyzerTreeView.xaml` | View | Yes | `src/ProjectRover/Analyzers/AnalyzerTreeView.cs` | Partial |
| `src/ILSpy/ILSpy/Analyzers/AnalyzerTreeViewModel.cs` | ViewModel | Yes | (linked) | Partial |
| `src/ILSpy/ILSpy/ViewModels/ManageAssemblyListsViewModel.cs` | ViewModel | Yes | `src/ProjectRover/Views/ManageAssemblyListsDialog.axaml` | Partial |
| `src/ILSpy/ILSpy/ViewModels/UpdatePanelViewModel.cs` | ViewModel | Yes | `src/ProjectRover/Views/UpdatePanel.axaml` | Done |
| `src/ILSpy/ILSpy/Options/OptionsDialogViewModel.cs` | ViewModel | No | `src/ProjectRover/Options/OptionsDialog.axaml.cs` | Done |
| `src/ILSpy/ILSpy/Options/DecompilerSettingsViewModel.cs` | ViewModel | No | `src/ProjectRover/Options/DecompilerSettingsPanel.axaml` | Done |
| `src/ILSpy/ILSpy/Options/MiscSettingsViewModel.cs` | ViewModel | No | `src/ProjectRover/Options/MiscSettingsPanel.axaml` | Done |
| `src/ILSpy/ILSpy/Options/DisplaySettingsViewModel.cs` | ViewModel | No | `src/ProjectRover/Options/DisplaySettingsPanel.axaml` | Done |
| `src/ILSpy/ILSpy/Controls/ZoomScrollViewer.xaml` | Control/XAML | No | `src/ProjectRover/Controls/ZoomScrollViewer.axaml` | Done |
| `src/ILSpy/ILSpy/Controls/TreeView/SharpTreeView.xaml` | Control/XAML | Partial | (none) | Done (use Avalonia `TreeView`) |
| `src/ILSpy/ILSpy/Controls/SortableGridViewColumn.cs` | Control | No | (none) | Skip |
| `src/ILSpy/ILSpy/Controls/CollapsiblePanel.cs` | Control | No | (none) | Done (use Avalonia `Expander`) |
| `src/ILSpy/ILSpy/Metadata/FlagsTooltip.xaml` | View | No | `src/ProjectRover/Metadata/FlagsTooltip.axaml` | Done |
| `src/ILSpy/ILSpy/TreeNodes/AssemblyListTreeNode.cs` | TreeNode | Yes | `src/ProjectRover/TreeNodes/AssemblyListTreeNode.avalonia.cs` | Done |

---

## Platform differences

Documented platform-specific differences between ILSpy WPF and Project Rover (Avalonia):

1. **Windows-only: Windows shell / taskbar integration** — Windows-specific integrations (e.g., Windows Taskbar progress, jump lists, some shell APIs) are only available on Windows. Rover targets cross-platform; Windows-specific features are not available on macOS/Linux.
2. **No SharpTreeView (WPF control)** — The WPF `SharpTreeView` control and its XAML templates and automation peers are not available in Avalonia. ProjectRover includes a `SharpTreeViewShim.cs` to approximate behavior, but full parity (templates, automation peers, text-search helpers) remains TODO.
3. **No WPF adorners** — WPF `Adorner` layer features (used for overlays/visual adornments) do not exist in Avalonia; equivalent UX must be implemented using Avalonia overlays or custom controls.
4. **AvaloniaEditor (AvaloniaEdit) differences** — The editor used by Rover (`AvaloniaEdit`) is a separate codebase and differs from WPF `AvalonEdit`. Some features and APIs (e.g., certain text rendering hooks, adorner-like features, or exact highlighting behaviors) require adaptation; expect API and behavior deltas.

Notes: for each platform-specific delta above, we should decide whether to implement a Rover-specific shim, accept the platform limitation, or contribute required features upstream to Avalonia/AvaloniaEdit.


## Revised Epics & Status (actionable, short)

### Epic: WPF Parity (High Priority)
- **AssemblyList & LoadedAssembly:** Completed — ILSpy's AssemblyList/LoadedAssembly are present and used. Evidence: [src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs](src/ILSpy/ICSharpCode.ILSpyX/AssemblyList.cs#L40).
- **Resolver-based referenced-assembly loading + AutoLoad UX:** Partially completed — settings exist (`AutoLoadReferencedAssemblies`), resolver hooks are used by decompiler, but UX prompts and confirmation flow need to be implemented.
- **Token+assembly identity resolution:** Mostly completed — token probing + DecompilerTypeSystem resolution present and used by navigation.
- **JumpToReferenceAsync / Navigation orchestration:** Completed — message flow and navigation handler implemented. Evidence: [src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs](src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs#L597).
- **Expand indexing to exported/forwarded types & lazy strategies:** Not completed — search/indexing exists but needs extension.
 - **Expand indexing to exported/forwarded types & lazy strategies:** Future (deferred) — not completed; work deferred until ILSpy WPF is retired and Rover becomes the official UI.
- **Replace ad-hoc assembly-loads with AssemblyList APIs:** Partially completed — many ILSpy calls are already part of ProjectRover, but audit + replace remaining ad-hoc loads is recommended.

### Epic: Resolver Improvements
- **Signature decoding accuracy & cancellation/progress wiring:** Not completed — worth prioritizing if heavy background work (indexing/navigation) is added.

### Epic: Settings, Recent Files, and State (High Priority)
- **ILSpy settings provider wired:** Completed — `ILSpySettingsFilePathProvider` implemented. Evidence: [src/ProjectRover/Services/ILSpySettingsFilePathProvider.cs](src/ProjectRover/Services/ILSpySettingsFilePathProvider.cs#L1).
- **Rover options mapped to ILSpy settings sections:** Mostly completed — Rover settings classes exist and are persisted; a compatibility audit is advised.
- **Persist search mode / pane visibility:** Completed (search pane hooks are present).
- **Map AutoLoadAssemblyReferences, active tree path, assembly-list persistence:** Partially completed — session settings code present but end-to-end behavior should be validated.

### Epic: Decompilation & Editor Integration (High Priority)
- **Reference-highlighting / navigation in AvaloniaEdit:** Partially completed — many features work (local marks, jumps) but some ILSpy reference-highlighting behaviors remain TODO.

### Epic: Analyzers & Advanced Features (Medium Priority)
- **Analyzers present and runnable:** Implemented — analyzer code is included. UI polish and settings for analyzers are partial.

### Epic: UX Polishing & Theming (Low Priority)
- **Resource nodes expansion:** Partial — resource node infrastructure is present.
- **Icon resources & theme mappings:** Partial — theme shim exists; consolidation required.
- **Tree ordering & grouping parity:** Partial — basic parity achieved; edge cases remain.

##### Theming checklist

- [x] Per-editor `TextMate.Installation` created and wired in `DecompilerTextView` (SetTheme + AppliedTheme) — applies syntax theme and GUI brushes.
- [x] GUI brushes applied from TextMate themes (`editor.background`, `editor.foreground`, `editor.selectionBackground`, `editor.lineHighlightBackground`, `editorLineNumber.foreground`) with resource fallbacks.
- [x] Demo `MainWindow` updated as canonical example for wiring `RegistryOptions`, `InstallTextMate`, and applying theme-derived brushes.
- [ ] Propagate the same `InstallTextMate` + `ApplyThemeColorsToEditor` pattern to other `TextEditor` owners in the codebase.
- [ ] Verify runtime behavior across platforms by running the app and toggling Light/Dark themes; capture logs and visual confirmation.

### Epic: Plugin/Extension Model (Low Priority)
- **Inventory of ILSpy add-ins:** Completed — many add-ins linked and inventoried.
- **Compatibility shims/adaptors for Avalonia controls:** Completed/ongoing — many shims present (e.g., `ThemeManagerShim`).
