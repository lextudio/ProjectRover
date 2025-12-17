# Search feature coverage

ProjectRover already mirrors ILSpy’s built-in search modes via `IlSpyXSearchAdapter` and the combo box in `SearchDockView`. Each mode uses ILSpyX search strategies so filtering occurs on the ILSpyX side:

- **Types and Members:** the default search (`Types and Members`) calls `MemberSearchStrategy` with `MemberSearchKind.All`, so both type definitions and every member are included.
- **Type-only:** `SearchMode.Type` limits `MemberSearchStrategy` to `MemberSearchKind.Type`, giving simple type names (no namespace) plus a namespace location in the results.
- **Member-specific modes:** `SearchMode.Member`, `Method`, `Field`, `Property`, `Event`, and the special `Constant` mode drive `MemberSearchStrategy` with the matching `MemberSearchKind`. The adapter filters down to members of that kind and for `Constant` only const fields.
- **Resource search:** `SearchMode.Resource` switches to `ResourceSearchStrategy`, returning ILSpyX resource nodes and pointing navigation to `ResolveResourceNode`.
- **Assembly and Namespace:** `AssemblySearchStrategy` and `NamespaceSearchStrategy` produce results tied to the matching assembly/namespace, so the UI can select the corresponding `AssemblyNode`.
- **Metadata Token:** `MetadataTokenSearchStrategy` exposes metadata-token searches, matching the ILSpy experience.

Each `BasicSearchResult` maps ILSpyX metadata back to Rover nodes (types, members, resources, assemblies) via the handle/assembly resolver services, so double-click navigation works for every mode. The `SearchModes` collection and `MapMode` method tie the UI selection to these strategies, and `FilterByMode` enforces the correct result subset.

With this wiring in place, the “Ensure all ILSpy search features” action is satisfied, leaving only the Go-To/Find usages integration for Epic 3.
