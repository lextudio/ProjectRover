using Avalonia.Controls;
using System.Threading.Tasks;
using ICSharpCode.ILSpy.Util;

namespace ICSharpCode.ILSpy.Options
{
    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();
            var settingsService = new SettingsService();
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
    }
}
