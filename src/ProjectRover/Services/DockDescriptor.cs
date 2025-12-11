using System.Collections.Generic;

namespace ProjectRover.Services;

public record DockPaneDescriptor(
    string Id,
    string Title,
    DockPosition Position,
    bool IsVisibleByDefault,
    bool CanFloat);

public record DockLayoutDescriptor(IReadOnlyList<DockPaneDescriptor> Panes);

public enum DockPosition
{
    Left,
    Right,
    Top,
    Bottom,
    Document
}

public interface IDockLayoutDescriptorProvider
{
    DockLayoutDescriptor GetLayout();
}

public sealed class DefaultDockLayoutDescriptorProvider : IDockLayoutDescriptorProvider
{
    private static readonly IReadOnlyList<DockPaneDescriptor> panes = new[]
    {
        new DockPaneDescriptor("TreePane", "Explorer", DockPosition.Left, IsVisibleByDefault: true, CanFloat: false),
        new DockPaneDescriptor("SearchPane", "Search", DockPosition.Bottom, IsVisibleByDefault: true, CanFloat: true),
        new DockPaneDescriptor("DocumentPane", "Document", DockPosition.Document, IsVisibleByDefault: true, CanFloat: false),
        new DockPaneDescriptor("OutputPane", "Notifications", DockPosition.Bottom, IsVisibleByDefault: false, CanFloat: true)
    };

    public DockLayoutDescriptor GetLayout() => new(panes);
}
