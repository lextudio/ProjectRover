using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpyX.Search;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.Abstractions;
using ICSharpCode.ILSpyX.Extensions;
using TomsToolbox.Essentials;
using TomsToolbox.Wpf;

namespace ICSharpCode.ILSpy.Search
{
    public partial class SearchPane : UserControl
    {
        const int MAX_RESULTS = 1000;
        const int MAX_REFRESH_TIME_MS = 10;

        RunningSearch? currentSearch;
        bool runSearchOnNextShow;
        IComparer<SearchResult>? resultsComparer;
        readonly AssemblyTreeModel? assemblyTreeModel;
        readonly ITreeNodeFactory? treeNodeFactory;
        readonly SettingsService? settingsService;

        public ObservableCollection<SearchResult> Results { get; } = new();

        public SearchPane()
        {
            InitializeComponent();

            // Try to resolve dependencies from export provider if available
            if (App.ExportProvider != null)
            {
                try
                {
                    assemblyTreeModel = App.ExportProvider.GetExportedValue<AssemblyTreeModel>();
                    treeNodeFactory = App.ExportProvider.GetExportedValue<ITreeNodeFactory>();
                    settingsService = App.ExportProvider.GetExportedValue<SettingsService>();
                }
                catch { }
            }

            // Use a timer to periodically update results (replacement for CompositionTarget.Rendering)
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, UpdateResults);
            timer.Start();
        }

        void CurrentAssemblyList_Changed()
        {
            if (IsVisible)
            {
                StartSearch(this.SearchTerm);
            }
            else
            {
                StartSearch(null);
                runSearchOnNextShow = true;
            }
        }

