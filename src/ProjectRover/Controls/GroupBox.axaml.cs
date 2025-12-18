using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectRover.Controls;

public partial class GroupBox : UserControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<GroupBox, object?>(nameof(Header));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public GroupBox()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
