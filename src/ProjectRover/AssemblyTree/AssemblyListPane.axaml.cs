using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    public partial class AssemblyListPane : UserControl
    {
        public AssemblyListPane()
        {
            InitializeComponent();
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
