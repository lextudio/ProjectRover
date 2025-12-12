## Epics and Actions (prioritized)

### Epic 1 — ILSpyX Adoption & Tree Unification (High Priority)
Goal: Make Rover a thin UI layer consuming ILSpyX for assembly and tree logic.
Actions:
- [x] Verify `UsingLocalIlSpyX` conditional project reference works. (done)
- [x] Provide `IlSpyXTreeAdapter` to map `LoadedAssembly` → `ResourceNode`/`TypeNode` (done/partial).
- [ ] Replace Rover Node implementations with thin wrappers around ILSpyX `SharpTreeNode` where practical.
- [ ] Remove duplicated assembly management (`IlSpyBackend`) once adapters are stable.
Status: In progress — adapters present, some nodes still custom.

### Epic 2 — Settings, Recent Files, and State (High Priority)
Goal: Share settings data with ILSpy and maintain consistent startup state.
Actions:
- [x] Wire `ILSpySettings.SettingsFilePathProvider` to Rover provider. (done)
- [x] Migrate Rover options to use ILSpy settings sections (or implement a compatibility layer).
- [x] Store search mode and search pane visibility in the shared settings so WPF and Rover honor the same choice.
Status: In progress.

### Epic 3 — Search & Navigation Parity (High Priority)
Goal: Use ILSpyX search providers and mapping to match WPF behavior.
Actions:
- [x] Add `IlSpyXSearchAdapter` to convert ILSpyX search results to Rover `SearchResult` (done)
- [x] Focus assembly search results on their matching assembly node.
- [x] Ensure all ILSpy search features (filtering by type/member/resource/assembly) are available in Rover (`docs/search-features.md` documents the coverage).
- [ ] Integrate Go-To/Find usages behavior via ILSpyX mapping.
Status: Progressing — search coverage expanding.

### Epic 4 — Decompilation & Editor Integration (High Priority)
Goal: Provide identical decompilation results and in-editor hyperlinks/Go-To behavior.
Actions:
- [ ] Delegate token -> node mapping and symbol navigation to ILSpyX.
- [ ] Ensure language selection and settings flow from ILSpyX to Rover editor.
- [ ] Port ILSpy reference-highlighting features fully to AvaloniaEdit.
Status: TODO — partial features ported.

### Epic 5 — Analyzers & Advanced Features (Medium Priority)
Goal: Enable analyzers, metrics and advanced search features from ILSpy.
Actions:
- [x] Identify analyzers in ILSpy and expose their results in Rover UI (cataloged at `docs/ilspy-analyzers.md` with a high-level integration plan).
- [ ] Add UI affordances for analyzer settings and results.
Status: TODO.

### Epic 6 — UX Polishing & Theming (Low Priority)
Goal: Align icons, docks, and small UX behaviors with ILSpy WPF.
Actions:
- [x] Ensure resource nodes expand `.resources` and `.resx` entries (done)
- [ ] Consolidate icon resources and theme mappings.
- [ ] Match tree ordering and grouping rules.
Status: Ongoing.

### Epic 7 — Plugin/Extension Model (Low Priority)
Goal: Support ILSpy add-ins or define a Rover-specific plugin surface.
Actions:
- [x] Inventory ILSpy add-ins and prioritized ones to support (see `docs/ilspy-addins.md` for the current catalog).
- [x] Define compatibility shims / adaptors for Avalonia controls (covered: commands, tree-node interface, dock descriptor).
Status: TODO.

---

## Actionable near-term checklist (Can be executed in 1–2 week sprints)

Sprint A: Stabilize ILSpyX adapters
- Replace Rover tree construction for assemblies with `IlSpyXTreeAdapter` output.
- Implement light compatibility wrappers for Node/SharpTreeNode differences.
- Tests: compare node counts and node labels against WPF ILSpy for a set of assemblies.

Sprint B: Complete Search integration
- Swap Rover search pipeline to call ILSpyX providers directly.
- Add unit tests for search result mapping.

Sprint C: Decompilation mapping and editor features
- Wire Go-To Definition through ILSpyX tokens.
- Port any remaining reference-highlighting.

Sprint D: Settings & UX finish
- Migrate Rover settings to ILSpySettings sections or add compatibility layer.
- Polish icons and docking behavior.

---

## Ownership and Risks

- Ownership recommendations: small cross-repo team (1-2 maintainers) that understand ILSpyX internals and Avalonia.
- Risks: Requiring changes in ILSpy WPF to extract more cross-platform logic increases coordination costs. Some WPF-specific UI paradigms (commands, resources, and certain behaviors) might not map 1:1 to Avalonia.

