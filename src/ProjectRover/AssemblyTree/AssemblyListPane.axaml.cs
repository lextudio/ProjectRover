/// AGPLv3 License

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    public partial class AssemblyListPane : UserControl
    {
        private static readonly string[] SupportedAssemblyExtensions =
            [".dll", ".exe", ".winmd", ".netmodule"];

        public AssemblyListPane()
        {
            InitializeComponent();
            var treeView = ExplorerTreeView;
            if (treeView != null)
            {
                ContextMenuProvider.Add(treeView);
                treeView.KeyDown += TreeView_KeyDown;
                DragDrop.SetAllowDrop(treeView, true);
                treeView.AddHandler(DragDrop.DragOverEvent, ExplorerTreeView_DragOver);
                treeView.AddHandler(DragDrop.DropEvent, ExplorerTreeView_Drop);
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

        private void ExplorerTreeView_DragOver(object? sender, Avalonia.Input.DragEventArgs e)
        {
            if (GetDroppedAssemblyPaths(e.DataTransfer).Count > 0)
            {
                e.DragEffects = Avalonia.Input.DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = Avalonia.Input.DragDropEffects.None;
            }
        }

        private void ExplorerTreeView_Drop(object? sender, Avalonia.Input.DragEventArgs e)
        {
            if (DataContext is not AssemblyTreeModel model)
                return;

            var files = GetDroppedAssemblyPaths(e.DataTransfer);
            if (files.Count == 0)
                return;

            model.OpenFiles(files.ToArray());
            e.DragEffects = Avalonia.Input.DragDropEffects.Copy;
            e.Handled = true;
        }

        private static List<string> GetDroppedAssemblyPaths(Avalonia.Input.IDataTransfer? dataTransfer)
        {
            if (dataTransfer == null)
                return new List<string>();

            var files = dataTransfer.TryGetFiles();
            if (files == null)
                return new List<string>();

            return files
                .Select(item => item.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Where(path => File.Exists(path))
                .Where(IsSupportedAssemblyFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsSupportedAssemblyFile(string path)
        {
            return SupportedAssemblyExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase);
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
