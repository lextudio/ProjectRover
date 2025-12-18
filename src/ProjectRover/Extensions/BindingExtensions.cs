using Avalonia.Controls;

namespace ICSharpCode.ILSpy
{
    public static class BindingExtensions
    {
        public static void SetBinding(this Control toggleButton, Avalonia.AvaloniaProperty property, Binding binding)
        {
            toggleButton.Bind(property, binding);
        }
    }
}
