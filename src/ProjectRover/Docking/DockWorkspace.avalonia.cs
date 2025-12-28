using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpy.Search;
using ICSharpCode.ILSpy.Util;
using Dock.Model.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.TomsToolbox.Controls;
using Dock.Model.TomsToolbox;
using Dock.Model.TomsToolbox.Core;
using ProjectRover.Settings;
using System.Windows.Threading;

namespace ICSharpCode.ILSpy.Docking
{
  /// <summary>
  /// Avalonia-specific extensions to <see cref="DockWorkspace"/>.
  /// </summary>
  public partial class DockWorkspace
  {
    private IFactory? currentFactory;
    private readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Dock");
    private DockControl? dockHost;
    private IDocumentDock? documentDock;
    private IFactory? factory;
    private ProjectRoverSettingsSection? roverSettings;
    private static readonly Type[] dockLayoutKnownTypes = new[]
    {
      typeof(RootDock),
      typeof(ProportionalDock),
      typeof(DockDock),
      typeof(StackDock),
      typeof(GridDock),
      typeof(WrapDock),
      typeof(UniformGridDock),
      typeof(ToolDock),
      typeof(DocumentDock),
      typeof(ProportionalDockSplitter),
      typeof(GridDockSplitter),
      typeof(Tool),
      typeof(Document),
      typeof(DockWindow)
    };

    public void InitializeLayout()
    {
      try
      {
        var app = Avalonia.Application.Current;
        var mainWindow = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                         ?? Avalonia.Controls.TopLevel.GetTopLevel(null) as Window;
        if (mainWindow == null)
          return;

        var dockHost = mainWindow.FindControl<DockControl>("DockHost");
        if (dockHost == null)
          return;

        // If layout is already initialized, reuse it but hook listeners
        if (dockHost.Layout != null && dockHost.Factory != null)
        {
          ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("DockWorkspace.InitializeLayout: Layout already initialized, hooking listeners");
          var docDock1 = FindDockById(dockHost.Layout, "DocumentDock") as IDocumentDock;
          AttachToDockHost(dockHost, dockHost.Factory, docDock1);
          HookUpToolListeners(dockHost);
          return;
        }

        var viewModel = mainWindow.DataContext as MainWindowViewModel;
        if (viewModel == null)
        {
          ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("DockWorkspace.InitializeLayout: ViewModel is null, cannot initialize specific layout.");
          return;
        }

        var roverSettings = GetRoverSettings();
        if (!roverSettings.UseDefaultDockLayoutOnly && TryRestoreLayout(dockHost))
        {
          return;
        }

        ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("DockWorkspace.InitializeLayout: Creating specific layout structure");

        var assemblyTreeModel = viewModel.AssemblyTreeModel;
        var searchPaneModel = viewModel.SearchPaneModel;

        // Create Dock Structure
        var documentDock = this.CreateDocumentDock() ?? new DocumentDock
        {
          Id = "DocumentDock",
          Title = "DocumentDock",
          VisibleDockables = new ObservableCollection<IDockable>()
        };

        this.RegisterTool(assemblyTreeModel);

        searchPaneModel.IsVisible = false;
        searchPaneModel.IsActive = false;
        this.RegisterTool(searchPaneModel);

        var layout = DefaultDockLayoutBuilder.Build(assemblyTreeModel, documentDock);
        documentDock = layout.DocumentDock;

        var factory = new Factory();
        dockHost.Factory = factory;
        dockHost.InitializeFactory = true;
        dockHost.InitializeLayout = true;
        dockHost.Layout = layout.Root;

        AttachToDockHost(dockHost, factory, documentDock);
        HookUpToolListeners(dockHost);

        dockHost.IsVisible = true;

        ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("DockWorkspace.InitializeLayout: Specific layout initialized successfully");
      }
      catch (Exception ex)
      {
        ICSharpCode.ILSpy.Util.RoverLog.Log.Error(ex, "DockWorkspace.InitializeLayout error");
      }
    }

    public void SaveLayout()
    {
      try
      {
        var host = dockHost;
        if (host?.Layout is not IRootDock rootLayout)
          return;

        if (GetRoverSettings().UseDefaultDockLayoutOnly)
        {
          ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("DockWorkspace.SaveLayout: Skipped because Rover uses the default layout only.");
          return;
        }

        var snapshot = BuildLayoutSnapshot(rootLayout);
        if (snapshot == null)
          return;

        var serializer = CreateLayoutSerializer();
        var layoutXml = serializer.Serialize(snapshot);
        if (!string.IsNullOrWhiteSpace(layoutXml))
        {
          GetRoverSettings().DockLayout = layoutXml;
        }
      }
      catch (Exception ex)
      {
        ICSharpCode.ILSpy.Util.RoverLog.Log.Error(ex, "DockWorkspace.SaveLayout error");
      }
    }

