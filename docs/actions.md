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
- [x] Expose the `Literal` search mode alongside other ILSpy filters.
- [x] Focus assembly search results on their matching assembly node.
- [ ] Ensure all ILSpy search features (filtering by type/member/resource/assembly) are available in Rover.
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
- [ ] Identify analyzers in ILSpy and expose their results in Rover UI.
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
- [ ] Inventory ILSpy add-ins and prioritized ones to support.
- [ ] Define compatibility shims / adaptors for Avalonia controls.
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

If you'd like, I can now:
- run a focused search to enumerate all `Node` types in both projects and produce a side-by-side mapping CSV,
- or generate a prioritized GitHub issue list for the top 3 epics and create starter issues/PR templates.

Pick the next action and I'll proceed.
