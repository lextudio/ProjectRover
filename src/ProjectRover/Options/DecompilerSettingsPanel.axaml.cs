using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Options
{
    public partial class DecompilerSettingsPanel : UserControl
    {
        public DecompilerSettingsPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