    private ProjectRoverSettingsSection GetRoverSettings()
    {
      roverSettings ??= exportProvider.GetExportedValue<SettingsService>()
        .GetSettings<ProjectRoverSettingsSection>();
      return roverSettings;
    }

    private bool TryRestoreLayout(DockControl dockHost)
    {
      var layoutXml = GetRoverSettings().DockLayout;
      if (string.IsNullOrWhiteSpace(layoutXml))
        return false;

      try
      {
        var serializer = CreateLayoutSerializer();
        var layout = serializer.Deserialize<RootDock>(layoutXml);
        if (layout == null)
          return false;

        RegisterKnownTools();
        ReplaceToolDockables(layout);
        ResetDocumentDock(layout);

        if (FindDockableById(layout, "SearchDock") is IDockable searchDock)
        {
          if (searchDock.Proportion > 0.9)
          {
            searchDock.Proportion = 0.33;
          }
        }

        if (FindDockableById(layout, "DocumentDock") is IDockable foundDocDock)
        {
          if (double.IsNaN(foundDocDock.Proportion) || foundDocDock.Proportion < 0.1)
          {
            foundDocDock.Proportion = 0.66;
          }
          foundDocDock.IsCollapsable = false;
        }

        RegisterDockablesForActivation(layout);

        var rightDock = FindDockById(layout, "RightDock");
        log.Debug("TryRestoreLayout: RightDock found: {Found}", rightDock != null);

        var factory = new Factory();
        dockHost.Factory = factory;
        dockHost.InitializeFactory = true;
        dockHost.InitializeLayout = true;
        dockHost.Layout = layout;

        var docDock = FindDockById(layout, "DocumentDock") as IDocumentDock;
        AttachToDockHost(dockHost, factory, docDock);
        HookUpToolListeners(dockHost);

        dockHost.IsVisible = true;
        log.Debug("InitializeLayout: Restored layout from ProjectRover settings");
        return true;
      }
      catch (Exception ex)
      {
        log.Error(ex, "InitializeLayout: Failed to restore layout");
        return false;
      }
    }

    private void RegisterKnownTools()
    {
      foreach (var tool in ToolPanes)
      {
        RegisterTool(tool);
      }
    }

    private void RegisterDockablesForActivation(IDockable layout)
    {
      var searchDock = FindDockableById(layout, "SearchDock") as ToolDock;
      log.Debug("RegisterDockablesForActivation: SearchDock found in layout: {Found}", searchDock != null);
      if (searchDock == null)
      {
        searchDock = new ToolDock
        {
          Id = "SearchDock",
          Title = "Search",
          Alignment = Alignment.Top,
          VisibleDockables = new ObservableCollection<IDockable>(),
          CanCloseLastDockable = true,
          Proportion = 0.5
        };

        // If SearchDock wasn't in the layout, ensure all search tools are marked invisible
        var searchPane = ToolPanes.FirstOrDefault(t => t.ContentId == SearchPaneModel.PaneContentId);
        if (searchPane != null && searchPane.IsVisible)
        {
          log.Debug("SearchDock missing, resetting searchPane.IsVisible to false");
          searchPane.IsVisible = false;
        }
      }
      RegisterDockable(searchDock);

      var searchSplitter = FindDockableById(layout, "SearchSplitter") as ProportionalDockSplitter;
      if (searchSplitter == null)
      {
        searchSplitter = new ProportionalDockSplitter { Id = "SearchSplitter", CanResize = true };
      }
      RegisterDockable(searchSplitter);
    }

    private void ReplaceToolDockables(IDockable root)
    {
      var toolsById = ToolPanes
        .Where(tool => !string.IsNullOrWhiteSpace(tool.Id))
        .ToDictionary(tool => tool.Id, StringComparer.Ordinal);

      ReplaceToolDockables(root, toolsById);
    }

