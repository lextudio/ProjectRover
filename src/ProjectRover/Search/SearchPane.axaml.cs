using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.Abstractions;
using ICSharpCode.ILSpyX.Extensions;
using ICSharpCode.ILSpyX.Search;
using ICSharpCode.ILSpyX.TreeView;
using TomsToolbox.Essentials;
using ICSharpCode.ILSpy.AppEnv;
using Avalonia.VisualTree;

namespace ICSharpCode.ILSpy.Search;

public partial class SearchPane : UserControl
{
    const int MAX_RESULTS = 1000;
    const int MAX_REFRESH_TIME_MS = 10;
    RunningSearch? currentSearch;
    bool runSearchOnNextShow;
    IComparer<SearchResult>? resultsComparer;
    
    // Dependencies (resolved via App.ExportProvider or similar if not injected)
    AssemblyTreeModel? assemblyTreeModel;
    ITreeNodeFactory? treeNodeFactory;
    SettingsService? settingsService;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public static readonly StyledProperty<bool> IsSearchingProperty =
        AvaloniaProperty.Register<SearchPane, bool>(nameof(IsSearching));

    public bool IsSearching
    {
        get => GetValue(IsSearchingProperty);
        set => SetValue(IsSearchingProperty, value);
    }

    public static readonly StyledProperty<string?> NumberOfResultsTextProperty =
        AvaloniaProperty.Register<SearchPane, string?>(nameof(NumberOfResultsText));

    public string? NumberOfResultsText
    {
        get => GetValue(NumberOfResultsTextProperty);
        set => SetValue(NumberOfResultsTextProperty, value);
    }

    public static readonly StyledProperty<SearchResult?> SelectedSearchResultProperty =
        AvaloniaProperty.Register<SearchPane, SearchResult?>(nameof(SelectedSearchResult));

    public SearchResult? SelectedSearchResult
    {
        get => GetValue(SelectedSearchResultProperty);
        set => SetValue(SelectedSearchResultProperty, value);
    }

