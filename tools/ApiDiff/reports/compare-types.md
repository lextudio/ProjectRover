# DataGrid API comparison — type classification

This file lists types found in the Avalonia DataGrid manifests vs the .NET 10 PresentationFramework reference manifest used for comparison.

Generated from: `tools/ApiDiff/reports/member-diff-summary.json`

Summary:

- Related types: 63
- Avalonia-only:  (types present in Avalonia manifest but not in WPF)
- WPF-only:      (types present in WPF manifest but not in Avalonia) — none in this scoped comparison
- Matched pairs: types that map to a WPF counterpart (Left -> Right)

---

## Avalonia-only types (left only)

These types appear in the Avalonia DataGrid manifest and have no mapped counterpart in the WPF PresentationFramework ref manifest.

Count: 18

- Avalonia.Collections.DataGridCollectionView
- Avalonia.Collections.DataGridCollectionViewGroup
- DataGridExtensions.DataGridColumnExtensions
- Avalonia.Collections.DataGridComparerSortDescription
- Avalonia.Controls.DataGridCellEditEndedEventArgs
- Avalonia.Controls.DataGridCellHitTestResult
- Avalonia.Controls.DataGridCellPointerPressedEventArgs
- Avalonia.Collections.DataGridCurrentChangingEventArgs
- Avalonia.Collections.DataGridGroupDescription
- Avalonia.Collections.DataGridPathGroupDescription
- DataGridExtensions.DataGridFilter
- Avalonia.Controls.DataGridFilterKind
- Avalonia.Controls.Primitives.DataGridFrozenGrid
- Avalonia.Collections.DataGridSortDescription
- Avalonia.Collections.DataGridSortDescriptionCollection
- Avalonia.Controls.DataGridContextMenuEventArgs
- Avalonia.Controls.DataGridRowEditEndedEventArgs
- Avalonia.Controls.DataGridRowGroupHeader
- Avalonia.Controls.DataGridRowGroupHeaderEventArgs

Notes: many of these are collection, group- or helper types that the comparator flagged as Avalonia-only. Consider whether these should be internal or included in mapping aliases.

## WPF-only types (right only)

Count: 0

No types were discovered that exist in WPF and not in Avalonia within the filtered comparison scope.

## Matched pairs (Avalonia -> WPF)

Count: 45

The following types were considered related by namespace mappings / name aliasing and are listed as Left -> Right.

- Avalonia.Controls.DataGrid -> System.Windows.Controls.DataGrid
- Avalonia.Controls.DataGridAutoGeneratingColumnEventArgs -> System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs
- Avalonia.Controls.Automation.Peers.DataGridAutomationPeer -> System.Windows.Automation.Peers.DataGridAutomationPeer
- Avalonia.Controls.DataGridBeginningEditEventArgs -> System.Windows.Controls.DataGridBeginningEditEventArgs
- Avalonia.Controls.DataGridBoundColumn -> System.Windows.Controls.DataGridBoundColumn
- Avalonia.Controls.DataGridCell -> System.Windows.Controls.DataGridCell
- Avalonia.Controls.Automation.Peers.DataGridCellAutomationPeer -> System.Windows.Automation.Peers.DataGridCellAutomationPeer
- Avalonia.Controls.DataGridCellEditEndingEventArgs -> System.Windows.Controls.DataGridCellEditEndingEventArgs
- Avalonia.Controls.Primitives.DataGridCellsPresenter -> System.Windows.Controls.Primitives.DataGridCellsPresenter
- Avalonia.Controls.DataGridCheckBoxColumn -> System.Windows.Controls.DataGridCheckBoxColumn
- Avalonia.Controls.DataGridClipboardCellContent -> System.Windows.Controls.DataGridClipboardCellContent
- Avalonia.Controls.DataGridClipboardCopyMode -> System.Windows.Controls.DataGridClipboardCopyMode
- Avalonia.Controls.DataGridColumn -> System.Windows.Controls.DataGridColumn
- Avalonia.Controls.DataGridColumnEventArgs -> System.Windows.Controls.DataGridColumnEventArgs
- Avalonia.Controls.DataGridColumnHeader -> System.Windows.Controls.Primitives.DataGridColumnHeader
- Avalonia.Controls.Automation.Peers.DataGridColumnHeaderAutomationPeer -> System.Windows.Automation.Peers.DataGridColumnHeaderAutomationPeer
- Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter -> System.Windows.Controls.Primitives.DataGridColumnHeadersPresenter
- Avalonia.Controls.Automation.Peers.DataGridColumnHeadersPresenterAutomationPeer -> System.Windows.Automation.Peers.DataGridColumnHeadersPresenterAutomationPeer
- Avalonia.Controls.DataGridColumnReorderingEventArgs -> System.Windows.Controls.DataGridColumnReorderingEventArgs
- Avalonia.Controls.Primitives.DataGridDetailsPresenter -> System.Windows.Controls.Primitives.DataGridDetailsPresenter
- Avalonia.Controls.Automation.Peers.DataGridDetailsPresenterAutomationPeer -> System.Windows.Automation.Peers.DataGridDetailsPresenterAutomationPeer
- Avalonia.Controls.DataGridEditAction -> System.Windows.Controls.DataGridEditAction
- Avalonia.Controls.DataGridEditingUnit -> System.Windows.Controls.DataGridEditingUnit
- Avalonia.Controls.DataGridHeadersVisibility -> System.Windows.Controls.DataGridHeadersVisibility
- Avalonia.Controls.DataGridLength -> System.Windows.Controls.DataGridLength
- Avalonia.Controls.DataGridLengthConverter -> System.Windows.Controls.DataGridLengthConverter
- Avalonia.Controls.DataGridLengthUnitType -> System.Windows.Controls.DataGridLengthUnitType
- Avalonia.Controls.DataGridPreparingCellForEditEventArgs -> System.Windows.Controls.DataGridPreparingCellForEditEventArgs
- Avalonia.Controls.DataGridRow -> System.Windows.Controls.DataGridRow
- Avalonia.Automation.Peers.DataGridRowAutomationPeer -> System.Windows.Automation.Peers.DataGridRowAutomationPeer
- Avalonia.Controls.DataGridRowClipboardEventArgs -> System.Windows.Controls.DataGridRowClipboardEventArgs
- Avalonia.Controls.DataGridRowDetailsEventArgs -> System.Windows.Controls.DataGridRowDetailsEventArgs
- Avalonia.Controls.DataGridRowDetailsVisibilityMode -> System.Windows.Controls.DataGridRowDetailsVisibilityMode
- Avalonia.Controls.DataGridRowEditEndingEventArgs -> System.Windows.Controls.DataGridRowEditEndingEventArgs
- Avalonia.Controls.DataGridRowEventArgs -> System.Windows.Controls.DataGridRowEventArgs
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