    private static void ReplaceToolDockables(IDockable root, IReadOnlyDictionary<string, ToolPaneModel> toolsById)
    {
      if (root is IDock dock && dock.VisibleDockables != null)
      {
        for (int i = dock.VisibleDockables.Count - 1; i >= 0; i--)
        {
          var dockable = dock.VisibleDockables[i];
          if (dockable is IDock nestedDock)
          {
            ReplaceToolDockables(nestedDock, toolsById);
            continue;
          }

          if (dockable is Tool tool)
          {
            if (tool.Id != null && toolsById.TryGetValue(tool.Id, out var actualTool))
            {
              actualTool.IsVisible = tool.IsVisible;
              actualTool.IsActive = tool.IsActive;
              dock.VisibleDockables[i] = actualTool;
            }
            else
            {
              dock.VisibleDockables.RemoveAt(i);
            }
          }
        }

        dock.ActiveDockable = RemapDockable(dock.ActiveDockable, toolsById);
        dock.DefaultDockable = RemapDockable(dock.DefaultDockable, toolsById);
        dock.FocusedDockable = RemapDockable(dock.FocusedDockable, toolsById);
      }

      if (root is RootDock rootDock)
      {
        ReplaceDockableList(rootDock.HiddenDockables, toolsById);
        ReplaceDockableList(rootDock.LeftPinnedDockables, toolsById);
        ReplaceDockableList(rootDock.RightPinnedDockables, toolsById);
        ReplaceDockableList(rootDock.TopPinnedDockables, toolsById);
        ReplaceDockableList(rootDock.BottomPinnedDockables, toolsById);
        if (rootDock.PinnedDock != null)
          ReplaceToolDockables(rootDock.PinnedDock, toolsById);
      }
    }

    private static IDockable? RemapDockable(IDockable? dockable, IReadOnlyDictionary<string, ToolPaneModel> toolsById)
    {
      if (dockable is ITool tool && tool.Id != null && toolsById.TryGetValue(tool.Id, out var actualTool))
        return actualTool;

      return dockable is ITool ? null : dockable;
    }

    private static void ReplaceDockableList(IList<IDockable>? dockables, IReadOnlyDictionary<string, ToolPaneModel> toolsById)
    {
      if (dockables == null)
        return;

      for (int i = dockables.Count - 1; i >= 0; i--)
      {
        var dockable = dockables[i];
        if (dockable is IDock dock)
        {
          ReplaceToolDockables(dock, toolsById);
          continue;
        }

        if (dockable is Tool tool)
        {
          if (tool.Id != null && toolsById.TryGetValue(tool.Id, out var actualTool))
          {
            actualTool.IsVisible = tool.IsVisible;
            actualTool.IsActive = tool.IsActive;
            dockables[i] = actualTool;
          }
          else
          {
            dockables.RemoveAt(i);
          }
        }
        else if (dockable is IDocument)
        {
          dockables.RemoveAt(i);
        }
      }
    }

    private static void ResetDocumentDock(IDockable root)
    {
      if (FindDockById(root, "DocumentDock") is IDocumentDock docDock)
      {
        docDock.VisibleDockables?.Clear();
        docDock.ActiveDockable = null;
        docDock.DefaultDockable = null;
        docDock.FocusedDockable = null;
        docDock.Proportion = double.NaN;
      }
    }

    private static RootDock? BuildLayoutSnapshot(IRootDock layout)
    {
      // Clone the layout with stub dockables so we don't serialize view-model types.
      var map = new Dictionary<IDockable, IDockable>();
      return CloneDockable(layout, map) as RootDock;
    }

    private static DockLayoutXmlSerializer CreateLayoutSerializer()
    {
      return new DockLayoutXmlSerializer(typeof(ObservableCollection<>), dockLayoutKnownTypes);
    }

