using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using ICSharpCode.ILSpy.TextViewControl;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpy.Search;
using Dock.Model.Controls;

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
    private readonly Dictionary<TabPageModel, Dock.Model.Avalonia.Controls.Document> documents = new();

    public void InitializeLayout()
    {
      try
      {
        var app = Avalonia.Application.Current;
        var mainWindow = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                         ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
        if (mainWindow == null)
          return;

        var dockHost = mainWindow.FindControl<DockControl>("DockHost");
        if (dockHost == null)
          return;

        // If layout is already initialized (by MainWindow.ConfigureDockLayout), reuse it but hook listeners
        if (dockHost.Layout != null && dockHost.Factory != null)
        {
          Console.WriteLine("DockWorkspace.InitializeLayout: Layout already initialized, hooking listeners");
          var docDock1 = FindDockById(dockHost.Layout, "DocumentDock") as IDocumentDock;
          AttachToDockHost(dockHost, dockHost.Factory, docDock1);
          HookUpToolListeners(dockHost);
          return;
        }

        // Fallback: create a basic layout structure if not already done
        Console.WriteLine("DockWorkspace.InitializeLayout: Creating layout structure");
        var factory = new Factory();
        var docDock = CreateDocumentDock();

        // Create tool dock (for side panels like Search, etc.)
        var toolDock = new ToolDock
        {
          Id = "ToolDock",
          Title = "Tools",
          Alignment = Alignment.Right,
          VisibleDockables = new ObservableCollection<IDockable>(),
          ActiveDockable = null
        };

        toolDock.Proportion = 0.25;
        docDock.Proportion = 0.75;

        // Create main horizontal layout
        var mainLayout = new ProportionalDock
        {
          Id = "MainLayout",
          Title = "MainLayout",
          Orientation = Dock.Model.Core.Orientation.Horizontal,
          VisibleDockables = new ObservableCollection<IDockable>
          {
            docDock,
            new ProportionalDockSplitter { CanResize = true },
            toolDock
          },
          ActiveDockable = docDock
        };

        // Create root dock
        var rootDock = new RootDock
        {
          Id = "Root",
          Title = "Root",
          VisibleDockables = new ObservableCollection<IDockable> { mainLayout },
          ActiveDockable = mainLayout
        };

        // Populate tools
        foreach (var toolModel in this.ToolPanes)
        {
            var tool = new Tool 
            { 
                Id = toolModel.ContentId, 
                Title = toolModel.Title,
                Content = toolModel 
            };
            toolDock.VisibleDockables.Add(tool);
        }

        // Assign factory and layout to dock host
        dockHost.Factory = factory;
        dockHost.Layout = rootDock;
        dockHost.InitializeFactory = true;
        dockHost.InitializeLayout = true;

        AttachToDockHost(dockHost, factory, docDock);
        HookUpToolListeners(dockHost);

        Console.WriteLine("DockWorkspace.InitializeLayout: Dock layout initialized successfully");
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
        if (e?.Dockable is Dock.Model.Avalonia.Controls.Document doc)
        {
            var tab = documents.FirstOrDefault(kv => kv.Value == doc).Key;
            if (tab != null)
            {
                tabPages.Remove(tab);
            }
        }
    }

    private Dictionary<string, Dock.Model.Controls.ITool> _registeredTools = new();
    private Dictionary<string, Dock.Model.Core.IDockable> _registeredDockables = new();

    public void RegisterTool(Dock.Model.Controls.ITool tool)
    {
        if (tool.Id != null)
        {
            _registeredTools[tool.Id] = tool;
        }
    }

    public void RegisterDockable(Dock.Model.Core.IDockable dockable)
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
            else
            {
                RebindExistingDocuments(documentDock);
            }
            SyncActiveDocument();
        }
    }

    private void HookTabPageCollection()
    {
        tabPages.CollectionChanged -= TabPages_CollectionChangedForDock;
        tabPages.CollectionChanged += TabPages_CollectionChangedForDock;

        // Keep document titles/content synced
        foreach (var tab in tabPages)
        {
            tab.PropertyChanged -= TabPage_PropertyChanged;
            tab.PropertyChanged += TabPage_PropertyChanged;
        }

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

        foreach (var kvp in documents.Keys.ToList())
        {
            kvp.PropertyChanged -= TabPage_PropertyChanged;
        }
        documents.Clear();
        documentDock.VisibleDockables.Clear();
        EnsureTabPage();

        foreach (var tab in tabPages)
        {
            documentDock.VisibleDockables.Add(CreateDocument(tab));
        }
    }

    private Dock.Model.Avalonia.Controls.Document CreateDocument(TabPageModel tabPage)
    {
        // Ensure there is a visible text view and that it knows its tab page
        if (tabPage.Content is not DecompilerTextView textView)
        {
            textView = new DecompilerTextView(tabPage.ExportProvider);
            tabPage.Content = textView;
        }
        if (textView.DataContext == null)
        {
            textView.DataContext = tabPage;
        }

        var doc = new Dock.Model.Avalonia.Controls.Document
        {
            Id = tabPage.ContentId ?? $"Tab{documents.Count + 1}",
            Title = tabPage.Title,
            Content = tabPage.Content,
            Context = tabPage,
            CanClose = tabPage.IsCloseable,
            CanFloat = false,
            CanPin = false
        };

        documents[tabPage] = doc;
        tabPage.PropertyChanged -= TabPage_PropertyChanged;
        tabPage.PropertyChanged += TabPage_PropertyChanged;

        return doc;
    }

    private void RemoveDocument(TabPageModel tabPage)
    {
        if (documents.TryGetValue(tabPage, out var doc))
        {
            tabPage.PropertyChanged -= TabPage_PropertyChanged;
            documents.Remove(tabPage);
            documentDock?.VisibleDockables?.Remove(doc);
        }
    }

    private void RebindExistingDocuments(IDocumentDock docDock)
    {
        foreach (var kvp in documents.Keys.ToList())
        {
            kvp.PropertyChanged -= TabPage_PropertyChanged;
        }
        documents.Clear();

        if (docDock.VisibleDockables == null)
            return;

        foreach (var dockable in docDock.VisibleDockables.OfType<Dock.Model.Avalonia.Controls.Document>())
        {
            if (dockable.Context is TabPageModel tab)
            {
                documents[tab] = dockable;
                tab.PropertyChanged -= TabPage_PropertyChanged;
                tab.PropertyChanged += TabPage_PropertyChanged;
            }
        }
    }

    private void TabPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TabPageModel tab || !documents.TryGetValue(tab, out var doc))
            return;

        switch (e.PropertyName)
        {
            case nameof(TabPageModel.Title):
                doc.Title = tab.Title;
                break;
            case nameof(TabPageModel.Content):
                doc.Content = tab.Content;
                if (doc.Content is DecompilerTextView dtv && dtv.DataContext == null)
                    dtv.DataContext = tab;
                break;
            case nameof(TabPageModel.IsCloseable):
                doc.CanClose = tab.IsCloseable;
                break;
        }
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
            if (dock.ActiveDockable is Dock.Model.Avalonia.Controls.Document doc)
            {
                var tab = documents.FirstOrDefault(kv => kv.Value == doc).Key;
                if (tab != null && !ReferenceEquals(ActiveTabPage, tab))
                {
                    ActiveTabPage = tab;
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

        if (!documents.TryGetValue(targetTab, out var doc))
            return;

        documentDock.ActiveDockable = doc;
        factory.SetActiveDockable(doc);
        factory.SetFocusedDockable(documentDock, doc);
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
                             if (_registeredDockables.TryGetValue("SearchDock", out var searchDock) && searchDock is Dock.Model.Controls.IToolDock sd)
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

                    if (targetDock is Dock.Model.Controls.IToolDock toolDock)
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

    private static Dock.Model.Controls.ITool? FindToolById(Dock.Model.Core.IDockable root, string id)
    {
      if (root is Dock.Model.Controls.ITool tool && tool.Id == id)
        return tool;

      if (root is Dock.Model.Core.IDock dock && dock.VisibleDockables != null)
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

    private static Dock.Model.Core.IDock? FindDockById(Dock.Model.Core.IDockable root, string id)
    {
      if (root is Dock.Model.Core.IDock dock)
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
