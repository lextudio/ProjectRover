using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Dock.Model.Core;
using DockOrientation = Dock.Model.Core.Orientation;
using Dock.Model.TomsToolbox.Controls;
using Dock.Model.Controls;

namespace ICSharpCode.ILSpy.Docking;

public sealed record DefaultDockLayout(
    RootDock Root,
    ToolDock ToolDock,
    DocumentDock DocumentDock);

public static class DefaultDockLayoutBuilder
{
    public static DefaultDockLayout Build(ITool leftTool, DocumentDock? documentDock = null)
    {
        return Build(new[] { leftTool }, documentDock);
    }

    public static DefaultDockLayout Build(IEnumerable<ITool> leftTools, DocumentDock? documentDock = null)
    {
        var tools = leftTools?.Where(t => t != null).ToList() ?? [];
        if (tools.Count == 0)
            throw new ArgumentException("At least one tool is required to build the default dock layout.", nameof(leftTools));

        var document = documentDock ?? new DocumentDock
        {
            Id = "DocumentDock",
            Title = "DocumentDock",
            VisibleDockables = new ObservableCollection<IDockable>()
        };

        var primaryTool = tools[0];

        var toolDock = new ToolDock
        {
            Id = "LeftDock",
            Title = "LeftDock",
            Alignment = Alignment.Left,
            VisibleDockables = new ObservableCollection<IDockable>(tools),
            ActiveDockable = primaryTool,
            DefaultDockable = primaryTool,
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
