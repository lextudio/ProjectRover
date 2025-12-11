# ILSpy analyzers reference

ILSpy exposes a collection of analyzers (under `ICSharpCode.ILSpyX/Analyzers`) that inspect metadata and IL to compute relationships such as "a method overrides another" or "this type is used by others." Rover can reuse these analyzers to provide the same insights in the Avalonia UI.

## Analyzer categories

- **Attribute/usage analyzers**
  - `AttributeAppliedToAnalyzer`: finds members with a specific attribute.
  - `FindTypeInAttributeDecoder`: locates attribute arguments that point to a given type.
- **Reference analyzers**
  - `MethodUsedByAnalyzer`, `MethodUsesAnalyzer`, `MethodVirtualUsedByAnalyzer`: show call relationships.
  - `FieldAccessAnalyzer`, `PropertyImplementedByAnalyzer`, `EventImplementedByAnalyzer`, etc.: capture who accesses/implements/overrides a member.
  - `MemberImplementsInterfaceAnalyzer`, `MethodImplementedByAnalyzer`, `TypeExposedByAnalyzer`, `TypeUsedByAnalyzer`: describe implementation/usage chains for members/types.
- **Inheritance analyzers**
  - `MethodOverriddenByAnalyzer`, `PropertyOverriddenByAnalyzer`, `EventOverriddenByAnalyzer`: walk override graphs.
  - `TypeExtensionMethodsAnalyzer`: lists methods that extend the target type.
  - `TypeInstantiatedByAnalyzer`: finds where a given type is instantiated.
  - `MethodImplementedByAnalyzer` / `PropertyImplementedByAnalyzer`: show interface implementation points.

All analyzers implement `IAnalyzer` and are exported via `ExportAnalyzerAttribute`, so the ILSpy analyzer workspace autobinds them for ILSpyX clients.

## Rover integration plan

1. **Analyzer service** – wrap the ILSpy analyzer infrastructure in a Rover-specific service (e.g., `IAnalyzerService`) that exposes analyzers for a selected node or assembly.
2. **UI affordance** – add a docked pane or panel that lists analyzer results when a tree node is selected (mirroring ILSpy’s “Analyzers” view). Each entry should show the analyser type, the related symbol, and allow navigation to the reference (via the existing node/handle mapping).
3. **Shared descriptors** – reuse `CommandCatalog` + `ISharedTreeNode` so analyzer results can reuse the same commands and icons (e.g., “Go To Definition”).
4. **Caching and background execution** – leverage ILSpyX’s analyzer context (`AnalyzerContext`, `AnalyzerScope`) so Rover can run analyzers in the background and update the UI incrementally, similar to ILSpy’s behavior.

Documenting these analyzers and the integration plan ensures Epic 5 has a clear path toward exposing ILSpy metrics in Rover. The next step is building the `IAnalyzerService` and the panel that displays the results.
