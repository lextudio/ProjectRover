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
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.Analyzers
{
    public partial class AnalyzerTreeView : UserControl
    {
        private AnalyzerTreeViewModel? viewModel;
        private bool suppressSelectionChanged;

        public AnalyzerTreeView()
        {
            InitializeComponent();

            if (Tree != null)
            {
                ContextMenuProvider.Add(Tree);
                Tree.SelectionChanged += AnalyzerTreeView_OnSelectionChanged;
            }
        }

        private TreeView? Tree => this.FindControl<TreeView>("tree");

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (viewModel != null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            viewModel = DataContext as AnalyzerTreeViewModel;

            if (viewModel != null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            SyncSelectionFromViewModel(focusIfActive: false);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AnalyzerTreeViewModel.SelectedItems)
                || e.PropertyName == nameof(AnalyzerTreeViewModel.IsActive))
            {
                SyncSelectionFromViewModel(focusIfActive: true);
            }
        }

        private void SyncSelectionFromViewModel(bool focusIfActive)
        {
            if (Tree == null || viewModel == null)
            {
                return;
            }

            var selectedNode = viewModel.SelectedItems?.FirstOrDefault();
            if (selectedNode is null)
            {
                return;
            }

            ApplySelectionWithRetry(selectedNode, focusIfActive, attempt: 0);
        }

        private void ApplySelectionWithRetry(AnalyzerTreeNode selectedNode, bool focusIfActive, int attempt)
        {
            if (Tree == null || viewModel == null)
            {
                return;
            }

            // Ignore stale requests after the selection changed again.
            if (!ReferenceEquals(viewModel.SelectedItems?.FirstOrDefault(), selectedNode))
            {
                return;
            }

            suppressSelectionChanged = true;
            selectedNode.IsSelected = true;
            if (!ReferenceEquals(Tree.SelectedItem, selectedNode))
            {
                Tree.SelectedItem = selectedNode;
            }

            if (ReferenceEquals(Tree.SelectedItem, selectedNode))
            {
                Tree.ScrollIntoView(selectedNode);
                if (focusIfActive && viewModel.IsActive)
                {
                    Tree.Focus();
                }
                // Keep suppression for one UI turn to swallow trailing transient SelectionChanged(null).
                Dispatcher.UIThread.Post(() => {
                    suppressSelectionChanged = false;
                    var currentVmSelection = viewModel?.SelectedItems?.FirstOrDefault();
                    if (Tree != null
                        && currentVmSelection != null
                        && !ReferenceEquals(Tree.SelectedItem, currentVmSelection)
                        && attempt < 3)
                    {
                        ApplySelectionWithRetry(currentVmSelection, focusIfActive, attempt + 1);
                        return;
                    }
                }, DispatcherPriority.Background);
                return;
            }

            // On first pane activation, ItemsSource/materialization can lag behind VM updates.
            // if (attempt >= 3)
            // {
            //     suppressSelectionChanged = false;
            //     return;
            // }

            // Dispatcher.UIThread.Post(
            //     () => ApplySelectionWithRetry(selectedNode, focusIfActive, attempt + 1),
            //     DispatcherPriority.Background);
        }

        private void AnalyzerTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not AnalyzerTreeViewModel vm || Tree == null)
            {
                return;
            }

            if (suppressSelectionChanged)
            {
                return;
            }

            var node = e.AddedItems?.OfType<AnalyzerTreeNode>().FirstOrDefault()
                ?? Tree.SelectedItem as AnalyzerTreeNode;

            if (node != null)
            {
                node.IsSelected = true;
                vm.SelectedItems = new[] { node };
                Tree.ScrollIntoView(node);
                if (vm.IsActive)
                {
                    Tree.Focus();
                }
            }
        }
    }
}
