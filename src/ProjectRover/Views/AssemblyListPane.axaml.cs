using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectRover.Views;

public partial class AssemblyListPane : UserControl
{
    public AssemblyListPane()
    {
        InitializeComponent();
    }

    public TreeView ExplorerTreeView => TreeView;
}
