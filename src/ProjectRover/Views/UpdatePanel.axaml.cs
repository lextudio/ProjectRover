using Avalonia.Controls;
using ProjectRover.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectRover.Views;

public partial class UpdatePanel : UserControl
{
    public UpdatePanel()
    {
        InitializeComponent();
        
        if (!Design.IsDesignMode)
            DataContext = App.Current.Services.GetRequiredService<IUpdatePanelViewModel>();
    }
}