    public SearchPane()
    {
        InitializeComponent();
        
        // Resolve dependencies
        if (ProjectRover.App.ExportProvider != null)
        {
            assemblyTreeModel = ProjectRover.App.ExportProvider.GetExportedValue<AssemblyTreeModel>();
            settingsService = ProjectRover.App.ExportProvider.GetExportedValue<SettingsService>();
            treeNodeFactory = ProjectRover.App.ExportProvider.GetExportedValue<ITreeNodeFactory>();
            try {
                Console.WriteLine($"[Log][SearchPane] Resolved AssemblyTreeModel instance={assemblyTreeModel?.GetHashCode()}");
            } catch { }
        }

        // Avalonia doesn't have CompositionTarget.Rendering in the same way, but we can use DispatcherTimer or similar.
        // Or TopLevel.RequestAnimationFrame.
        // For simplicity, let's use a DispatcherTimer for now, or just async updates.
        // WPF uses CompositionTarget.Rendering to update UI on every frame if needed.
        // We can use a timer.
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, UpdateResults);
        timer.Start();
    }

    public TextBox SearchTextBoxControl => SearchTextBox;

    private void OnClearSearchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SearchPaneModel vm)
        {
            vm.SearchTerm = string.Empty;
        }

        SearchTextBox.Focus();
    }

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled)
            return;

        if (sender is not ListBox listBox)
            return;

        var result = GetSearchResultAtPoint(listBox, e.GetPosition(listBox));
        if (result == null)
            return;

        listBox.SelectedItem = result;
        MessageBus.Send(this, new NavigateToReferenceEventArgs(result.Reference)); // TODO: known issue is that first click might not land on the right node as the list is expanding.
        Console.WriteLine("send selection changed message from SearchPane");
        e.Handled = true;
    }

    SearchResult? GetSearchResultAtPoint(ListBox listBox, Point point)
    {
        if (listBox.InputHitTest(point) is not IInputElement input)
            return null;

        for (var current = input as Visual; current != null && current != listBox; current = current.GetVisualParent())
        {
            if (current is ListBoxItem item && item.DataContext is SearchResult searchResult)
                return searchResult;
        }

        return listBox.SelectedItem as SearchResult;
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SearchPaneModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
            StartSearch(vm.SearchTerm);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SearchPaneModel vm)
        {
            if (e.PropertyName == nameof(SearchPaneModel.SearchTerm))
            {
                StartSearch(vm.SearchTerm);
            }
            else if (e.PropertyName == nameof(SearchPaneModel.SelectedSearchMode))
            {
                StartSearch(vm.SearchTerm);
            }
        }
    }

    void UpdateResults(object? sender, EventArgs e)
    {
        if (currentSearch == null)
            return;

        var timer = Stopwatch.StartNew();
        int resultsAdded = 0;
        while (Results.Count < MAX_RESULTS && timer.ElapsedMilliseconds < MAX_REFRESH_TIME_MS && currentSearch.ResultQueue.TryTake(out var result))
        {
            if (resultsComparer != null)
                Results.InsertSorted(result, resultsComparer);
            else
                Results.Add(result);
            ++resultsAdded;
        }
        
        NumberOfResultsText = $"{Results.Count} results found";

        if (resultsAdded <= 0 || Results.Count != MAX_RESULTS)
            return;

        Results.Add(new SearchResult {
            Name = "More than 1000 results found", // Properties.Resources.SearchAbortedMoreThan1000ResultsFound
            Location = null!,
            Assembly = null!,
            Image = null!,
            LocationImage = null!,
            AssemblyImage = null!,
        });

        currentSearch.Cancel();
    }

    async void StartSearch(string? searchTerm)
    {
        if (currentSearch != null)
        {
            currentSearch.Cancel();
            currentSearch = null;
        }
        IsSearching = false;
        
        if (settingsService == null || assemblyTreeModel == null) return;

        resultsComparer = settingsService.DisplaySettings.SortResults ?
            SearchResult.ComparerByFitness :
            SearchResult.ComparerByName;
        Results.Clear();
        NumberOfResultsText = null;

        RunningSearch? startedSearch = null;
        if (!string.IsNullOrEmpty(searchTerm) && DataContext is SearchPaneModel vm)
        {
            IsSearching = true;
            // searchProgressBar.IsIndeterminate = true; // Bind to VM
            
            // We need ITreeNodeFactory. 
            // If we don't have it, we can't search properly.
            // For now, let's assume we can get it or create a temporary one.
            // In ILSpy, TreeNodeFactory is usually tied to the assembly being decompiled.
            // But search runs across all assemblies.
            // The WPF version passes a factory.
            // Let's try to get it from the current assembly list or create a dummy one if needed.
            // Actually, the RunningSearch class uses it.
            
            // Hack: Create a dummy factory if null, just to make it compile.
            // Real implementation needs proper factory.
            var factory = treeNodeFactory ?? new DummyTreeNodeFactory();

            startedSearch = new(await assemblyTreeModel.AssemblyList.GetAllAssemblies(),
                searchTerm,
                vm.SelectedSearchMode.Mode,
                assemblyTreeModel.CurrentLanguage,
                assemblyTreeModel.CurrentLanguageVersion,
                factory,
                settingsService);
            currentSearch = startedSearch;

            await startedSearch.Run();
            IsSearching = false;
        }
    }
    
    sealed class DummyTreeNodeFactory : ITreeNodeFactory
    {
        public ITreeNode CreateDecompilerTreeNode(ILanguage language, EntityHandle handle, MetadataFile module) => null!;
        public ITreeNode CreateResourcesList(MetadataFile module) => null!;
        public ITreeNode Create(Resource resource) => null!;
        public ITreeNode CreateNamespace(string namespaceName) => null!;
        public ITreeNode CreateAssembly(string assemblyName) => null!;
    }

    sealed class RunningSearch
    {
        readonly CancellationTokenSource cts = new();
        readonly IList<LoadedAssembly> assemblies;
        readonly SearchRequest searchRequest;
        readonly SearchMode searchMode;
        readonly Language language;
        readonly ICSharpCode.ILSpyX.LanguageVersion languageVersion;
        readonly ApiVisibility apiVisibility;
        readonly ITreeNodeFactory treeNodeFactory;
        readonly SettingsService settingsService;

        public IProducerConsumerCollection<SearchResult> ResultQueue { get; } = new ConcurrentQueue<SearchResult>();

        public RunningSearch(IList<LoadedAssembly> assemblies, string searchTerm, SearchMode searchMode,
            Language language, ICSharpCode.ILSpyX.LanguageVersion languageVersion, ITreeNodeFactory treeNodeFactory, SettingsService settingsService)
        {
            this.assemblies = assemblies;
            this.language = language;
            this.languageVersion = languageVersion;
            this.searchMode = searchMode;
            this.apiVisibility = settingsService.SessionSettings.LanguageSettings.ShowApiLevel;
            this.treeNodeFactory = treeNodeFactory;
            this.settingsService = settingsService;
            this.searchRequest = Parse(searchTerm);
        }

        SearchRequest Parse(string input)
        {
            string[] parts = CommandLineTools.CommandLineToArgumentArray(input);

            SearchRequest request = new();
            List<string> keywords = new();
            Regex? regex = null;
            request.Mode = searchMode;

            foreach (string part in parts)
            {
                // Parse: [prefix:|@]["]searchTerm["]
                // Find quotes used for escaping
                int prefixLength = part.IndexOfAny(new[] { '"', '/' });
                if (prefixLength < 0)
                {
                    // no quotes
                    prefixLength = part.Length;
                }

                int delimiterLength;
                // Find end of prefix
                if (part.StartsWith("@", StringComparison.Ordinal))
                {
                    prefixLength = 1;
                    delimiterLength = 0;
                }
                else
                {
                    prefixLength = part.IndexOf(':', 0, prefixLength);
                    delimiterLength = 1;
                }
                string? prefix;
                if (prefixLength <= 0)
                {
                    prefix = null;
                    prefixLength = -1;
                }
                else
                {
                    prefix = part.Substring(0, prefixLength);
                }

                // unescape quotes
                string searchTerm = part.Substring(prefixLength + delimiterLength).Trim();
                if (searchTerm.Length > 0)
                {
                    searchTerm = CommandLineTools.CommandLineToArgumentArray(searchTerm)[0];
                }
                else
                {
                    // if searchTerm is only "@" or "prefix:",
                    // then we do not interpret it as prefix, but as searchTerm.
                    searchTerm = part;
                    prefix = null;
                }

                if (prefix == null || prefix.Length <= 2)
                {
                    if (regex == null && searchTerm.StartsWith("/", StringComparison.Ordinal) && searchTerm.Length > 1)
                    {
                        int searchTermLength = searchTerm.Length - 1;
                        if (searchTerm.EndsWith("/", StringComparison.Ordinal))
                        {
                            searchTermLength--;
                        }

                        request.FullNameSearch |= searchTerm.Contains("\\.");

                        regex = CreateRegex(searchTerm.Substring(1, searchTermLength));
                    }
                    else
                    {
                        request.FullNameSearch |= searchTerm.Contains(".");
                        keywords.Add(searchTerm);
                    }
                    request.OmitGenerics |= !(searchTerm.Contains("<") || searchTerm.Contains("`"));
                }

                switch (prefix?.ToUpperInvariant())
                {
                    case "@":
                        request.Mode = SearchMode.Token;
                        break;
                    case "INNAMESPACE":
                        request.InNamespace ??= searchTerm;
                        break;
                    case "INASSEMBLY":
                        request.InAssembly ??= searchTerm;
                        break;
                    case "A":
                        request.AssemblySearchKind = AssemblySearchKind.NameOrFileName;
                        request.Mode = SearchMode.Assembly;
                        break;
                    case "AF":
                        request.AssemblySearchKind = AssemblySearchKind.FilePath;
                        request.Mode = SearchMode.Assembly;
                        break;
                    case "AN":
                        request.AssemblySearchKind = AssemblySearchKind.FullName;
                        request.Mode = SearchMode.Assembly;
                        break;
                    case "N":
                        request.Mode = SearchMode.Namespace;
                        break;
                    case "TM":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.All;
                        break;
                    case "T":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Type;
                        break;
                    case "M":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Member;
                        break;
                    case "MD":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Method;
                        break;
                    case "F":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Field;
                        break;
                    case "P":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Property;
                        break;
                    case "E":
                        request.Mode = SearchMode.Member;
                        request.MemberSearchKind = MemberSearchKind.Event;
                        break;
                    case "C":
                        request.Mode = SearchMode.Literal;
                        break;
                    case "R":
                        request.Mode = SearchMode.Resource;
                        break;
                }
            }

            Regex? CreateRegex(string s)
            {
                try
                {
                    return new(s, RegexOptions.Compiled);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            request.Keywords = keywords.ToArray();
            request.RegEx = regex;
            request.SearchResultFactory = new SearchResultFactory(language);
            request.TreeNodeFactory = this.treeNodeFactory;
            var decompilerSettings = settingsService.DecompilerSettings.Clone();
            if (!Enum.TryParse(this.languageVersion?.Version, out Decompiler.CSharp.LanguageVersion languageVersion))
                languageVersion = Decompiler.CSharp.LanguageVersion.Latest;
            decompilerSettings.SetLanguageVersion(languageVersion);
            request.DecompilerSettings = decompilerSettings;

            return request;
        }

        public void Cancel()
        {
            cts.Cancel();
        }

        public async Task Run()
        {
            try
            {
                await Task.Factory.StartNew(() => {
                    var searcher = GetSearchStrategy(searchRequest);
                    if (searcher == null)
                        return;
                    try
                    {
                        foreach (var loadedAssembly in assemblies)
                        {
                            var module = loadedAssembly.GetMetadataFileOrNull();
                            if (module == null)
                                continue;
                            searcher.Search(module, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore cancellation
                    }

                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // ignore cancellation
            }
        }

        AbstractSearchStrategy? GetSearchStrategy(SearchRequest request)
        {
            if (request.Keywords.Length == 0 && request.RegEx == null)
                return null;

            switch (request.Mode)
            {
                case SearchMode.TypeAndMember:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue);
                case SearchMode.Type:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Type);
                case SearchMode.Member:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Member);
                case SearchMode.Literal:
                    return new LiteralSearchStrategy(language, apiVisibility, request, ResultQueue);
                case SearchMode.Method:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Method);
                case SearchMode.Field:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Field);
                case SearchMode.Property:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Property);
                case SearchMode.Event:
                    return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Event);
                case SearchMode.Token:
                    return new MetadataTokenSearchStrategy(language, apiVisibility, request, ResultQueue);
                case SearchMode.Resource:
                    return new ResourceSearchStrategy(apiVisibility, request, ResultQueue);
                case SearchMode.Assembly:
                    return new AssemblySearchStrategy(request, ResultQueue, AssemblySearchKind.NameOrFileName);
                case SearchMode.Namespace:
                    return new NamespaceSearchStrategy(request, ResultQueue);
            }

            return null;
        }
    }
}
