# DataGrid API comparison — type classification

This file lists types found in the Avalonia DataGrid manifests vs the .NET 10 PresentationFramework reference manifest used for comparison.

Generated from: `tools/ApiDiff/reports/member-diff-summary.json`

Summary (EventArgs excluded):

- Related types: 59
- Avalonia-only:  (types present in Avalonia manifest but not in WPF) — excluding EventArgs
- WPF-only:      (types present in WPF manifest but not in Avalonia) — none in this scoped comparison
- Matched pairs: types that map to a WPF counterpart (Left -> Right) — excluding EventArgs

---

## Avalonia-only types (left only)

These types appear in the Avalonia DataGrid manifest and have no mapped counterpart in the WPF PresentationFramework ref manifest.

Count: 11

- Avalonia.Controls.DataGridCellHitTestResult
- DataGridExtensions.DataGridColumnExtensions
- Avalonia.Collections.DataGridComparerSortDescription
- DataGridExtensions.DataGridFilter
- Avalonia.Controls.DataGridFilterKind
- Avalonia.Controls.Primitives.DataGridFrozenGrid
- Avalonia.Controls.DataGridRowGroupHeader
- Avalonia.Collections.DataGridSortDescription
- Avalonia.Collections.DataGridSortDescriptionCollection
- Avalonia.Collections.IDataGridCollectionView
- Avalonia.Collections.IDataGridCollectionViewFactory

Notes: EventArgs were excluded from these lists by name (`*EventArgs`). Many of the remaining types are collection, group- or helper types — consider marking them internal or adding mapping aliases.

## WPF-only types (right only)

Count: 0

No types were discovered that exist in WPF and not in Avalonia within the filtered comparison scope.

## Matched pairs (Avalonia -> WPF)

Count: 35

The following types were considered related by namespace mappings / name aliasing and are listed as Left -> Right.

- Avalonia.Controls.DataGrid -> System.Windows.Controls.DataGrid
- Avalonia.Controls.Automation.Peers.DataGridAutomationPeer -> System.Windows.Automation.Peers.DataGridAutomationPeer
- Avalonia.Controls.DataGridBoundColumn -> System.Windows.Controls.DataGridBoundColumn
- Avalonia.Controls.DataGridCell -> System.Windows.Controls.DataGridCell
- Avalonia.Controls.Automation.Peers.DataGridCellAutomationPeer -> System.Windows.Automation.Peers.DataGridCellAutomationPeer
- Avalonia.Controls.Primitives.DataGridCellsPresenter -> System.Windows.Controls.Primitives.DataGridCellsPresenter
- Avalonia.Controls.DataGridCheckBoxColumn -> System.Windows.Controls.DataGridCheckBoxColumn
- Avalonia.Controls.DataGridClipboardCellContent -> System.Windows.Controls.DataGridClipboardCellContent
- Avalonia.Controls.DataGridClipboardCopyMode -> System.Windows.Controls.DataGridClipboardCopyMode
- Avalonia.Controls.DataGridColumn -> System.Windows.Controls.DataGridColumn
- Avalonia.Controls.DataGridColumnHeader -> System.Windows.Controls.Primitives.DataGridColumnHeader
- Avalonia.Controls.Automation.Peers.DataGridColumnHeaderAutomationPeer -> System.Windows.Automation.Peers.DataGridColumnHeaderAutomationPeer
- Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter -> System.Windows.Controls.Primitives.DataGridColumnHeadersPresenter
- Avalonia.Controls.Automation.Peers.DataGridColumnHeadersPresenterAutomationPeer -> System.Windows.Automation.Peers.DataGridColumnHeadersPresenterAutomationPeer
- Avalonia.Controls.Primitives.DataGridDetailsPresenter -> System.Windows.Controls.Primitives.DataGridDetailsPresenter
- Avalonia.Controls.Automation.Peers.DataGridDetailsPresenterAutomationPeer -> System.Windows.Automation.Peers.DataGridDetailsPresenterAutomationPeer
- Avalonia.Controls.DataGridEditAction -> System.Windows.Controls.DataGridEditAction
- Avalonia.Controls.DataGridEditingUnit -> System.Windows.Controls.DataGridEditingUnit
- Avalonia.Controls.DataGridHeadersVisibility -> System.Windows.Controls.DataGridHeadersVisibility
- Avalonia.Controls.DataGridLength -> System.Windows.Controls.DataGridLength
- Avalonia.Controls.DataGridLengthConverter -> System.Windows.Controls.DataGridLengthConverter
- Avalonia.Controls.DataGridLengthUnitType -> System.Windows.Controls.DataGridLengthUnitType
- Avalonia.Controls.DataGridRow -> System.Windows.Controls.DataGridRow
- Avalonia.Automation.Peers.DataGridRowAutomationPeer -> System.Windows.Automation.Peers.DataGridRowAutomationPeer
- Avalonia.Controls.DataGridRowDetailsVisibilityMode -> System.Windows.Controls.DataGridRowDetailsVisibilityMode
- Avalonia.Controls.Primitives.DataGridRowHeader -> System.Windows.Controls.Primitives.DataGridRowHeader
- Avalonia.Controls.Primitives.DataGridRowsPresenter -> System.Windows.Controls.Primitives.DataGridRowsPresenter
- Avalonia.Controls.DataGridSelectionMode -> System.Windows.Controls.DataGridSelectionMode
- Avalonia.Controls.DataGridSelectionUnit -> System.Windows.Controls.DataGridSelectionUnit
- Avalonia.Controls.DataGridTemplateColumn -> System.Windows.Controls.DataGridTemplateColumn
- Avalonia.Controls.DataGridTextColumn -> System.Windows.Controls.DataGridTextColumn

---

## Next steps and recommendations

- Review the Avalonia-only list above and confirm whether these types are intended to be part of the public API. If not, mark them internal and re-run the comparator.
- For types that are conceptually similar but live in different namespaces or with slightly different names, consider adding entries to `tools/ApiDiff/configs/datagrid-mappings.json` under `typeAliases` to map the Avalonia name to the WPF counterpart.
- If you want, I can automatically open PR with candidate internalization changes for the clearly-internal helper types (I already applied a conservative batch earlier). Otherwise I can iterate with updated mappings and re-run the comparison.

Report generated from `member-diff-summary.json` on local workspace.
