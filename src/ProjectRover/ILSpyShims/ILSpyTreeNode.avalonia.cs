using ICSharpCode.ILSpyX.TreeView.PlatformAbstractions;

namespace ICSharpCode.ILSpy.TreeNodes
{
	public partial class ILSpyTreeNode
	{
		public override void ActivateItemSecondary(IPlatformRoutedEventArgs e)
		{
			var assemblyTreeModel = AssemblyTreeModel;

			assemblyTreeModel.SelectNode(this, inNewTabPage: true);

			Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
				assemblyTreeModel.RefreshDecompiledView,
				Avalonia.Threading.DispatcherPriority.Background);
		}
	}
}