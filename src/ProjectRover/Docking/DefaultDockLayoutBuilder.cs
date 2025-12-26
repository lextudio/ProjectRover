using System.Collections.ObjectModel;
using Dock.Model.Core;
using DockOrientation = Dock.Model.Core.Orientation;
using Dock.Model.TomsToolbox.Controls;

namespace ICSharpCode.ILSpy.Docking;

public sealed record DefaultDockLayout(
    RootDock Root,
    ToolDock ToolDock,
    ToolDock SearchDock,
    DocumentDock DocumentDock,
    ProportionalDockSplitter SearchSplitter);

public static class DefaultDockLayoutBuilder
{
    public static DefaultDockLayout Build(IDockable leftTool, IDockable searchTool, DocumentDock? documentDock = null)
    {
        var document = documentDock ?? new DocumentDock
        {
            Id = "DocumentDock",
            Title = "DocumentDock",
            VisibleDockables = new ObservableCollection<IDockable>()
        };

        var toolDock = new ToolDock
        {
            Id = "LeftDock",
            Title = "LeftDock",
            Alignment = Alignment.Left,
            VisibleDockables = new ObservableCollection<IDockable> { leftTool },
            ActiveDockable = leftTool,
            DefaultDockable = leftTool,
            Proportion = 0.3
        };

        var searchDock = new ToolDock
        {
            Id = "SearchDock",
            Title = "Search",
            Alignment = Alignment.Top,
            VisibleDockables = new ObservableCollection<IDockable> { searchTool },
            ActiveDockable = searchTool,
            DefaultDockable = searchTool,
            CanCloseLastDockable = true,
            Proportion = 0.5,
            IsVisible = false
        };

        var searchSplitter = new ProportionalDockSplitter
        {
            Id = "SearchSplitter",
            CanResize = true
        };

        var rightDock = new ProportionalDock
        {
            Id = "RightDock",
            Orientation = DockOrientation.Vertical,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                searchDock,
                searchSplitter,
                document
            },
            ActiveDockable = document
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = DockOrientation.Horizontal,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                toolDock,
                new ProportionalDockSplitter { CanResize = true },
                rightDock
            },
            ActiveDockable = rightDock
        };

        var rootDock = new RootDock
        {
            Id = "Root",
            Title = "Root",
            VisibleDockables = new ObservableCollection<IDockable> { mainLayout },
            ActiveDockable = mainLayout
        };

        return new DefaultDockLayout(rootDock, toolDock, searchDock, document, searchSplitter);
    }
}
