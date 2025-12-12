using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectRover.ViewModels;

namespace ProjectRover.Views;

public partial class AssemblyCandidateChooserDialog : Window
{
    public AssemblyCandidateChooserDialog()
    {
        InitializeComponent();
        OkButton.Click += OkButton_Click;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as AssemblyCandidateChooserViewModel;
        Close(vm?.Selected);
    }
}
