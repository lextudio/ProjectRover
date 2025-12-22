using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.TomsToolbox.Controls;
using Dock.Model.TomsToolbox.Core;

namespace Dock.Model.TomsToolbox;

/// <summary>
/// Factory for TomsToolbox shim.
/// </summary>
public class Factory : FactoryBase
{
    public Factory()
    {
        VisibleDockableControls = new Dictionary<IDockable, IDockableControl>();
        PinnedDockableControls = new Dictionary<IDockable, IDockableControl>();
        TabDockableControls = new Dictionary<IDockable, IDockableControl>();
        VisibleRootControls = new Dictionary<IDockable, object>();
        PinnedRootControls = new Dictionary<IDockable, object>();
        TabRootControls = new Dictionary<IDockable, object>();
        ToolControls = new Dictionary<IDockable, object>();
        DocumentControls = new Dictionary<IDockable, object>();
        DockControls = new ObservableCollection<IDockControl>();
        HostWindows = new ObservableCollection<IHostWindow>();
    }

    public override IDictionary<IDockable, IDockableControl> VisibleDockableControls { get; }
    public override IDictionary<IDockable, object> VisibleRootControls { get; }
    public override IDictionary<IDockable, IDockableControl> PinnedDockableControls { get; }
    public override IDictionary<IDockable, object> PinnedRootControls { get; }
    public override IDictionary<IDockable, IDockableControl> TabDockableControls { get; }
    public override IDictionary<IDockable, object> TabRootControls { get; }
    public override IDictionary<IDockable, object> ToolControls { get; }
    public override IDictionary<IDockable, object> DocumentControls { get; }
    public override IList<IDockControl> DockControls { get; }
    public override IList<IHostWindow> HostWindows { get; }

    public override IList<T> CreateList<T>(params T[] items) => new ObservableCollection<T>(items);
    public override IRootDock CreateRootDock() => new RootDock
    {
        LeftPinnedDockables = CreateList<IDockable>(),
        RightPinnedDockables = CreateList<IDockable>(),
        TopPinnedDockables = CreateList<IDockable>(),
        BottomPinnedDockables = CreateList<IDockable>()
    };

    public override IProportionalDock CreateProportionalDock() => new ProportionalDock();
    public override IDockDock CreateDockDock() => new DockDock();
    public override IStackDock CreateStackDock() => new StackDock();
    public override IGridDock CreateGridDock() => new GridDock();
    public override IWrapDock CreateWrapDock() => new WrapDock();
    public override IUniformGridDock CreateUniformGridDock() => new UniformGridDock();
    public override IProportionalDockSplitter CreateProportionalDockSplitter() => new ProportionalDockSplitter();
    public override IGridDockSplitter CreateGridDockSplitter() => new GridDockSplitter();
    public override IToolDock CreateToolDock() => new ToolDock();
    public override IDocumentDock CreateDocumentDock() => new DocumentDock();
    public override IDockWindow CreateDockWindow() => new DockWindow();
    public override IRootDock CreateLayout() => CreateRootDock();
    public override IDocument CreateDocument() => new Document();
    public override ITool CreateTool() => new Tool();
}
