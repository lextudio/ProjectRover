## Compatibility shims for Avalonia controls

To keep Rover aligned with ILSpy WPF while running on Avalonia, we need a thin compatibility layer that maps shared concepts (commands, icons, docking behaviors, tree nodes) onto Avalonia equivalents. This keeps future work (e.g., web or macOS builds) focused on data/model sharing rather than UI rewrites.

### 1. Menu/command descriptors
- ILSpy defines actions via `ILSpy.AddIn.vsct` and command classes (e.g., `CopyFullyQualifiedNameContextMenuEntry`), which attach to the tree view, menus, or context menus.
- **Shim idea:** define an Avalonia `CommandDescriptor` record that mirrors the VSCT metadata (id, group, order, icon key) and expose a `CommandCatalog` service that builds `Avalonia.Controls.ICommand` implementations backed by shared view models.
- Once `CommandCatalog` is available, both the main menu and context menus can consume the same descriptors, reducing duplication between WPF and Rover.

### 2. Tree node contracts
- WPF ILSpy uses `SharpTreeNode` + derived tree node types that expose commands, icons, and selection behavior.
- **Shim idea:** introduce `ISharedTreeNode` interface (properties such as `Text`, `ImageKey`, `Command`, `Children`) implemented by Rover nodes and a lightweight wrapper over `SharpTreeNode` when consuming ILSpyX results.
- This interface can power both the Avalonia `TreeView` and future UI surfaces, so tree-rendering logic stays consistent with ILSpy’s `NavigationHistory`, `SelectNodeByMemberReference`, etc.

### 3. Docking/toolbar behaviors
- ILSpy’s docking system arranges panes, handles theme colors, and exposes toolbar commands via `DockWorkspace`.
- **Shim idea:** create a `DockDescriptor` (panes, tools, default visibility) that Rover reads to build `Dock.Model` configuration. The descriptor layer can also surface command bindings so toolbar buttons reuse the same commands defined in step 1.
- This keeps the layout metadata centralized, meaning we can align future Av controllers with ILSpy’s docking structure without rewriting the data model.

### 4. Icon & resource mapping
- ILSpy WPF uses the VS 2022 icon library (e.g., `Images.Class`, `Images.Field`). Rover already consumes `IconKeyToPathConverter`.
- **Shim idea:** publish a `SharedIconSet` mapping (key → themed SVG path) with tokens used by both versions. Rover’s converter can consult this map to resolve either light or dark variants.

### Next steps
1. Implement the `CommandCatalog` service and register shared commands (menu IDs, icons). Already-parsed ILSpy resources can seed it.
2. Introduce `ISharedTreeNode` and adapt `IlSpyXTreeAdapter` to emit nodes that implement it.
3. Build a `DockDescriptor` JSON or code model that can generate either WPF or Avalonia layouts, keeping the same pane names/ids.
4. Expand `SharedIconSet` so Rover automatically picks VS 2022 icons (light/dark) per theme.

Once these shims exist, subsequent actions (e.g., actual plugin plumbing) can reuse the shared metadata, and tests can verify the descriptors against the WPF ILSpy config.
