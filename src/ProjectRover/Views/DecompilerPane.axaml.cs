using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;

namespace ProjectRover.Views;

public partial class DecompilerPane : UserControl
{
    public DecompilerPane()
    {
        InitializeComponent();
    }

    public TextEditor Editor => TextEditor;
}
