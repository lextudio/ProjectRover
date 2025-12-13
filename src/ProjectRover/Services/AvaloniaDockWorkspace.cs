using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Avalonia;

namespace ProjectRover.Services
{
    /// <summary>
    /// Minimal scaffold implementation of ICSharpCode.ILSpy.IDockWorkspace for Avalonia.
    /// This is intentionally small and returns placeholder objects; extend as needed.
    /// </summary>
    public class AvaloniaDockWorkspace : ICSharpCode.ILSpy.Docking.IDockWorkspace
    {
        public IReadOnlyList<object> TabPages { get; private set; } = Array.Empty<object>();

        public IReadOnlyList<object> ToolPanes { get; private set; } = Array.Empty<object>();

        public object? ActiveTabPage { get; set; }

        public object AddTabPage(object? tabPage = null)
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return tabPage ?? new object();

                // find DockControl named DockHost
                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                if (dockHost?.Factory == null)
                {
                    ActiveTabPage = tabPage ?? new object();
                    return ActiveTabPage!;
                }

                var factory = dockHost.Factory!;

                // create a document if none provided. Use concrete Document type
                // to avoid depending on different factory API surface versions.
                IDocument document;
                if (tabPage is IDocument d)
                {
                    document = d;
                }
                else
                {
                    document = new Dock.Model.Avalonia.Controls.Document();
                }

                // insert into the root layout if possible
                var layout = dockHost.Layout;
                if (layout is Dock.Model.Avalonia.Controls.ProportionalDock proportional && proportional.VisibleDockables != null)
                {
                    // insert at the last position of the documents dock
                    factory.InsertDockable(proportional, (Dock.Model.Core.IDockable)document, proportional.VisibleDockables.Count);
                }

                // set active
                factory.SetActiveDockable(document);

                // If caller expects an ILSpy TabPageModel-like object, wrap the document in our shim
                object returnObj = document;
                try
                {
                    if (!(tabPage is Dock.Model.Controls.IDocument))
                    {
                        // Create a TabPageModel shim and set its Content to a DecompilerPane hosted in the document
                        var tabType = Type.GetType("ICSharpCode.ILSpy.ViewModels.TabPageModel, ProjectRover")
                                      ?? Type.GetType("ICSharpCode.ILSpy.ViewModels.TabPageModel");
                        object? shim = null;
                        if (tabType != null)
                        {
                            try
                            {
                                shim = Activator.CreateInstance(tabType);
                            }
                            catch { shim = null; }
                        }

                        if (shim != null)
                        {
                            // Attempt to set Content on shim to the Document.Content (if any) or a new DecompilerPane
                            try
                            {
                                var mainWindowType = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
                                    ? (Avalonia.Application.Current.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.GetType()
                                    : null;

                                // Create DecompilerPane from ProjectRover.Views if available
                                var decompType = Type.GetType("ProjectRover.Views.DecompilerPane, ProjectRover") ?? Type.GetType("ProjectRover.Views.DecompilerPane");
                                object? decomp = null;
                                if (decompType != null)
                                {
                                    try { decomp = Activator.CreateInstance(decompType); } catch { decomp = null; }
                                }

                                var contentProp = shim.GetType().GetProperty("Content");
                                if (contentProp != null)
                                {
                                    if (decomp != null)
                                        contentProp.SetValue(shim, decomp);
                                    else
                                        contentProp.SetValue(shim, document);
                                }

                                // set title if available
                                var titleProp = shim.GetType().GetProperty("Title");
                                titleProp?.SetValue(shim, document.Title);

                                returnObj = shim;
                            }
                            catch
                            {
                                // ignore shim failures
                            }
                        }
                    }
                }
                catch
                {
                }

                ActiveTabPage = returnObj;
                // refresh TabPages/ToolPanes from layout
                UpdateCollections(dockHost);

                return returnObj;
            }
            catch
            {
                ActiveTabPage = tabPage ?? new object();
                return ActiveTabPage!;
            }
        }

        public bool ShowToolPane(string contentId)
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return false;

                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                var factory = dockHost?.Factory;
                if (factory == null)
                    return false;

                // find a tool pane with matching Id or Title
                var layout = dockHost.Layout;
                if (layout == null)
                    return false;

