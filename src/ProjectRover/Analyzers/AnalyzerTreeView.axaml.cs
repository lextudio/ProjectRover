using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.Analyzers
{
    public partial class AnalyzerTreeView : UserControl
    {
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

        private void AnalyzerTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not AnalyzerTreeViewModel vm || Tree == null)
                return;

            if (Tree.SelectedItem is AnalyzerTreeNode node)
            {
                vm.SelectedItems = new[] { node };
                Tree.ScrollIntoView(node);
            }
            else
            {
                vm.SelectedItems = Array.Empty<AnalyzerTreeNode>();
            }
        }
    }
}
