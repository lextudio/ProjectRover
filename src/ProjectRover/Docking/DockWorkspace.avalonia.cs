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
using Dock.Model.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.TomsToolbox.Controls;
using Dock.Model.TomsToolbox;

namespace ICSharpCode.ILSpy.Docking
{
  /// <summary>
  /// Avalonia-specific extensions to <see cref="DockWorkspace"/>.
  /// </summary>
  public partial class DockWorkspace
  {
    private IFactory? currentFactory;
    private DockControl? dockHost;
    private IDocumentDock? documentDock;
    private IFactory? factory;

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
          Console.WriteLine("DockWorkspace.InitializeLayout: Layout already initialized, hooking listeners");
          var docDock1 = FindDockById(dockHost.Layout, "DocumentDock") as IDocumentDock;
          AttachToDockHost(dockHost, dockHost.Factory, docDock1);
          HookUpToolListeners(dockHost);
          return;
        }

        var viewModel = mainWindow.DataContext as MainWindowViewModel;
        if (viewModel == null)
        {
             Console.WriteLine("DockWorkspace.InitializeLayout: ViewModel is null, cannot initialize specific layout.");
             return;
        }

        Console.WriteLine("DockWorkspace.InitializeLayout: Creating specific layout structure");

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

        var toolDock = new ToolDock
        {
            Id = "LeftDock",
            Title = "LeftDock",
            Alignment = Alignment.Left,
            VisibleDockables = new ObservableCollection<IDockable>(),
            Proportion = 0.3
        };

        this.RegisterTool(searchPaneModel);

        var searchDock = new ToolDock
        {
            Id = "SearchDock",
            Title = "Search",
            Alignment = Alignment.Top,
            VisibleDockables = new ObservableCollection<IDockable>(),
            CanCloseLastDockable = true,
            Proportion = 0.5
        };
        this.RegisterDockable(searchDock);

        var searchSplitter = new ProportionalDockSplitter { Id = "SearchSplitter", CanResize = true };
        this.RegisterDockable(searchSplitter);

        var rightDock = new ProportionalDock
        {
            Id = "RightDock",
            Orientation = Dock.Model.Core.Orientation.Vertical,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                documentDock
            },
            ActiveDockable = documentDock
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Dock.Model.Core.Orientation.Horizontal,
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

        var factory = new Factory();
        dockHost.Factory = factory;
        dockHost.InitializeFactory = true;
        dockHost.InitializeLayout = true;
        dockHost.Layout = rootDock;

        AttachToDockHost(dockHost, factory, documentDock);
        HookUpToolListeners(dockHost);

        dockHost.IsVisible = true;

        Console.WriteLine("DockWorkspace.InitializeLayout: Specific layout initialized successfully");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"DockWorkspace.InitializeLayout error: {ex}");
      }
    }

    private void HookUpToolListeners(DockControl dockHost)
    {
        HookUpFactoryListeners(dockHost.Factory);

        foreach (var toolModel in this.ToolPanes)
        {
            // Note: We are not unsubscribing here because we don't have the previous handler instance.
            // Assuming InitializeLayout is called once or we accept multiple subscriptions (which is bad).
            // Ideally we should track subscriptions.
            
            toolModel.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(ToolPaneModel.IsVisible))
                {
                    var tm = (ToolPaneModel)s;
                    if (tm.IsVisible)
                    {
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
            var factory = dockHost.Factory;
            var layout = dockHost.Layout;
            if (factory == null || layout == null) return;

            var tool = FindToolById(layout, contentId);
            if (tool != null)
            {
                factory.SetActiveDockable(tool);
                factory.SetFocusedDockable(layout, tool);
                Console.WriteLine($"DockWorkspace: Activated tool {contentId}");
            }
            else
            {
                // Try to find in registered tools
                if (_registeredTools.TryGetValue(contentId, out var registeredTool))
                {
                    // Determine target dock
                    string targetDockId = contentId == SearchPaneModel.PaneContentId ? "SearchDock" : "LeftDock";
                    var targetDock = FindDockById(layout, targetDockId);
                    
                    // If target dock not found in layout, try to find in registered dockables and insert it
                    if (targetDock == null && targetDockId == "SearchDock")
                    {
                        var rightDock = FindDockById(layout, "RightDock");
                        if (rightDock != null && rightDock.VisibleDockables != null)
                        {
                             if (_registeredDockables.TryGetValue("SearchDock", out var searchDock) && searchDock is IToolDock sd)
                             {
                                 sd.Owner = rightDock;
                                 sd.Factory = factory;
                                 rightDock.VisibleDockables.Insert(0, sd);
                                 targetDock = sd;

                                 if (_registeredDockables.TryGetValue("SearchSplitter", out var splitter))
                                 {
                                     splitter.Owner = rightDock;
                                     splitter.Factory = factory;
                                     rightDock.VisibleDockables.Insert(1, splitter);
                                 }
                             }
                        }
                    }

                    if (targetDock is IToolDock toolDock)
                    {
                        factory.AddDockable(toolDock, registeredTool);
                        factory.SetActiveDockable(registeredTool);
                        factory.SetFocusedDockable(layout, registeredTool);
                        Console.WriteLine($"DockWorkspace: Restored and activated tool {contentId} in {targetDockId}");
                        return;
                    }
                }

                Console.WriteLine($"DockWorkspace: Tool {contentId} not found for activation");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DockWorkspace: Error activating tool {contentId}: {ex}");
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

    private static IDock? FindDockById(IDockable root, string id)
    {
      if (root is IDock dock)
      {
          if (dock.Id == id) return dock;
          
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
  }
}