                // Search recursively for tool by Id or Title
                Dock.Model.Core.IDockable? found = DockSearchHelpers.FindByContentId(layout, contentId);
                if (found is Dock.Model.Controls.ITool tool)
                {
                    factory.SetActiveDockable(tool);
                    factory.SetFocusedDockable(layout, tool);
                    UpdateCollections(dockHost);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Remove(object model)
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return;

                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                var factory = dockHost?.Factory;
                if (factory == null)
                {
                    if (ReferenceEquals(model, ActiveTabPage))
                        ActiveTabPage = null;
                    return;
                }

                if (model is Dock.Model.Core.IDockable dockable)
                {
                    factory.RemoveDockable(dockable, true);
                }
                else if (ReferenceEquals(model, ActiveTabPage))
                {
                    ActiveTabPage = null;
                }

                UpdateCollections(dockHost);
            }
            catch
            {
            }
        }

        public void InitializeLayout()
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return;

                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                if (dockHost == null)
                    return;

                // Ensure factory and layout are initialized
                if (dockHost.Factory != null && dockHost.Layout != null)
                {
                    // update internal cached lists
                    UpdateCollections(dockHost);
                }
            }
            catch
            {
            }
        }

        public void ResetLayout()
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return;

                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                var factory = dockHost?.Factory;
                if (factory == null)
                    return;

                // Attempt to reset by reinitializing factory/layout
                dockHost.InitializeFactory = true;
                dockHost.InitializeLayout = true;
                UpdateCollections(dockHost);
            }
            catch
            {
            }
        }

        public void CloseAllTabs()
        {
            try
            {
                var app = Avalonia.Application.Current;
                var main = (app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(null);
                if (main == null)
                    return;

                var dockHost = main.FindControl<Dock.Avalonia.Controls.DockControl>("DockHost");
                var factory = dockHost?.Factory;
                if (factory == null)
                {
                    ActiveTabPage = null;
                    return;
                }

                // Close documents by removing them from layout
                var layout = dockHost.Layout;
                if (layout is Dock.Model.Avalonia.Controls.ProportionalDock proportional && proportional.VisibleDockables != null)
                {
                    var docs = proportional.VisibleDockables.OfType<Dock.Model.Controls.IDocument>().ToArray();
                    foreach (var d in docs)
                    {
                        factory.RemoveDockable(d, true);
                    }
                }

                ActiveTabPage = null;
                UpdateCollections(dockHost);
            }
            catch
            {
            }
        }

        public Task<T> RunWithCancellation<T>(Func<CancellationToken, Task<T>> taskCreation)
        {
            // Directly run the task without UI wrapping for now.
            return taskCreation(CancellationToken.None);
        }

        public void ShowText(object textOutput)
        {
            Console.WriteLine("showtext called");
            // Try to find the main window and set its Document to display the provided text.
            try
            {
                var app = Avalonia.Application.Current;
                if (app == null)
                    return;

                var main = (app.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
                           ?? Avalonia.Controls.TopLevel.GetTopLevel(Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime ? null : null);
                if (main == null)
                {
                    // fallback: try to find any open Window via Application.Current?.ApplicationLifetime (non-classic lifetimes not supported here)
                    main = (app.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                }
                if (main == null)
                    return;

                if (main.DataContext is ProjectRover.ViewModels.MainWindowViewModel mwvm)
                {
                    // If the textOutput is ILSpy's AvalonEditTextOutput, try to extract its prepared TextDocument
                    try
                    {
                        Console.WriteLine(textOutput?.GetType().FullName);
                        // Use type name to avoid hard compile dependency on AvalonEdit's TextDocument type
                        if (textOutput != null && textOutput.GetType().FullName == "ICSharpCode.ILSpy.TextView.AvalonEditTextOutput")
                        {
                            // Prepare and get the original AvalonEdit TextDocument, then extract text
                            var prepareMethod = textOutput.GetType().GetMethod("PrepareDocument");
                            var getDocMethod = textOutput.GetType().GetMethod("GetDocument");
                            if (prepareMethod != null && getDocMethod != null)
                            {
                                // Prepare on calling thread (background writer may have already prepared it)
                                try { prepareMethod.Invoke(textOutput, null); } catch { }
                                object? avalonDoc = null;
                                try { avalonDoc = getDocMethod.Invoke(textOutput, null); } catch { }
                                if (avalonDoc != null)
                                {
                                    // Get text property via reflection
                                    var textProp = avalonDoc.GetType().GetProperty("Text");
                                    var text = textProp?.GetValue(avalonDoc) as string;
                                    if (text != null)
                                    {
                                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                        {
                                            try
                                            {
                                                mwvm.Document = new AvaloniaEdit.Document.TextDocument(text);

                                                // Try to copy foldings and references from the original AvalonEdit output.
                                                try
                                                {
                                                    // Foldings: reflectively read 'Foldings' field (List<NewFolding>)
                                                    var foldingsField = textOutput.GetType().GetField("Foldings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                                    if (foldingsField != null)
                                                    {
                                                        var foldingsObj = foldingsField.GetValue(textOutput) as System.Collections.IEnumerable;
                                                        if (foldingsObj != null)
                                                        {
                                                            var newFoldings = new List<AvaloniaEdit.Folding.NewFolding>();
                                                            foreach (var f in foldingsObj)
                                                            {
                                                                try
                                                                {
                                                                    var ftype = f.GetType();
                                                                    var startProp = ftype.GetProperty("StartOffset") ?? ftype.GetProperty("Start");
                                                                    var endProp = ftype.GetProperty("EndOffset") ?? ftype.GetProperty("End");
                                                                    var nameProp = ftype.GetProperty("Name");
                                                                    var defaultClosedProp = ftype.GetProperty("DefaultClosed");

                                                                    var start = startProp != null ? (int)startProp.GetValue(f)! : 0;
                                                                    var end = endProp != null ? (int)endProp.GetValue(f)! : 0;
                                                                    var name = nameProp != null ? nameProp.GetValue(f) as string : null;
                                                                    var defaultClosed = defaultClosedProp != null ? (bool)defaultClosedProp.GetValue(f)! : false;

                                                                    if (start < end)
                                                                    {
                                                                        var nf = new AvaloniaEdit.Folding.NewFolding(start, end)
                                                                        {
                                                                            Name = name,
                                                                            DefaultClosed = defaultClosed
                                                                        };
                                                                        // try set IsDefinition if present
                                                                        var isDefProp = ftype.GetProperty("IsDefinition");
                                                                        if (isDefProp != null)
                                                                        {
                                                                            var isDef = (bool)isDefProp.GetValue(f);
                                                                            var prop = nf.GetType().GetProperty("IsDefinition");
                                                                            prop?.SetValue(nf, isDef);
                                                                        }
                                                                        newFoldings.Add(nf);
                                                                    }
                                                                }
                                                                catch
                                                                {
                                                                    // ignore individual folding conversion failures
                                                                }
                                                            }

                                                            // Install folding manager for the editor and update foldings
                                                            try
                                                            {
                                                                var mainWindowType = main.GetType();
                                                                var editorProp = mainWindowType.GetProperty("TextEditor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                                                var editor = editorProp?.GetValue(main) as AvaloniaEdit.TextEditor;
                                                                if (editor != null)
                                                                {
                                                                    var fm = AvaloniaEdit.Folding.FoldingManager.Install(editor.TextArea);
                                                                    fm.UpdateFoldings(newFoldings, -1);
                                                                }
                                                            }
                                                            catch
                                                            {
                                                            }
                                                        }
                                                    }

                                                    // References: reflectively read 'References' field (TextSegmentCollection<ReferenceSegment>)
                                                    try
                                                    {
                                                        var refsField = textOutput.GetType().GetProperty("References", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                                                                        ?? (System.Reflection.MemberInfo?)textOutput.GetType().GetField("References", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                                        object? refsObj = null;
                                                        if (refsField is System.Reflection.PropertyInfo rpi)
                                                            refsObj = rpi.GetValue(textOutput);
                                                        else if (refsField is System.Reflection.FieldInfo rfi)
                                                            refsObj = rfi.GetValue(textOutput);

                                                        if (refsObj is System.Collections.IEnumerable refsEnumerable)
                                                        {
                                                            try
                                                            {
                                                                // Clear existing static references collection on MainWindow
                                                                var mainWindowType = main.GetType();
                                                                var refsStaticField = mainWindowType.GetField("references", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                                                if (refsStaticField != null)
                                                                {
                                                                    var refsCollection = refsStaticField.GetValue(null) as dynamic;
                                                                    try { refsCollection.Clear(); } catch { }

                                                                    foreach (var r in refsEnumerable)
                                                                    {
                                                                        try
                                                                        {
                                                                            var rtype = r.GetType();
                                                                            var startProp = rtype.GetProperty("StartOffset") ?? rtype.GetProperty("Start");
                                                                            var endProp = rtype.GetProperty("EndOffset") ?? rtype.GetProperty("End");
                                                                            var referenceProp = rtype.GetProperty("Reference") ?? (System.Reflection.PropertyInfo?)null;
                                                                            var referenceField = referenceProp == null ? rtype.GetField("Reference") : null;

                                                                            var start = startProp != null ? (int?)startProp.GetValue(r) ?? 0 : 0;
                                                                            var end = endProp != null ? (int?)endProp.GetValue(r) ?? 0 : 0;
                                                                            object? memberRef = null;
                                                                            if (referenceProp != null)
                                                                            {
                                                                                memberRef = referenceProp.GetValue(r);
                                                                            }
                                                                            else if (referenceField != null)
                                                                            {
                                                                                memberRef = referenceField.GetValue(r);
                                                                            }

                                                                            var refSegType = mainWindowType.Assembly.GetType("ProjectRover.Views.ReferenceTextSegment") ?? mainWindowType.Assembly.GetType("ProjectRover.Views.MainWindow+ReferenceTextSegment") ?? null;
                                                                            object? refSeg = null;
                                                                            if (refSegType != null)
                                                                            {
                                                                                try
                                                                                {
                                                                                    refSeg = Activator.CreateInstance(refSegType);
                                                                                }
                                                                                catch
                                                                                {
                                                                                    refSeg = null;
                                                                                }
                                                                            }
                                                                            if (refSeg != null)
                                                                            {
                                                                                try
                                                                                {
                                                                                    var rsType = refSeg.GetType();
                                                                                    rsType.GetProperty("StartOffset")?.SetValue(refSeg, start);
                                                                                    rsType.GetProperty("EndOffset")?.SetValue(refSeg, end);
                                                                                    var memberProp = rsType.GetProperty("MemberReference");
                                                                                    if (memberProp != null)
                                                                                        memberProp.SetValue(refSeg, memberRef);
                                                                                    var resolvedProp = rsType.GetProperty("Resolved");
                                                                                    if (resolvedProp != null)
                                                                                        resolvedProp.SetValue(refSeg, true);

                                                                                    // add to static MainWindow.references collection
                                                                                    try { refsCollection.Add(refSeg); } catch { }
                                                                                }
                                                                                catch
                                                                                {
                                                                                }
                                                                            }
                                                                        }
                                                                        catch
                                                                        {
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            catch
                                                            {
                                                            }
                                                        }
                                                    }
                                                    catch
                                                    {
                                                    }
                                                }
                                                catch
                                                {
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        });
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore reflection failures and fallback to string conversion
                    }

                    // Fallback: string or ToString()
                    string? textFallback = null;
                    switch (textOutput)
                    {
                        case string s:
                            textFallback = s;
                            break;
                        default:
                            try { textFallback = textOutput?.ToString(); } catch { textFallback = null; }
                            break;
                    }

                    if (textFallback != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                mwvm.Document = new AvaloniaEdit.Document.TextDocument(textFallback);
                            }
                            catch
                            {
                            }
                        });
                    }
                }
            }
            catch
            {
                // best-effort only
            }
        }

        private void UpdateCollections(Dock.Avalonia.Controls.DockControl dockHost)
        {
            try
            {
                var layout = dockHost.Layout;
                if (layout == null)
                    return;

                var docs = new List<object>();
                var tools = new List<object>();

                void Collect(IDock? d)
                {
                    if (d == null)
                        return;

                    if (d is Dock.Model.Controls.IDocumentDock dd && dd.VisibleDockables != null)
                    {
                        foreach (var v in dd.VisibleDockables)
                        {
                            docs.Add(v);
                        }
                    }

                    if (d is Dock.Model.Controls.IToolDock td && td.VisibleDockables != null)
                    {
                        foreach (var v in td.VisibleDockables)
                        {
                            tools.Add(v);
                        }
                    }

                    // traverse children if ProportionalDock etc.
                    if (d.VisibleDockables != null)
                    {
                        foreach (var v in d.VisibleDockables)
                        {
                            if (v is IDock childDock)
                                Collect(childDock);
                        }
                    }
                }

                Collect(layout);

                TabPages = docs.AsReadOnly();
                ToolPanes = tools.AsReadOnly();
            }
            catch
            {
            }
        }

        private static class DockSearchHelpers
        {
            public static Dock.Model.Core.IDockable? FindByContentId(Dock.Model.Core.IDock? dock, string contentId)
            {
                if (dock == null)
                    return null;

                if (dock is Dock.Model.Controls.IToolDock td && td.VisibleDockables != null)
                {
                    foreach (var v in td.VisibleDockables)
                    {
                        if (v is Dock.Model.Controls.ITool tool && (tool.Id == contentId || tool.Title == contentId))
                            return tool;
                    }
                }

                if (dock.VisibleDockables != null)
                {
                    foreach (var v in dock.VisibleDockables)
                    {
                        if (v is Dock.Model.Core.IDock dchild)
                        {
                            var found = FindByContentId(dchild, contentId);
                            if (found != null)
                                return found;
                        }
                        else if (v is Dock.Model.Controls.ITool tool && (tool.Id == contentId || tool.Title == contentId))
                        {
                            return tool;
                        }
                    }
                }

                return null;
            }
        }
    }
}
