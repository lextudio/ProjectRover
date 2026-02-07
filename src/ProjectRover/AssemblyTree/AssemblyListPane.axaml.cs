/// AGPLv3 License

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    public partial class AssemblyListPane : UserControl
    {
        public AssemblyListPane()
        {
            InitializeComponent();
            var treeView = ExplorerTreeView;
            if (treeView != null)
            {
                ContextMenuProvider.Add(treeView);
                treeView.KeyDown += TreeView_KeyDown;
            }
        }

        private void TreeView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var tree = ExplorerTreeView;
                if (tree?.SelectedItem is ICSharpCode.ILSpyX.TreeView.SharpTreeNode node)
                {
                    if (node.CanDelete())
                    {
                        node.Delete();
                        e.Handled = true;
                    }
                }
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is AssemblyTreeModel model)
                model.SetActiveView(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public TreeView ExplorerTreeView => this.FindControl<TreeView>("tree");

        public IDisposable LockUpdates()
        {
            return new NoopDisposable();
        }

        public void ScrollIntoView(SharpTreeNode node)
        {
            ExplorerTreeView?.ScrollIntoView(node);
        }

        internal void FocusNode(SharpTreeNode node)
        {
            if (ExplorerTreeView != null)
            {
                ExplorerTreeView.SelectedItem = node;
                ExplorerTreeView.Focus();
                ExplorerTreeView.ScrollIntoView(node);
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