    private static IDockable? CloneDockable(IDockable source, IDictionary<IDockable, IDockable> map)
    {
      if (map.TryGetValue(source, out var existing))
        return existing;

      IDockable? clone = source switch
      {
        IRootDock => new RootDock(),
        IProportionalDock => new ProportionalDock(),
        IToolDock => new ToolDock(),
        IDocumentDock => new DocumentDock(),
        IProportionalDockSplitter => new ProportionalDockSplitter(),
        IDocument => null,
        ITool => new Tool(),
        _ => null
      };

      if (clone == null)
        return null;

      map[source] = clone;
      CopyDockableProperties(source, clone);

      if (source is ToolDock sourceToolDock && clone is ToolDock cloneToolDock)
      {
        cloneToolDock.Alignment = sourceToolDock.Alignment;
        cloneToolDock.IsExpanded = sourceToolDock.IsExpanded;
        cloneToolDock.AutoHide = sourceToolDock.AutoHide;
        cloneToolDock.GripMode = sourceToolDock.GripMode;
      }

      if (source is DocumentDock sourceDocDock && clone is DocumentDock cloneDocDock)
      {
        cloneDocDock.CanCreateDocument = sourceDocDock.CanCreateDocument;
        cloneDocDock.EnableWindowDrag = sourceDocDock.EnableWindowDrag;
        cloneDocDock.TabsLayout = sourceDocDock.TabsLayout;
      }

      if (source is ProportionalDock sourceProportional && clone is ProportionalDock cloneProportional)
      {
        cloneProportional.Orientation = sourceProportional.Orientation;
      }

      if (source is ProportionalDockSplitter sourceSplitter && clone is ProportionalDockSplitter cloneSplitter)
      {
        cloneSplitter.CanResize = sourceSplitter.CanResize;
        cloneSplitter.ResizePreview = sourceSplitter.ResizePreview;
      }

      if (source is RootDock sourceRoot && clone is RootDock cloneRoot)
      {
        cloneRoot.IsFocusableRoot = sourceRoot.IsFocusableRoot;
        cloneRoot.EnableAdaptiveGlobalDockTargets = sourceRoot.EnableAdaptiveGlobalDockTargets;
        cloneRoot.HiddenDockables = CloneDockablesList(sourceRoot.HiddenDockables, map);
        cloneRoot.LeftPinnedDockables = CloneDockablesList(sourceRoot.LeftPinnedDockables, map);
        cloneRoot.RightPinnedDockables = CloneDockablesList(sourceRoot.RightPinnedDockables, map);
        cloneRoot.TopPinnedDockables = CloneDockablesList(sourceRoot.TopPinnedDockables, map);
        cloneRoot.BottomPinnedDockables = CloneDockablesList(sourceRoot.BottomPinnedDockables, map);
        if (sourceRoot.PinnedDock != null)
          cloneRoot.PinnedDock = CloneDockable(sourceRoot.PinnedDock, map) as IToolDock;
      }

      if (source is IDock sourceDock && clone is IDock cloneDock)
      {
        if (sourceDock.VisibleDockables != null)
        {
          var visibleDockables = new ObservableCollection<IDockable>();
          foreach (var dockable in sourceDock.VisibleDockables)
          {
            var dockableClone = CloneDockable(dockable, map);
            if (dockableClone != null)
              visibleDockables.Add(dockableClone);
          }
          cloneDock.VisibleDockables = visibleDockables;
        }

        cloneDock.ActiveDockable = MapDockable(sourceDock.ActiveDockable, map);
        cloneDock.DefaultDockable = MapDockable(sourceDock.DefaultDockable, map);
        cloneDock.FocusedDockable = MapDockable(sourceDock.FocusedDockable, map);
      }

      return clone;
    }

    private static IList<IDockable>? CloneDockablesList(IList<IDockable>? source, IDictionary<IDockable, IDockable> map)
    {
      if (source == null)
        return null;

      var list = new ObservableCollection<IDockable>();
      foreach (var dockable in source)
      {
        var clone = CloneDockable(dockable, map);
        if (clone != null)
          list.Add(clone);
      }
      return list;
    }

    private static IDockable? MapDockable(IDockable? source, IDictionary<IDockable, IDockable> map)
    {
      if (source != null && map.TryGetValue(source, out var clone))
        return clone;
      return null;
    }

    private static void CopyDockableProperties(IDockable source, IDockable target)
    {
      if (source is not DockableBase sourceBase || target is not DockableBase targetBase)
        return;

      targetBase.Id = sourceBase.Id;
      targetBase.Title = sourceBase.Title;
      targetBase.Proportion = sourceBase.Proportion;
      targetBase.Dock = sourceBase.Dock;
      targetBase.Column = sourceBase.Column;
      targetBase.Row = sourceBase.Row;
      targetBase.ColumnSpan = sourceBase.ColumnSpan;
      targetBase.RowSpan = sourceBase.RowSpan;
      targetBase.IsSharedSizeScope = sourceBase.IsSharedSizeScope;
      targetBase.CollapsedProportion = sourceBase.CollapsedProportion;
      targetBase.IsCollapsable = sourceBase.IsCollapsable;
      targetBase.IsEmpty = sourceBase.IsEmpty;
      targetBase.CanClose = sourceBase.CanClose;
      targetBase.CanPin = sourceBase.CanPin;
      targetBase.KeepPinnedDockableVisible = sourceBase.KeepPinnedDockableVisible;
      targetBase.CanFloat = sourceBase.CanFloat;
      targetBase.CanDrag = sourceBase.CanDrag;
      targetBase.CanDrop = sourceBase.CanDrop;
      targetBase.MinWidth = sourceBase.MinWidth;
      targetBase.MaxWidth = sourceBase.MaxWidth;
      targetBase.MinHeight = sourceBase.MinHeight;
      targetBase.MaxHeight = sourceBase.MaxHeight;
      targetBase.IsModified = sourceBase.IsModified;
      targetBase.DockGroup = sourceBase.DockGroup;
      targetBase.IsVisible = sourceBase.IsVisible;
      targetBase.IsActive = sourceBase.IsActive;
    }

