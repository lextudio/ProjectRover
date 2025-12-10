using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;

namespace ProjectRover.Views;

public partial class CenterDockView : UserControl
{
    public CenterDockView()
    {
        InitializeComponent();
    }

    public TextEditor Editor => TextEditor;
}
