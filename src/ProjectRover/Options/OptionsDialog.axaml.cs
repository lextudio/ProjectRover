using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using ICSharpCode.ILSpy.Util;

namespace ICSharpCode.ILSpy.Options
{
    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();
            var settingsService = ProjectRover.App.ExportProvider?.GetExportedValue<SettingsService>() ?? new SettingsService();
            DataContext = new OptionsDialogViewModel(settingsService);
        }

        public OptionsDialog(SettingsService settingsService)
        {
            InitializeComponent();
            DataContext = new OptionsDialogViewModel(settingsService);
        }

        public Task<bool?> ShowDialogAsync(Window? owner = null)
        {
            if (owner != null)
                this.Owner = owner;
            return this.ShowDialog<bool?>(owner);
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is OptionsDialogViewModel viewModel
                && viewModel.CommitCommand.CanExecute(null))
            {
                viewModel.CommitCommand.Execute(null);
            }

            Close(true);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