    private void HookUpToolListeners(DockControl dockHost)
    {
      log.Debug("HookUpToolListeners called, ToolPanes count: {Count}", ToolPanes.Count);
      HookUpFactoryListeners(dockHost.Factory);

      foreach (var toolModel in this.ToolPanes)
      {
        log.Debug("Hooking up PropertyChanged for tool: {ContentId}, IsVisible={IsVisible}", toolModel.ContentId, toolModel.IsVisible);
        // Note: We are not unsubscribing here because we don't have the previous handler instance.
        // Assuming InitializeLayout is called once or we accept multiple subscriptions (which is bad).
        // Ideally we should track subscriptions.

        toolModel.PropertyChanged += (s, e) =>
        {
          if (e.PropertyName == nameof(ToolPaneModel.IsVisible))
          {
            var tm = (ToolPaneModel)s;
            log.Debug("ToolModel {ContentId} IsVisible changed to {IsVisible}", tm.ContentId, tm.IsVisible);
            if (tm.IsVisible)
            {
              log.Debug("Calling ActivateTool for {ContentId}", tm.ContentId);
              ActivateTool(dockHost, tm.ContentId);
            }
          }
        };
      }
    }

    private void HookUpFactoryListeners(IFactory? factory)
    {
      if (ReferenceEquals(currentFactory, factory))
        return;

      if (currentFactory != null)
      {
        currentFactory.DockableClosed -= OnDockableClosed;
      }

      currentFactory = factory;

      if (currentFactory != null)
      {
        currentFactory.DockableClosed += OnDockableClosed;
      }
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
      var pane = e?.Dockable?.Id is { } dockableId
          ? this.ToolPanes.FirstOrDefault(p => p.ContentId == dockableId)
          : null;

      if (pane != null)
      {
        pane.IsVisible = false;
        return;
      }

      // Closing documents should remove the corresponding tab page.
      if (e?.Dockable is TabPageModel doc)
      {
        tabPages.Remove(doc);
      }
    }

    private Dictionary<string, ITool> _registeredTools = new();
    private Dictionary<string, IDockable> _registeredDockables = new();

    public void RegisterTool(ITool tool)
    {
      if (tool.Id != null)
      {
        _registeredTools[tool.Id] = tool;
      }
    }

    public void RegisterDockable(IDockable dockable)
    {
      if (dockable.Id != null)
      {
        _registeredDockables[dockable.Id] = dockable;
      }
    }

    /// <summary>
    /// Build a document dock based on the current tab pages and track future changes.
    /// </summary>
    public DocumentDock CreateDocumentDock()
    {
      log.Debug("CreateDocumentDock called");
      EnsureTabPage();

      var docDock = new DocumentDock
      {
        Id = "DocumentDock",
        Title = "Documents",
        VisibleDockables = new ObservableCollection<IDockable>()
      };

      foreach (var tab in tabPages)
      {
        docDock.VisibleDockables.Add(CreateDocument(tab));
      }

      docDock.ActiveDockable = docDock.VisibleDockables.FirstOrDefault();
      documentDock = docDock;
      SyncActiveDocument();
      HookTabPageCollection();

      return docDock;
    }

    /// <summary>
    /// Connects DockWorkspace to an existing DockControl layout (used by MainWindow).
    /// </summary>
    public void AttachToDockHost(DockControl host, IFactory factory, IDocumentDock? docDock)
    {
      dockHost = host;
      this.factory = factory;
      documentDock = docDock;

      HookUpFactoryListeners(factory);
      HookTabPageCollection();

      if (documentDock != null)
      {
        HookDocumentDockListeners(documentDock);
        if (documentDock.VisibleDockables == null || documentDock.VisibleDockables.Count == 0)
        {
          PopulateDocuments();
        }

        SyncActiveDocument();
      }
    }

    private void HookTabPageCollection()
    {
      tabPages.CollectionChanged -= TabPages_CollectionChangedForDock;
      tabPages.CollectionChanged += TabPages_CollectionChangedForDock;

      this.PropertyChanged -= DockWorkspace_PropertyChanged;
      this.PropertyChanged += DockWorkspace_PropertyChanged;
    }