---

## Appendix A — File pointers

- ILSpy (WPF): `ilspy/ILSpy` — primary folders: `TreeNodes`, `Views`, `Commands`, `Analyzers`, `Metadata`.
- Rover (Avalonia): `ILSpyRover/src/ProjectRover` — primary folders: `Nodes`, `Views`, `ViewModels`, `Services`, `Providers`.
- Decompiler util: `ilspy/ICSharpCode.Decompiler/Util/ResourcesFile.cs` (used by both for .resources parsing)

---

## Observed Model Differences & Actions (diagnostic)

These items summarize concrete structural and behavioral differences between ILSpy WPF and Rover (Avalonia) discovered during the analysis session. Each entry includes an action to close the parity gap.

- Assembly lifecycle and list management:
	- Difference: ILSpy has a rich `AssemblyList`/`LoadedAssembly` abstraction with thread ownership, snapshots, persistence, hot-replace and file-loader extensibility. Rover currently loads assemblies synchronously via `IlSpyBackend` and `AssemblyTreeModel`.
	- [ ] Action: Implement a minimal `AssemblyList` shim (Open/Find/GetAssemblies/OpenFromStream/Reload/Unload) and migrate `AssemblyTreeModel` to use it for all assembly lifecycle operations.

- Threading and async metadata loading:
	- Difference: ILSpy performs async metadata loads and marshals UI updates to the UI thread; Rover performs synchronous loads and indexes on the calling thread.
	- [ ] Action: Make assembly loading and handle indexing asynchronous; ensure UI updates happen on the UI thread (use Avalonia Dispatcher). Add unit tests to ensure no UI thread blocking on large loads.

- Resolver-based reference loading / auto-load behavior:
	- Difference: ILSpy resolves and auto-loads referenced assemblies (via `AssemblyList` and file loaders), honoring user preferences for prompting/auto-load. Rover currently attempts ad-hoc loads when navigating.
	- [ ] Action: Add resolver-based lookup using an `AssemblyList` and a `LoaderRegistry` shim; implement an `AutoLoadReferencedAssemblies` preference and prompt UX.

- Robust handle resolution (token + assembly identity):
	- Difference: Rover relies on `(assemblyPath, EntityHandle)` map + a string-kind fallback that is brittle when assemblies differ or are not yet loaded. ILSpy resolves tokens against PE metadata and probes candidate assemblies to determine which PE contains a given token.
	- [ ] Action: Replace string-kind fallback with a token+assembly identity resolution algorithm: prefer exact (filePath + token), then consult `AssemblyList` candidates by assembly simple name or MVID and test token presence in each candidate's metadata (like `CanResolveTypeInPEFile`).

- Jump/Navigation flow parity:
	- Difference: ILSpy's `JumpToReferenceAsync` orchestrates resolution, waits for metadata, optionally loads referenced assemblies, and updates navigation history & selection. Rover's navigation is split across ViewModel and `AssemblyTreeModel` and is less resilient.
	- [ ] Action: Implement `JumpToReferenceAsync` in `AssemblyTreeModel` or a NavigationService that uses `AssemblyList`, resolves tokens, optionally loads assemblies, indexes handles, and then selects the target node.

- Indexing surface and handle coverage:
	- Difference: ILSpy indexes additional metadata constructs (exported types, forwarded types, and other tables) and has richer tree nodes (`SharpTreeNode`) supporting lazy children. Rover currently indexes types/members but should expand coverage.
	- [ ] Action: Extend indexing to include exported types, forwarded types, module-level handles, and consider lazy indexing for very large assemblies.

- Backend API surface & separation of concerns:
	- Difference: ILSpy separates assembly-list, loader logic, and tree node construction across `AssemblyList`, `LoadedAssembly`, and `TreeNode` adapters. Rover centralizes some behaviors in `IlSpyBackend` and `AssemblyTreeModel`.
	- [ ] Action: Move low-level IO and ILSpyX calls into `IlSpyBackend` and keep `AssemblyTreeModel` responsible for UI model and indexing; expose `IlSpyBackend.OpenAssembly/OpenAssemblyFromStream/FindLoadedAssembly` APIs.

- UX + settings parity:
	- Difference: ILSpy respects session settings (auto-load, theme, last-tree-path) and uses them in navigation; Rover keeps some settings but lacks complete integration.
	- [ ] Action: Add compatibility mappings for `AutoLoadAssemblyReferences`, active-tree-path restore, and assembly-list persistence; wire them into `AssemblyList` behavior.

Each of the above actions can be broken into smaller tasks; we should prioritize by user-visible impact: token+assembly resolution and resolver-based auto-load are highest priority to fix Find Usages / Go-To parity.

