using System.Collections.ObjectModel;
using Dock.Model.Core;
using DockOrientation = Dock.Model.Core.Orientation;
using Dock.Model.TomsToolbox.Controls;

namespace ICSharpCode.ILSpy.Docking;

public sealed record DefaultDockLayout(
    RootDock Root,
    ToolDock ToolDock,
    DocumentDock DocumentDock);

public static class DefaultDockLayoutBuilder
{
    public static DefaultDockLayout Build(IDockable leftTool, DocumentDock? documentDock = null)
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

        var rightDock = new ProportionalDock
        {
            Id = "RightDock",
            Orientation = DockOrientation.Vertical,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                //searchDock,
                // searchSplitter,
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

        return new DefaultDockLayout(rootDock, toolDock, document);
    }
}
