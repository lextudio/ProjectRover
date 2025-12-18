using Avalonia.Controls;
using System.Composition;

namespace ICSharpCode.ILSpy.Options
{
    [Export]
    public partial class MiscSettingsPanel : UserControl
    {
        public MiscSettingsPanel()
        {
            InitializeComponent();
        }
    }
}
