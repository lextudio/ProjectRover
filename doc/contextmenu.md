# Context menus and tooltips in WPF ILSpy

This document lists WPF features in the ILSpy project that provide context menus and tooltips, where they are implemented (file paths), and short steps to test them in the running application.

Notes
- Paths are workspace-relative. Open the referenced XAML/CS files when following links.

Context menus

- Main decompile/stack tree context menu entries
  - Location: src/ILSpy/ILSpy/Commands (various files implementing `IContextMenuEntry`)
  - Examples: `DecompileCommand.cs`, `CopyFullyQualifiedNameContextMenuEntry.cs`, `CreateDiagramContextMenuEntry.cs`, `ShowCFGContextMenuEntry.cs`
  - How to test:
    1. Open ILSpy and browse to an assembly node or a type/method in the tree view.
    2. Right-click the item to open the context menu. Verify entries such as "Decompile", "Copy name", and any exported entries from plugins appear.
    3. Select commands and verify expected behavior (decompile view opens, clipboard updated, diagram created when available).

- Manage assembly lists preconfigured menu
  - Location: src/ILSpy/ILSpy/Views/ManageAssemblyListsDialog.xaml and ManageAssemblyLIstsDialog.xaml.cs
  - How to test:
    1. Open the Manage Assembly Lists dialog from the UI (toolbar or menu where assembly lists are managed).
    2. Right-click a list entry to see the `PreconfiguredAssemblyListsMenu`.
    3. Choose one of the menu items and observe the dialog behavior (apply/select the list).

- DataGrid cell context menu (third-party DataGrid)
  - Location: ProjectRover/thirdparty/Avalonia.Controls.DataGrid (DataGridContextMenuEventArgs.cs, Themes)
  - How to test:
    1. Open any view that renders a DataGrid (for features ported from Avalonia controls).
    2. Right-click cells or headers to trigger the DataGrid context menu. If the DataGrid implementation exposes `ContextMenuOpening` events, validate the per-cell context menu appears and is anchored to the cell.

Tooltips

- Search pane results
  - Location: src/ILSpy/ILSpy/Search/SearchPane.xaml
  - What: Items in the search results have `ToolTip` bound to additional info (Name, Location, Assembly)
  - How to test:
    1. Open the Search pane and run a search with results.
    2. Hover the `Name`, `Location`, or `Assembly` columns to see the tooltip content.

- Compare view toolbar buttons
  - Location: src/ILSpy/ILSpy/Views/CompareView.xaml
  - What: Buttons for swap, expand all, copy to JSON have `ToolTip` attributes.
  - How to test:
    1. Open the Compare view with two sets/assemblies loaded.
    2. Hover the toolbar buttons to verify their tooltips appear.

- Main toolbar controls
  - Location: src/ILSpy/ILSpy/Controls/MainToolBar.xaml
  - What: Many toolbar dropdowns and buttons have `ToolTip` bound to resources (select assembly list, manage lists, language/version selectors).
  - How to test:
    1. Hover toolbar buttons and dropdown toggles to see their tooltips.

- Tree view nodes
  - Location: src/ILSpy/ILSpy/Controls/TreeView/SharpTreeView.xaml and ICSharpCode.ILSpyX/TreeView/SharpTreeNode.cs
  - What: Tree nodes expose a `ToolTip` property that UI binds to.
  - How to test:
    1. Open the tree view showing assemblies/types.
    2. Hover nodes with long names or extra info to see the tooltip.

- Status bar / main window
  - Location: src/ILSpy/ILSpy/MainWindow.xaml
  - What: Status area has `ToolTip` bound to resources (status description).
  - How to test:
    1. Hover the status text in the main window bottom/status area to see the tooltip text.

- Search box and helper icons
  - Location: src/ILSpy/ILSpy/Controls/SearchBoxStyle.xaml and ICSharpCode.ILSpyX/MermaidDiagrammer/html/template.html (hints)
  - How to test:
    1. Hover the search icon/controls to view the tooltip giving search help.

Other tooltip usages (API / model)

- `ILanguage.GetTooltip` and `SearchResult.ToolTip`
  - Location: src/ILSpy/ICSharpCode.ILSpyX/Abstractions/ILanguage.cs and src/ILSpy/ICSharpCode.ILSpyX/Search/SearchResult.cs
  - What: Language plugins and search results supply tooltip data that the UI displays.
  - How to test:
    1. Install/enable languages and run searches or hover decompilation language elements that should show rich tooltip content.

Notes and testing tips
- Some context menu entries are exported via MEF (look for `ExportContextMenuEntry` attribute). Plugin-provided entries will appear only when the related plugin is available/active.
- Tooltips bound to `ToolTip` properties or resource strings may vary by localization. Use `Resources` strings to check the exact tooltip text if needed.
- If running the ProjectRover port, some UI surfaces may differ from the original WPF ILSpy; use the files referenced above as the canonical locations to inspect behavior.

Quick test checklist

1. Launch ILSpy / ProjectRover UI.
2. Browse the assembly tree, right-click several nodes; confirm main context menu entries and plugin-provided entries.
3. Open Search and Compare panes; hover items and toolbar buttons to verify tooltips.
4. Open Manage Assembly Lists dialog; right-click lists and test the preconfigured menu.
5. If available, open views containing DataGrids and validate per-cell context menus and tooltips (column header tooltips such as "Sort indicator" and "Open filter").

If you want, I can extend this document with direct file links, sample screenshots, or add step-by-step screenshots for CI tests.
