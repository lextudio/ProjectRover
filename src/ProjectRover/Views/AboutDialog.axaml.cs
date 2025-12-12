using Avalonia.Controls;
using ProjectRover.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectRover.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
            DataContext = App.Current.Services.GetRequiredService<IAboutWindowViewModel>();
    }
}
