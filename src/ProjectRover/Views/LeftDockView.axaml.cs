using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectRover.Views;

public partial class LeftDockView : UserControl
{
    public LeftDockView()
    {
        InitializeComponent();
    }

    public TreeView ExplorerTreeView => TreeView;
}