---

## Refactor Action: Align Rover Architecture With ILSpy WPF (detailed)

Goal: Refactor Rover incrementally so its model/viewmodel surface, assembly lifecycle, and navigation flow closely match ILSpy WPF semantics. This enables reliable Find Usages / Go‑To parity and reduces ad-hoc resolution logic.

Scope & Rationale:
- Move from a centralized `MainWindowViewModel` holding most responsibilities to a clear separation:
  - `AssemblyList` (manages loaded assemblies, persistence, loaders)
  - `LoadedAssembly` (represents a loaded PE + resolver + type system)
  - `AssemblyTreeModel` (owns tree nodes, indexing, and resolves handles to nodes)
  - `NavigationService` or `AssemblyTreeModel.JumpToReferenceAsync` (resolves tokens and performs navigation)

High-level steps (incremental, each step buildable & testable):

 - [ ] 1) Scaffold `AssemblyList` and `LoadedAssembly` shims (small API-first change)
	- [ ] Add `ICoreAssemblyList` interface or concrete `AssemblyListShim` with methods:
	  - `LoadedAssembly? OpenAssembly(string path, bool isAutoLoaded = false)`
	  - `LoadedAssembly? OpenAssembly(string path, Stream stream, bool isAutoLoaded = false)`
	  - `LoadedAssembly[] GetAssemblies()`
	  - `void Unload(LoadedAssembly asm)`
	  - `LoadedAssembly? FindAssembly(string file)`
	  - `Task<IList<LoadedAssembly>> GetAllAssemblies()`
	- [ ] `LoadedAssembly` should expose:
	  - `string FileName`, `PEFile GetMetadataFileOrNull()`, `IAssemblyResolver GetAssemblyResolver()`, `bool IsAutoLoaded`.
	- [ ] Implementation: delegate to existing `IlSpyBackend` / `ICSharpCode.ILSpyX.AssemblyList` where possible (adapter pattern).
	- [ ] Tests: ensure `OpenAssembly` returns same object as `IlSpyBackend.LoadAssembly` for a simple DLL.

 - [ ] 2) Migrate `IlSpyBackend` surface into explicit open/find calls
	- Add `IlSpyBackend.OpenAssembly`, `FindLoadedAssembly`, `GetLoadedAssemblies` wrappers.
	- Keep `DecompileMember` and `AnalyzeSymbolReferences` high-level methods here.

 - [ ] 3) Move assembly indexing into `AssemblyTreeModel` with `IndexAssemblyHandles(LoadedAssembly)`
	- `AssemblyTreeModel` will call `AssemblyList.OpenAssembly` and subscribe to assembly-list changes.
	- Index by `(AssemblyPath, EntityHandle)` as the primary map; keep a secondary map by handle alone for cross-assembly fallback.
	- Expand indexing to exported/forwarded types and module-level entries.

 - [ ] 4) Implement `JumpToReferenceAsync` (or `NavigationService.JumpToReferenceAsync`)
	- Accepts reference (IEntity, EntityHandle, or id-string) and optional source.
	- Resolution algorithm:
	  - If `EntityHandle` and assembly path present: attempt to resolve to node via `AssemblyTreeModel.ResolveNode`.
	  - Else consult `AssemblyList` to find candidate `LoadedAssembly` entries; for each candidate, probe `GetMetadataFileOrNull` + `DecompilerTypeSystem` to test token presence.
	  - If token found in an assembly not yet in tree: load node via `IlSpyXTreeAdapter` and index it, optionally auto-loading dependencies (respect `AutoLoad` setting or prompt user).
	  - When resolved, update selection & navigation history (use message bus or direct call into `MainWindowViewModel`).

 - [ ] 5) Make assembly load and indexing asynchronous + UI-thread safe
	- Use Avalonia's dispatcher to ensure tree updates happen on UI thread.
	- When analyzers return references in other assemblies, `JumpToReferenceAsync` can await loading & indexing without blocking UI.

 - [ ] 6) Wire settings and persistence
	- Map `AutoLoadAssemblyReferences`, Active Tree Path, and assembly lists into `AssemblyList` persistence.
	- Update `RestoreLastAssemblies` to use `AssemblyList` snapshots.

 - [ ] 7) Replace `MainWindowViewModel` ad-hoc functions with calls to `AssemblyTreeModel` / `NavigationService`

## Recent Findings (post-refactor iteration)

These are concrete items discovered while extracting navigation logic from `MainWindowViewModel` and wiring a small `NavigationService` shim. They should be added to the near-term checklist and considered when prioritizing the next sprint.

