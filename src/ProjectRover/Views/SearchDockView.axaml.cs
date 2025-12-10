using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectRover.Views;

public partial class SearchDockView : UserControl
{
    public SearchDockView()
    {
        InitializeComponent();
    }

    public TextBox SearchTextBoxControl => SearchTextBox;

    private void OnClearSearchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SearchText = string.Empty;
        }

        SearchTextBox.Focus();
    }
}
