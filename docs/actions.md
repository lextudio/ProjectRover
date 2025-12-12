See [Find Usages Integration](navigation-find-usages.md) for details on Go-To/Find Usages behavior in Rover.
## Summary

This document tracks the prioritized epics for bringing ILSpyRover (Avalonia) closer to ILSpy WPF behavior. It contains only three sections: `## Summary`, `## Epics`, and `## Appendix`.

---

## Epics

### Epic: WPF Parity (High Priority)
- [ ] Implement AssemblyList and LoadedAssembly shim to centralize lifecycle, persistence, and resolution.
- [ ] Add resolver-based referenced-assembly loading and an AutoLoadReferencedAssemblies preference with prompt UX.
- [ ] Implement token+assembly identity resolution algorithm to robustly map EntityHandle to tree nodes.
- [ ] Implement JumpToReferenceAsync (or NavigationService.JumpToReferenceAsync) to orchestrate async loads, indexing and navigation with progress.
- [ ] Expand indexing to exported/forwarded types and module-level constructs and add lazy indexing strategies.
- [ ] Map Rover settings and session persistence to ILSpy WPF settings where possible.
- [ ] Replace ad-hoc assembly-load calls across the codebase with IlSpyBackend and AssemblyList APIs.

### Epic: ILSpyX Adoption & Tree Unification (High Priority)
- [x] Verify UsingLocalIlSpyX conditional project reference works.
- [x] Provide IlSpyXTreeAdapter to map LoadedAssembly → ResourceNode/TypeNode.
- [ ] Replace Rover Node implementations with thin wrappers around ILSpyX SharpTreeNode where practical.
- [ ] Remove duplicated assembly management (IlSpyBackend) once adapters are stable.

### Epic: Settings, Recent Files, and State (High Priority)
- [x] Wire ILSpySettings.SettingsFilePathProvider to Rover provider.
- [x] Migrate Rover options to use ILSpy settings sections or implement a compatibility layer.
- [x] Store search mode and search pane visibility in the shared settings.
- [ ] Map AutoLoadAssemblyReferences, active tree path, and assembly-list persistence into Rover settings and AssemblyList behavior.

### Epic: Search & Navigation Parity (High Priority)
- [x] Add IlSpyXSearchAdapter to convert ILSpyX search results to Rover SearchResult.
- [x] Focus assembly search results on their matching assembly node.
- [x] Ensure all ILSpy search features (filtering by type/member/resource/assembly) are available in Rover.
- [ ] Integrate Go-To/Find usages behavior via ILSpyX mapping.

### Epic: Decompilation & Editor Integration (High Priority)
- [ ] Delegate token -> node mapping and symbol navigation to ILSpyX.
- [ ] Ensure language selection and settings flow from ILSpyX to Rover editor.
- [ ] Port ILSpy reference-highlighting features fully to AvaloniaEdit.

### Epic: Analyzers & Advanced Features (Medium Priority)
- [x] Identify analyzers in ILSpy and expose their results in Rover UI.
- [ ] Add UI affordances for analyzer settings and results.

### Epic: UX Polishing & Theming (Low Priority)
- [ ] Ensure resource nodes expand .resources and .resx entries.
- [ ] Consolidate icon resources and theme mappings.
- [ ] Match tree ordering and grouping rules.

### Epic: Plugin/Extension Model (Low Priority)
- [x] Inventory ILSpy add-ins and prioritized ones to support.
- [x] Define compatibility shims / adaptors for Avalonia controls (commands, tree-node interface, dock descriptor).

---

## Appendix — Pointers
- ILSpy (WPF): ilspy/ILSpy — primary folders: TreeNodes, Views, Commands, Analyzers, Metadata.
- Rover (Avalonia): ILSpyRover/src/ProjectRover — primary folders: Nodes, Views, ViewModels, Services, Providers.
- Decompiler util: ilspy/ICSharpCode.Decompiler/Util/ResourcesFile.cs (used by both for .resources parsing).

- [x] Inventory ILSpy add-ins and prioritized ones to support (see `docs/ilspy-addins.md`).
- [x] Define compatibility shims / adaptors for Avalonia controls (commands, tree-node interface, dock descriptor).