- **What we implemented (now present in Rover):**
	- **NavigationService shim:** A small `NavigationService` centralizes search-result resolution and assembly-load attempts. (File: ProjectRover/Services/Navigation/NavigationService.cs)
	- **ViewModel decoupling:** `MainWindowViewModel` delegates search-result navigation to `INavigationService`. (File: ProjectRover/ViewModels/MainWindowViewModel.cs)
	- **DI registration:** `INavigationService` is registered in DI so other components can reuse navigation logic. (File: ProjectRover/Extensions/ServiceCollectionExtensions.cs)
	- **Design VM support:** design-time `DesignMainWindowViewModel` was updated to provide a design `NavigationService` for the designer.

- **Remaining differences vs ILSpy WPF (high impact):**
	- **No `AssemblyList` / `LoadedAssembly` abstraction yet:** ILSpy's `AssemblyList` centralizes lifecycle, resolvers and persistence; implementing a compatible shim remains the highest-impact item.
	- **No resolver-based referenced-assembly auto-load:** ILSpy resolves references by identity (name/MVID) and optionally loads referenced assemblies; Rover still loads by file path on demand.
	- **Token resolution algorithm is incomplete:** Rover currently uses `(assemblyPath, EntityHandle)` map and probing fallbacks — ILSpy uses a more robust token+assembly identity resolution.
	- **No `JumpToReferenceAsync` orchestration:** WPF coordinates async loads, indexing, and navigation with progress; Rover uses a synchronous flow that needs to evolve into an async navigation flow.
	- **Indexing breadth and async loading:** ILSpy performs lazy async metadata loads and indexes additional metadata (exported/forwarded types). Rover must expand indexing and make loads non-blocking.
	- **Partial node parity and adapters:** Some Rover nodes still differ from ILSpy `SharpTreeNode` semantics and lazy children behaviour.
	- **Settings/persistence parity incomplete:** Session-level persistence of `AssemblyList` snapshots, auto-load preferences and active tree path restoration are not fully implemented.

- **Files/places to focus next (low-risk order):**
	1. Scaffold a minimal `AssemblyList` shim exposing `OpenAssembly`, `FindLoadedAssembly`, `GetLoadedAssemblies`, and wire `IlSpyBackend` through it.
	2. Move `AssemblyTreeModel` to use `AssemblyList` for lifecycle and reindexing.
	3. Implement resolver-based referenced-assembly loading and add `AutoLoadReferencedAssemblies` preference and prompt UX.
	4. Add `JumpToReferenceAsync` to `NavigationService` (or a new `NavigationOrchestrator`) to async-load/index and navigate with progress.

Add these findings as checklist items and optionally open issues for each to track implementation.
	- Keep `MainWindowViewModel` for UI glue but move heavy logic into new services.

8) Validate parity with tests and runtime checks
	- Automated tests: load a set of assemblies and compare node counts with ILSpy WPF for the same assemblies.
	- Manual test: run Find Usages on members that reference types in external dependencies; verify Rover loads referenced assembly and navigates to hits.

API notes and examples
- `AssemblyListShim.OpenAssembly` should internally call `IlSpyBackend.LoadAssembly` and return a `LoadedAssemblyShim` object that preserves `GetMetadataFileOrNull()` and `GetAssemblyResolver()` so existing code (adapters) can reuse them.
- `AssemblyTreeModel.IndexAssemblyHandles(LoadedAssembly)` should call `IlSpyXTreeAdapter.BuildAssemblyNode` and then populate `handleToNodeMap` with `(loaded.FilePath, token)` keys.

Risk mitigation and rollback
- Implement the refactor behind feature flags or add `UsingLocalIlSpyX` guard so changes are reversible.
- Each step should be small & buildable: add shims first, then atomically migrate one caller (e.g., `LoadAssemblies`), then run tests.

Estimated effort
- Scaffold shims + simple migration: 1–2 days.
- JumpToReference + auto-load + async: 2–4 days (depends on UX prompt integration).
- Full cleanup and tests: 1–2 days.

Ownership & next steps
- Small cross-repo pair: one engineer for ILSpyX internals and one for Rover/Avalonia integration.
- Next immediate task: scaffold `AssemblyList` and `LoadedAssembly` shim and migrate `AssemblyTreeModel.LoadAssembly` to use it.


---

If you'd like, I can now:
- run a focused search to enumerate all `Node` types in both projects and produce a side-by-side mapping CSV,
- or generate a prioritized GitHub issue list for the top 3 epics and create starter issues/PR templates.

Pick the next action and I'll proceed.