        void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not LanguageSettings)
                return;

            UpdateFilter();
        }

        void UpdateFilter()
        {
            if (IsVisible)
            {
                StartSearch(this.SearchTerm);
            }
            else
            {
                StartSearch(null);
                runSearchOnNextShow = true;
            }
        }

        public string SearchTerm => (this.FindControl<Controls.SearchBox>("SearchBox")?.Text) ?? string.Empty;

        void FocusSearchBox()
        {
            Dispatcher.UIThread.InvokeAsync(() => {
                var sb = this.FindControl<Controls.SearchBox>("SearchBox");
                sb?.Focus();
                // Attempt to select all if internal TextBox exposed
                var tb = sb?.FindControl<TextBox>("PART_TextBox");
                tb?.SelectAll();
            });
        }

        void SearchBox_TextChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            StartSearch(SearchTerm);
        }

        void SearchBox_PreviewKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            // Forward to existing logic: if down arrow, move focus to results grid
            if (e.Key == Key.Down)
            {
                var grid = this.FindControl<DataGrid>("resultsGrid");
                if (grid?.ItemsSource is IEnumerable enumerable && enumerable.Cast<object>().Any())
                {
                    e.Handled = true;
                    grid.SelectedIndex = grid.SelectedIndex < 0 ? 0 : grid.SelectedIndex;
                    grid.Focus();
                    grid.ScrollIntoView(grid.SelectedItem);
                }
            }
        }

        void SearchModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            StartSearch(this.SearchTerm);
        }

        void ResultsGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            JumpToSelectedItem();
            e.Handled = true;
        }

        void ResultsGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // Middle-click handling
            if (e.InitialPressMouseButton == MouseButton.Middle)
            {
                // Attempt to jump to selected item
                JumpToSelectedItem(inNewTabPage: true);
                e.Handled = true;
            }
        }

        void ResultsGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid ?? this.FindControl<DataGrid>("resultsGrid");
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                JumpToSelectedItem((e.KeyModifiers & KeyModifiers.Control) != 0);
            }
            else if (e.Key == Key.Up && grid?.SelectedIndex == 0)
            {
                e.Handled = true;
                grid.SelectedIndex = -1;
                FocusSearchBox();
            }
        }

        void UpdateResults(object? sender, EventArgs e)
        {
            if (currentSearch == null)
                return;

            var sw = Stopwatch.StartNew();
            int resultsAdded = 0;
            while (Results.Count < MAX_RESULTS && sw.ElapsedMilliseconds < MAX_REFRESH_TIME_MS && currentSearch.ResultQueue.TryTake(out var result))
            {
                Results.Add(result);
                ++resultsAdded;
            }

            if (resultsAdded <= 0 || Results.Count != MAX_RESULTS)
                return;

            Results.Add(new SearchResult {
                Name = Properties.Resources.SearchAbortedMoreThan1000ResultsFound,
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

            resultsComparer = settingsService?.DisplaySettings.SortResults == true ?
                SearchResult.ComparerByFitness :
                SearchResult.ComparerByName;
            Results.Clear();

            RunningSearch? startedSearch = null;
            if (!string.IsNullOrEmpty(searchTerm) && assemblyTreeModel != null && settingsService != null)
            {
                var factory = treeNodeFactory ?? throw new InvalidOperationException("TreeNodeFactory unavailable");

                startedSearch = new RunningSearch(await assemblyTreeModel.AssemblyList.GetAllAssemblies(),
                    searchTerm,
                    (SearchMode)this.FindControl<ComboBox>("searchModeComboBox")?.SelectedIndex!,
                    assemblyTreeModel.CurrentLanguage,
                    assemblyTreeModel.CurrentLanguageVersion,
                    factory,
                    settingsService);
                currentSearch = startedSearch;

                await startedSearch.Run();
            }
        }

        void JumpToSelectedItem(bool inNewTabPage = false)
        {
            var grid = this.FindControl<DataGrid>("resultsGrid");
            if (grid?.SelectedItem is SearchResult result)
            {
                MessageBus.Send(this, new NavigateToReferenceEventArgs(result.Reference, inNewTabPage));
            }
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
                    int prefixLength = part.IndexOfAny(new[] { '"', '/' });
                    if (prefixLength < 0) prefixLength = part.Length;

                    int delimiterLength;
                    if (part.StartsWith("@", StringComparison.Ordinal)) { prefixLength = 1; delimiterLength = 0; }
                    else { prefixLength = part.IndexOf(':', 0, prefixLength); delimiterLength = 1; }

                    string? prefix;
                    if (prefixLength <= 0) { prefix = null; prefixLength = -1; }
                    else prefix = part.Substring(0, prefixLength);

                    string searchTerm = part.Substring(prefixLength + delimiterLength).Trim();
                    if (searchTerm.Length > 0) searchTerm = CommandLineTools.CommandLineToArgumentArray(searchTerm)[0];
                    else { searchTerm = part; prefix = null; }

                    if (prefix == null || prefix.Length <= 2)
                    {
                        if (regex == null && searchTerm.StartsWith("/", StringComparison.Ordinal) && searchTerm.Length > 1)
                        {
                            int searchTermLength = searchTerm.Length - 1;
                            if (searchTerm.EndsWith("/", StringComparison.Ordinal)) searchTermLength--;
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
                        case "@": request.Mode = SearchMode.Token; break;
                        case "INNAMESPACE": request.InNamespace ??= searchTerm; break;
                        case "INASSEMBLY": request.InAssembly ??= searchTerm; break;
                        case "A": request.AssemblySearchKind = AssemblySearchKind.NameOrFileName; request.Mode = SearchMode.Assembly; break;
                        case "AF": request.AssemblySearchKind = AssemblySearchKind.FilePath; request.Mode = SearchMode.Assembly; break;
                        case "AN": request.AssemblySearchKind = AssemblySearchKind.FullName; request.Mode = SearchMode.Assembly; break;
                        case "N": request.Mode = SearchMode.Namespace; break;
                        case "TM": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.All; break;
                        case "T": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Type; break;
                        case "M": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Member; break;
                        case "MD": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Method; break;
                        case "F": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Field; break;
                        case "P": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Property; break;
                        case "E": request.Mode = SearchMode.Member; request.MemberSearchKind = MemberSearchKind.Event; break;
                        case "C": request.Mode = SearchMode.Literal; break;
                        case "R": request.Mode = SearchMode.Resource; break;
                    }
                }

                Regex? CreateRegex(string s)
                {
                    try { return new Regex(s, RegexOptions.Compiled); }
                    catch (ArgumentException) { return null; }
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

            public void Cancel() => cts.Cancel();

            public async Task Run()
            {
                try
                {
                    await Task.Factory.StartNew(() => {
                        var searcher = GetSearchStrategy(searchRequest);
                        if (searcher == null) return;
                        try
                        {
                            foreach (var loadedAssembly in assemblies)
                            {
                                var module = loadedAssembly.GetMetadataFileOrNull();
                                if (module == null) continue;
                                searcher.Search(module, cts.Token);
                            }
                        }
                        catch (OperationCanceledException) { }
                    }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { }
            }

            AbstractSearchStrategy? GetSearchStrategy(SearchRequest request)
            {
                if (request.Keywords.Length == 0 && request.RegEx == null) return null;
                switch (request.Mode)
                {
                    case SearchMode.TypeAndMember: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue);
                    case SearchMode.Type: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Type);
                    case SearchMode.Member: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Member);
                    case SearchMode.Literal: return new LiteralSearchStrategy(language, apiVisibility, request, ResultQueue);
                    case SearchMode.Method: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Method);
                    case SearchMode.Field: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Field);
                    case SearchMode.Property: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Property);
                    case SearchMode.Event: return new MemberSearchStrategy(language, apiVisibility, request, ResultQueue, MemberSearchKind.Event);
                    case SearchMode.Token: return new MetadataTokenSearchStrategy(language, apiVisibility, request, ResultQueue);
                    case SearchMode.Resource: return new ResourceSearchStrategy(apiVisibility, request, ResultQueue);
                    case SearchMode.Assembly: return new AssemblySearchStrategy(request, ResultQueue, AssemblySearchKind.NameOrFileName);
                    case SearchMode.Namespace: return new NamespaceSearchStrategy(request, ResultQueue);
                }
                return null;
            }
        }
    }
}
