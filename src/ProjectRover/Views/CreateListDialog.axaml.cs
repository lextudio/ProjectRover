using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ILSpy.Shims.Views
{
    public partial class CreateListDialog : UserControl
    {
        public CreateListDialog()
        {
            InitializeComponent();

            OkButton.Click += OkButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

        public string ListName
        {
            get => NameBox.Text ?? string.Empty;
            set => NameBox.Text = value;
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            // In real app this would close dialog with OK result.
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            // In real app this would close dialog with Cancel result.
        }
    }
}
