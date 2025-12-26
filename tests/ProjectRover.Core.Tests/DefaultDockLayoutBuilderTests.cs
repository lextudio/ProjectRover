using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.TomsToolbox.Controls;
using ICSharpCode.ILSpy.Docking;
using Xunit;

namespace ProjectRover.Core.Tests;

public class DefaultDockLayoutBuilderTests
{
    [Fact]
    public void Build_CreatesExpectedStructure()
    {
        var assemblyTool = new Tool { Id = "Assembly", Title = "Assembly" };
        var searchTool = new Tool { Id = "Search", Title = "Search" };

        var layout = DefaultDockLayoutBuilder.Build(assemblyTool, searchTool);

        // The root dock should carry the canonical ID so tooling can find it.
        Assert.Equal("Root", layout.Root.Id);
        Assert.Equal("LeftDock", layout.ToolDock.Id);
        // Tool dock must host the assembly tool as it represents the explorer pane.
        Assert.Contains(assemblyTool, layout.ToolDock.VisibleDockables!);
        Assert.Equal("SearchDock", layout.SearchDock.Id);
        // Search pane is hidden by default until the user opens it.
        Assert.False(layout.SearchDock.IsVisible);
        Assert.Equal(searchTool, layout.SearchDock.ActiveDockable);
        Assert.Equal("DocumentDock", layout.DocumentDock.Id);

        var mainLayout = layout.Root.VisibleDockables!.OfType<ProportionalDock>().First();
        // The top-level horizontal proportional dock defines the split between panes.
        Assert.Equal("MainLayout", mainLayout.Id);

        var rightDock = mainLayout.VisibleDockables!.OfType<ProportionalDock>().First(d => d.Id == "RightDock");
        // Right dock should aggregate search, splitter, and document panes in order.
        Assert.Contains(layout.DocumentDock, rightDock.VisibleDockables!);
        Assert.Contains(layout.SearchDock, rightDock.VisibleDockables!);
        Assert.Contains(layout.SearchSplitter, rightDock.VisibleDockables!);
    }
}