    private void DockWorkspace_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == nameof(ActiveTabPage))
      {
        SyncActiveDocument();
      }
    }

    private void TabPages_CollectionChangedForDock(object? sender, NotifyCollectionChangedEventArgs e)
    {
      if (documentDock?.VisibleDockables == null)
        return;

      if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
      {
        int insertIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : documentDock.VisibleDockables.Count;
        foreach (TabPageModel tab in e.NewItems)
        {
          var doc = CreateDocument(tab);
          documentDock.VisibleDockables.Insert(insertIndex++, doc);
        }
      }
      else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
      {
        foreach (TabPageModel tab in e.OldItems)
        {
          RemoveDocument(tab);
        }
      }
      else if (e.Action == NotifyCollectionChangedAction.Reset)
      {
        PopulateDocuments();
      }

      SyncActiveDocument();
    }

    private void PopulateDocuments()
    {
      if (documentDock?.VisibleDockables == null)
        return;

      documentDock.VisibleDockables.Clear();
      EnsureTabPage();

      foreach (var tab in tabPages)
      {
        if (documentDock.VisibleDockables.Contains(tab))
          continue;
        documentDock.VisibleDockables.Add(CreateDocument(tab));
      }
    }

    private Document CreateDocument(TabPageModel tabPage)
    {
      // Preserve arbitrary content: if tabPage.Content is a Control (or other UI element)
      // ensure its DataContext is set so it can bind to the TabPageModel.
      object? content = tabPage.Content;
      if (content is DecompilerTextView dtv)
      {
        if (dtv.DataContext == null)
          dtv.DataContext = tabPage;
      }
      else if (content is Control ctrl)
      {
        if (ctrl.DataContext == null)
          ctrl.DataContext = tabPage;
      }
      else if (content == null)
      {
        // Fallback to providing a DecompilerTextView when no content is set.
        var textView = new DecompilerTextView(tabPage.ExportProvider);
        textView.DataContext = tabPage;
        content = textView;
        tabPage.Content = content;
      }

      tabPage.Id = tabPage.ContentId ?? $"Tab{TabPages.Count + 1}";
      tabPage.CanFloat = false;
      tabPage.CanPin = false;
      return tabPage;
    }

    private void RemoveDocument(TabPageModel tabPage)
    {
      documentDock?.VisibleDockables?.Remove(tabPage);
    }

    private void HookDocumentDockListeners(IDocumentDock docDock)
    {
      if (docDock is INotifyPropertyChanged inpc)
      {
        inpc.PropertyChanged -= DocumentDock_PropertyChanged;
        inpc.PropertyChanged += DocumentDock_PropertyChanged;
      }
    }

    private void DocumentDock_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == nameof(IDock.ActiveDockable) && sender is IDock dock)
      {
        if (dock.ActiveDockable is TabPageModel doc)
        {
          if (!ReferenceEquals(ActiveTabPage, doc))
          {
            ActiveTabPage = doc;
          }
        }
      }
    }

    private void SyncActiveDocument()
    {
      if (documentDock == null || factory == null)
        return;

      var targetTab = ActiveTabPage ?? tabPages.FirstOrDefault();
      if (targetTab == null)
        return;

      documentDock.ActiveDockable = targetTab;
      factory.SetActiveDockable(targetTab);
      factory.SetFocusedDockable(documentDock, targetTab);
    }

    private void EnsureTabPage()
    {
      if (!tabPages.Any())
      {
        AddTabPage();
      }
    }

    private void ActivateTool(DockControl dockHost, string contentId)
    {
      try
      {
        log.Debug("ActivateTool called for contentId: {ContentId}", contentId);
        var factory = dockHost.Factory;
        var layout = dockHost.Layout;
        if (factory == null || layout == null)
        {
          log.Debug("ActivateTool: factory or layout is null");
          return;
        }

        var tool = FindToolById(layout, contentId);
        if (tool != null)
        {
          log.Debug("ActivateTool: Found tool {Id}, Owner={Owner}", tool.Id, tool.Owner?.GetType().Name);
          // Make the containing dock visible
          if (tool.Owner is IDock parentDock)
          {
            log.Debug("ActivateTool: Parent dock ID={Id}", parentDock.Id);
            if (parentDock is DockableBase dockableBase)
            {
              log.Debug("ActivateTool: Setting parent dock IsVisible=true (was {WasVisible})", dockableBase.IsVisible);
              dockableBase.IsVisible = true;
              log.Debug("ActivateTool: Parent dock IsVisible is now {IsVisible}", dockableBase.IsVisible);
            }
          }

          factory.SetActiveDockable(tool);
          factory.SetFocusedDockable(layout, tool);
          log.Debug("ActivateTool: Activated and focused tool {ContentId}", contentId);
          return;
        }
        else
        {
          log.Debug("ActivateTool: Tool {ContentId} not found in layout, checking registered tools", contentId);
        }
        {
          // Try to find in registered tools
          if (_registeredTools.TryGetValue(contentId, out var registeredTool))
          {
            log.Debug("ActivateTool: Found registered tool {ContentId}", contentId);
            // Determine target dock
            string targetDockId = contentId == SearchPaneModel.PaneContentId ? "SearchDock" : "LeftDock";
            var targetDock = FindDockById(layout, targetDockId);
            log.Debug("ActivateTool: Looking for targetDock {TargetDockId}, found={Found}", targetDockId, targetDock != null);

            // If target dock not found in layout, try to find in registered dockables and insert it
            if (targetDock == null && targetDockId == "SearchDock")
            {
              log.Debug("ActivateTool: SearchDock not in layout, will create and insert it");
              var docDock = FindDockById(layout, "DocumentDock");
              log.Debug("ActivateTool: DocumentDock found={Found}, Owner={Owner}, OwnerId={OwnerId}", docDock != null, docDock?.Owner?.GetType().Name, docDock?.Owner?.Id);
              if (docDock != null && docDock.Owner is IDock docOwner && docOwner.VisibleDockables != null)
              {
                log.Debug("ActivateTool: DocOwner is {Owner}, Orientation={Orientation}", docOwner.GetType().Name, (docOwner as IProportionalDock)?.Orientation);
                // If the owner is horizontal (e.g. MainLayout), we need to wrap DocumentDock in a vertical dock
                if (docOwner is IProportionalDock propDock && propDock.Orientation == Dock.Model.Core.Orientation.Horizontal)
                {
                  var newVerticalDock = new ProportionalDock
                  {
                    Id = "RightDock",
                    Orientation = Dock.Model.Core.Orientation.Vertical,
                    Proportion = double.IsNaN(docDock.Proportion) ? 1.0 : docDock.Proportion,
                    VisibleDockables = new ObservableCollection<IDockable>()
                  };

                  int index = docOwner.VisibleDockables.IndexOf(docDock);
                  if (index >= 0)
                  {
                    docOwner.VisibleDockables.RemoveAt(index);
                    docOwner.VisibleDockables.Insert(index, newVerticalDock);
                    newVerticalDock.Owner = docOwner;
                    newVerticalDock.Factory = factory;

                    if (_registeredDockables.TryGetValue("SearchDock", out var searchDock) && searchDock is IToolDock sd)
                    {
                      sd.Owner = newVerticalDock;
                      sd.Factory = factory;
                      newVerticalDock.VisibleDockables.Add(sd);
                      targetDock = sd;

                      if (_registeredDockables.TryGetValue("SearchSplitter", out var splitter))
                      {
                        splitter.Owner = newVerticalDock;
                        splitter.Factory = factory;
                        newVerticalDock.VisibleDockables.Add(splitter);
                      }
                    }

                    docDock.Owner = newVerticalDock;
                    newVerticalDock.VisibleDockables.Add(docDock);
                    newVerticalDock.ActiveDockable = docDock;
                  }
                }
                else
                {
                  log.Debug("ActivateTool: RightDock is vertical, will insert SearchDock at top");
                  // Owner is likely vertical (or we assume it is safe to insert), just insert at top
                  if (_registeredDockables.TryGetValue("SearchDock", out var searchDock) && searchDock is IToolDock sd)
                  {
                    log.Debug("ActivateTool: Found SearchDock in registered dockables");
                    if (!docOwner.VisibleDockables.Contains(sd))
                    {
                      log.Debug("ActivateTool: Inserting SearchDock into RightDock");
                      sd.Owner = docOwner;
                      sd.Factory = factory;
                      docOwner.VisibleDockables.Insert(0, sd);
                      targetDock = sd;

                      if (_registeredDockables.TryGetValue("SearchSplitter", out var splitter))
                      {
                        if (!docOwner.VisibleDockables.Contains(splitter))
                        {
                          splitter.Owner = docOwner;
                          splitter.Factory = factory;
                          docOwner.VisibleDockables.Insert(1, splitter);
                          log.Debug("ActivateTool: Inserted SearchSplitter");
                        }
                      }
                    }
                    else
                    {
                        log.Debug("ActivateTool: SearchDock already in RightDock");
                      targetDock = sd;
                    }
                  }
                  else
                  {
                    log.Debug("ActivateTool: SearchDock NOT found in registered dockables, creating new one");
                    // Create SearchDock if not registered
                    var newSearchDock = new ToolDock
                    {
                      Id = "SearchDock",
                      Title = "Search",
                      Alignment = Alignment.Top,
                      VisibleDockables = new ObservableCollection<IDockable>(),
                      CanCloseLastDockable = true,
                      Proportion = 0.33,
                      Owner = docOwner,
                      Factory = factory
                    };

                    docOwner.VisibleDockables.Insert(0, newSearchDock);
                    RegisterDockable(newSearchDock);
                    targetDock = newSearchDock;

                    // Also create and insert splitter
                    var newSplitter = new ProportionalDockSplitter
                    {
                      Id = "SearchSplitter",
                      CanResize = true,
                      Owner = docOwner,
                      Factory = factory
                    };
                    docOwner.VisibleDockables.Insert(1, newSplitter);
                    RegisterDockable(newSplitter);
                    log.Debug("ActivateTool: Created and inserted new SearchDock and splitter");
                  }
                }
              }
            }

            if (targetDock is ToolDock toolDock)
            {
              factory.AddDockable(toolDock, registeredTool);
              toolDock.IsVisible = true;
              factory.SetActiveDockable(registeredTool);
              factory.SetFocusedDockable(layout, registeredTool);
              return;
            }
          }
        }
      }
      catch (Exception ex)
      {
      }
    }

    private static ITool? FindToolById(IDockable root, string id)
    {
      if (root is ITool tool && tool.Id == id)
        return tool;

      if (root is IDock dock && dock.VisibleDockables != null)
      {
        foreach (var dockable in dock.VisibleDockables)
        {
          var found = FindToolById(dockable, id);
          if (found != null)
            return found;
        }
      }
      return null;
    }

    private static IDockable? FindDockableById(IDockable root, string id)
    {
      if (root.Id == id)
        return root;

      if (root is RootDock rootDock)
      {
        var hidden = FindDockableById(rootDock.HiddenDockables, id);
        if (hidden != null)
          return hidden;
        hidden = FindDockableById(rootDock.LeftPinnedDockables, id);
        if (hidden != null)
          return hidden;
        hidden = FindDockableById(rootDock.RightPinnedDockables, id);
        if (hidden != null)
          return hidden;
        hidden = FindDockableById(rootDock.TopPinnedDockables, id);
        if (hidden != null)
          return hidden;
        hidden = FindDockableById(rootDock.BottomPinnedDockables, id);
        if (hidden != null)
          return hidden;
      }

      if (root is IDock dock && dock.VisibleDockables != null)
      {
        foreach (var dockable in dock.VisibleDockables)
        {
          var found = FindDockableById(dockable, id);
          if (found != null)
            return found;
        }
      }
      return null;
    }

    private static IDockable? FindDockableById(IList<IDockable>? dockables, string id)
    {
      if (dockables == null)
        return null;

      foreach (var dockable in dockables)
      {
        var found = FindDockableById(dockable, id);
        if (found != null)
          return found;
      }
      return null;
    }

    private static IDock? FindDockById(IDockable root, string id)
    {
      if (root is IDock dock)
      {
        if (dock.Id == id)
          return dock;

        if (root is RootDock rootDock)
        {
          var hidden = FindDockableById(rootDock.HiddenDockables, id) as IDock;
          if (hidden != null)
            return hidden;
          hidden = FindDockableById(rootDock.LeftPinnedDockables, id) as IDock;
          if (hidden != null)
            return hidden;
          hidden = FindDockableById(rootDock.RightPinnedDockables, id) as IDock;
          if (hidden != null)
            return hidden;
          hidden = FindDockableById(rootDock.TopPinnedDockables, id) as IDock;
          if (hidden != null)
            return hidden;
          hidden = FindDockableById(rootDock.BottomPinnedDockables, id) as IDock;
          if (hidden != null)
            return hidden;
        }

        if (dock.VisibleDockables != null)
        {
          foreach (var dockable in dock.VisibleDockables)
          {
            var found = FindDockById(dockable, id);
            if (found != null)
              return found;
          }
        }
      }
      return null;
    }

    internal void ResetLayout()
    {
      foreach (var pane in ToolPanes)
      {
        pane.IsVisible = false;
      }
      CloseAllTabs();
      ResetLayoutPlatformSpecific();
      InitializeLayout();

      App.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => MessageBus.Send(this, new ResetLayoutEventArgs()));
    }

    void ResetLayoutPlatformSpecific()
    {
      try
      {
        log.Debug("ResetLayoutPlatformSpecific called");

        // Clear the current dockHost layout so InitializeLayout will create a new one
        if (dockHost != null)
        {
          dockHost.Layout = null;
          dockHost.Factory = null;
          log.Debug("Cleared dockHost Layout and Factory");
        }

        // Clear saved layout in Avalonia settings
        var roverSettings = GetRoverSettings();
        roverSettings.DockLayout = null;

        log.Debug("Cleared DockLayout setting");
      }
      catch (Exception ex)
      {
        log.Error(ex, "ResetLayoutPlatformSpecific error");
      }
    }
  }
}
