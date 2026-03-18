// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
