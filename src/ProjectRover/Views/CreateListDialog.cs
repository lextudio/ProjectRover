using System;
using System.ComponentModel;

namespace ICSharpCode.ILSpy
{
    // Lightweight shim that exposes the same API used by ViewModels.
    // This is not a visual window; it's a minimal dialog-like object used by the shim project.
    public class CreateListDialog
    {
        public object? Owner { get; set; }

        public event EventHandler<CancelEventArgs>? Closing;

        public ListNameBoxShim ListNameBox { get; } = new ListNameBoxShim();

        public string ListName
        {
            get => ListNameBox.Text ?? string.Empty;
            set => ListNameBox.Text = value;
        }

        public bool? DialogResult { get; set; }

        public CreateListDialog(string title)
        {
            // title is ignored in shim
        }

        public bool? ShowDialog()
        {
            // In the shim, just return DialogResult; calling code sets/reads DialogResult via handlers
            return DialogResult;
        }

        public void RaiseClosing(CancelEventArgs e)
        {
            Closing?.Invoke(this, e);
        }
    }

    public class ListNameBoxShim
    {
        public string? Text { get; set; }

        public void SelectAll()
        {
            // no-op for shim
        }

        public event EventHandler? TextChanged;

        public void RaiseTextChanged()
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
