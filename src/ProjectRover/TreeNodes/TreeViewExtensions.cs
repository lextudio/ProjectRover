using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy
{
    public static class TreeViewExtensions
    {
        /// <summary>
        /// Returns the selected items that do not have any of their ancestors selected.
        /// Mirrors SharpTreeView.GetTopLevelSelection semantics when using regular TreeView.
        /// </summary>
        public static IEnumerable<SharpTreeNode> GetTopLevelSelection(this TreeView treeView)
        {
            if (treeView == null)
                yield break;

            // Try to get selected items via IList if available (ListBox/TreeView exposes SelectedItems in some implementations)
            var selectedItemsProp = treeView.GetValue(TreeView.SelectedItemProperty);

            // Many usages expect multiple selection; try to find SelectedItems property via dynamic cast
            var multiSelector = treeView as dynamic;
            IEnumerable<SharpTreeNode> selection = Enumerable.Empty<SharpTreeNode>();
            try {
                if (multiSelector != null)
                {
                    var selectedItems = (IEnumerable<object>)multiSelector.SelectedItems;
                    selection = selectedItems.OfType<SharpTreeNode>();
                }
            }
            catch
            {
                // Fallback to single SelectedItem
                if (treeView.SelectedItem is SharpTreeNode singleNode)
                    selection = new[] { singleNode };
            }

            var selectionSet = new HashSet<SharpTreeNode>(selection);
            foreach (var item in selection)
            {
                bool hasAncestorSelected = false;
                for (var a = item.Parent; a != null; a = a.Parent)
                {
                    if (selectionSet.Contains(a))
                    {
                        hasAncestorSelected = true;
                        break;
                    }
                }
                if (!hasAncestorSelected)
                    yield return item;
            }
        }

        /// <summary>
        /// Returns a disposable lock object that can be used to suppress update handling temporarily.
        /// For Avalonia TreeView this is a no-op disposable that mirrors the WPF API used by callers.
        /// </summary>
        public static IDisposable LockUpdates(this TreeView treeView)
        {
            // TODO: Consider implementing a selection-preserving lock here.
            //       Current implementation returns a no-op disposable so callers
            //       that expect a LockUpdates token compile and run.
            //
            //       Improvements:
            //       - Selection-preserving: capture current `SharpTreeNode` selection
            //         and restore it on Dispose (works without flattener changes).
            //       - Flattener-aware: add an `updatesLocked` flag to the tree flattener
            //         and suppress intermediate selection/focus updates while locked
            //         (mirrors original WPF behavior more closely).
            return NoopDisposable.Instance;
        }
    }

    internal sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new NoopDisposable();
        private NoopDisposable() { }
        public void Dispose() { }
    }
}
