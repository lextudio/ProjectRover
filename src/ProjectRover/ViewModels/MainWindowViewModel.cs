/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ProjectRover.

    ProjectRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProjectRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using ProjectRover.Nodes;
using ProjectRover.Notifications;
using ProjectRover.Options;
using ProjectRover.SearchResults;
using ProjectRover.Services;
using ProjectRover.Services.IlSpyX;
using ProjectRover.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ProjectRover.ViewModels;

public record SearchMode(string Name, string IconKey);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IlSpyBackend ilSpyBackend;
    private readonly INotificationService notificationService;
    private readonly IAnalyticsService analyticsService;
    private readonly IDialogService dialogService;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly StartupOptions startupOptions;
    private readonly string startupStateFilePath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "ProjectRover",
        "startup.json");

    private readonly Dictionary<AssemblyNode, IlSpyAssembly> assemblyLookup = new();
    private readonly Dictionary<(string AssemblyPath, EntityHandle Handle), Node> handleToNodeMap = new();
    private readonly Stack<Node> backStack = new();
    private readonly Stack<Node> forwardStack = new();

    private bool isBackForwardNavigation;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        INotificationService notificationService,
        IAnalyticsService analyticsService,
        IDialogService dialogService,
        IOptions<StartupOptions> startupOptions)
    {
        this.logger = logger;
        this.notificationService = notificationService;
        this.analyticsService = analyticsService;
        this.dialogService = dialogService;
        this.startupOptions = startupOptions.Value;

        ilSpyBackend = new IlSpyBackend
        {
            UseDebugSymbols = this.startupOptions.UseDebugSymbols,
            ApplyWinRtProjections = this.startupOptions.ApplyWinRtProjections
        };

        Languages = new ObservableCollection<LanguageOption>
        {
            new("C#", DecompilationLanguage.CSharp),
            new("IL", DecompilationLanguage.IL)
        };
        selectedLanguage = Languages[0];

        Themes = new ObservableCollection<ThemeOption>
        {
            new("Light", ThemeVariant.Light),
            new("Dark", ThemeVariant.Dark)
        };
        selectedTheme = Themes[0];

        selectedSearchMode = SearchModes[0];

        LanguageVersions.CollectionChanged += OnLanguageVersionsChanged;
        UpdateLanguageVersions(selectedLanguage.Language);

        ShowCompilerGeneratedMembers = this.startupOptions.ShowCompilerGeneratedMembers;
        ShowInternalApi = this.startupOptions.ShowInternalApi;

        RestoreLastAssemblies();
    }

    internal void NavigateByFullName(string fullName)
    {
        var match = handleToNodeMap.FirstOrDefault(kvp =>
            kvp.Value is MemberNode member && string.Equals(member.Entity.FullName, fullName, StringComparison.Ordinal));
        if (match.Value != null)
        {
            SelectedNode = match.Value;
            ExpandParents(match.Value);
            return;
        }

        notificationService.ShowNotification(new Notification
        {
            Message = "Definition not found in the current tree.",
            Level = NotificationLevel.Warning
        });
    }

    public ObservableCollection<AssemblyNode> AssemblyNodes { get; } = new();

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<LanguageVersionOption> LanguageVersions { get; } = new();

    public bool HasLanguageVersions => LanguageVersions.Count > 0;

    public ObservableCollection<SearchResult> SearchResults { get; } = new();
    public ObservableCollection<SearchMode> SearchModes { get; } = new(new[]
    {
        new SearchMode("Types and Members", "ClassIcon"),
        new SearchMode("Type", "ClassIcon"),
        new SearchMode("Member", "MemberIcon"),
        new SearchMode("Method", "MethodIcon"),
        new SearchMode("Field", "FieldIcon"),
        new SearchMode("Property", "PropertyIcon"),
        new SearchMode("Event", "EventIcon"),
        new SearchMode("Constant", "ConstantIcon"),
        new SearchMode("Metadata Token", "MetadataIcon"),
        new SearchMode("Resource", "ResourceFileIcon"),
        new SearchMode("Assembly", "ReferenceIcon"),
        new SearchMode("Namespace", "NamespaceIcon")
    });

    [ObservableProperty]
    private SearchMode selectedSearchMode;

    public bool SearchPaneSelected => SelectedPaneIndex == 1;

    public ObservableCollection<ThemeOption> Themes { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateProjectCommand))]
    private Node? selectedNode;

    [ObservableProperty]
    private TextDocument? document;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateProjectCommand))]
    private LanguageOption selectedLanguage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLanguageVersions))]
    private LanguageVersionOption? selectedLanguageVersion;

    [ObservableProperty]
    private bool showCompilerGeneratedMembers;

    [ObservableProperty]
    private bool showInternalApi;

    [ObservableProperty]
    private ThemeOption selectedTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPaneSelected))]
    private int selectedPaneIndex;

    [ObservableProperty]
    private SearchResult? selectedSearchResult;

    [ObservableProperty]
    private string? numberOfResultsText;

    [ObservableProperty]
    private bool isSearching;

    [ObservableProperty]
    private bool isSearchDockVisible;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isResolving;

    public void RemoveAssembly(AssemblyNode assemblyNode)
    {
        if (!AssemblyNodes.Remove(assemblyNode))
            return;

        if (assemblyLookup.TryGetValue(assemblyNode, out var ilSpyAssembly))
        {
            ilSpyBackend.UnloadAssembly(ilSpyAssembly);
            assemblyLookup.Remove(assemblyNode);
        }

        var assemblyPath = assemblyLookup.TryGetValue(assemblyNode, out var removedAsm)
            ? removedAsm.FilePath
            : string.Empty;

        var toRemove = handleToNodeMap
            .Where(kvp => GetAssemblyNode(kvp.Value) == assemblyNode
                          || (assemblyPath != null && string.Equals(kvp.Key.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase)))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in toRemove)
        {
            handleToNodeMap.Remove(key);
        }

        FilterStack(backStack);
        FilterStack(forwardStack);
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();

        if (SelectedNode is null || GetAssemblyNode(SelectedNode) == assemblyNode)
        {
            SelectedNode = AssemblyNodes.FirstOrDefault();
        }

        PersistLastAssemblies();

        void FilterStack(Stack<Node> stack)
        {
            var filtered = stack.Where(n => GetAssemblyNode(n) != assemblyNode).Reverse().ToList();
            stack.Clear();
            foreach (var node in filtered)
            {
                stack.Push(node);
            }
        }
    }

    [RelayCommand]
    private void SetTheme(ThemeOption theme)
    {
        SelectedTheme = theme;
    }

    internal void SelectNodeByMemberReference(EntityHandle handle)
    {
        if (handle.IsNil)
            return;

        var node = TryResolveHandle(handle);
        if (node != null)
        {
            SelectedNode = node;
            ExpandParents(node);
            return;
        }

        // Try matching full name if token wasn't indexed (e.g., auto-loaded dependency not mapped yet)
        var fallback = handleToNodeMap.Values
            .OfType<MemberNode>()
            .FirstOrDefault(m => m.Entity.MetadataToken.Equals(handle));
        if (fallback != null)
        {
            SelectedNode = fallback;
            ExpandParents(fallback);
            return;
        }

        notificationService.ShowNotification(new Notification
        {
            Message = "Definition not found in the current tree.",
            Level = NotificationLevel.Warning
        });
    }

    public void NavigateToType(EntityHandle typeHandle)
    {
        var node = TryResolveHandle(typeHandle);
        if (node != null)
        {
            SelectedNode = node;
            ExpandParents(node);
            return;
        }

        notificationService.ShowNotification(new Notification
        {
            Message = $"Type not found in tree",
            Level = NotificationLevel.Warning
        });
    }

    private void ExpandParents(Node node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
    }

    internal async void TryLoadUnresolvedReference()
    {
        var storageProvider = (App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow!.StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load reference",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Assemblies")
                {
                    Patterns = new [] { "*.exe", "*.dll" }
                }
            }
        });

        if (files.Count == 0)
            return;

        LoadAssemblies(files.Select(f => f.Path.LocalPath));
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var storageProvider = (App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow!.StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Assembly",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Assemblies")
                {
                    Patterns = new [] { "*.exe", "*.dll" }
                }
            }
        });

        if (files.Count == 0)
            return;

        _ = analyticsService.TrackEventAsync(AnalyticsEvents.OpenFile);
        LoadAssemblies(files.Select(file => file.Path.LocalPath));
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        isBackForwardNavigation = true;
        forwardStack.Push(SelectedNode!);
        SelectedNode = backStack.Pop();
        ForwardCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoBack() => backStack.Any();

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void Forward()
    {
        isBackForwardNavigation = true;
        backStack.Push(SelectedNode!);
        SelectedNode = forwardStack.Pop();
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoForward() => forwardStack.Any();

    internal AssemblyNode? FindAssemblyNodeByFilePath(string filePath)
    {
        return assemblyLookup.FirstOrDefault(kvp => string.Equals(kvp.Value.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).Key;
    }

    internal IReadOnlyList<AssemblyNode> LoadAssemblies(IEnumerable<string> filePaths, bool loadDependencies = false)
    {
        var addedAssemblies = new List<AssemblyNode>();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            if (!processed.Add(filePath))
                continue;

            if (!File.Exists(filePath))
            {
                notificationService.ShowNotification(new Notification
                {
                    Message = $"The file \"{filePath}\" does not exist.",
                    Level = NotificationLevel.Error
                });
                continue;
            }

            var assembly = ilSpyBackend.LoadAssembly(filePath);
            if (assembly == null)
            {
                notificationService.ShowNotification(new Notification
                {
                    Message = $"Failed to load \"{filePath}\".",
                    Level = NotificationLevel.Error
                });
                continue;
            }

            if (!assemblyLookup.Any(kvp => string.Equals(kvp.Value.FilePath, assembly.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                var assemblyNode = IlSpyXTreeAdapter.BuildAssemblyNode(assembly.LoadedAssembly, includeCompilerGenerated: ShowCompilerGeneratedMembers, includeInternal: ShowInternalApi);
                if (assemblyNode == null)
                {
                    notificationService.ShowNotification(new Notification
                    {
                        Message = $"Failed to build tree for \"{filePath}\".",
                        Level = NotificationLevel.Error
                    });
                    continue;
                }
                IndexAssemblyHandles(assemblyNode, assembly.FilePath);
                AssemblyNodes.Add(assemblyNode);
                assemblyLookup[assemblyNode] = assembly;
                addedAssemblies.Add(assemblyNode);
                SelectedNode = assemblyNode;
            }

            if (loadDependencies)
            {
                // Dependency loading can be triggered explicitly when needed.
            }
        }

        PersistLastAssemblies();
        return addedAssemblies;
    }

    [RelayCommand(CanExecute = nameof(CanClearAssemblyList))]
    private void ClearAssemblyList()
    {
        _ = analyticsService.TrackEventAsync(AnalyticsEvents.CloseAll);

        SelectedNode = null;
        AssemblyNodes.Clear();
        handleToNodeMap.Clear();
        assemblyLookup.Clear();

        ClearHistory();
        ilSpyBackend.Clear();
        PersistLastAssemblies();
    }

    [RelayCommand]
    private void SortAssemblies()
    {
        if (AssemblyNodes.Count <= 1)
            return;

        var sorted = AssemblyNodes.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        AssemblyNodes.Clear();
        foreach (var assemblyNode in sorted)
        {
            AssemblyNodes.Add(assemblyNode);
        }

        // adjust selection to the same assembly if possible
        if (SelectedNode is AssemblyNode assemblySelection)
        {
            SelectedNode = AssemblyNodes.FirstOrDefault(a => string.Equals(a.Name, assemblySelection.Name, StringComparison.OrdinalIgnoreCase))
                           ?? SelectedNode;
        }

        PersistLastAssemblies();
    }

    private void ClearHistory()
    {
        backStack.Clear();
        forwardStack.Clear();
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanClearAssemblyList() => assemblyLookup.Any();

    partial void OnSelectedNodeChanged(Node? oldValue, Node? newValue)
    {
        if (!isBackForwardNavigation && oldValue != null)
        {
            backStack.Push(oldValue);
            forwardStack.Clear();
            BackCommand.NotifyCanExecuteChanged();
            ForwardCommand.NotifyCanExecuteChanged();
        }

        Decompile(newValue);
        isBackForwardNavigation = false;
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        AnalyticsEvent? analyticsEvent = value.Language switch
        {
            DecompilationLanguage.CSharp => AnalyticsEvents.ChangeLanguageToCSharp,
            DecompilationLanguage.IL => AnalyticsEvents.ChangeLanguageToIntermediateLanguage,
            _ => null
        };

        UpdateLanguageVersions(value.Language);

        if (analyticsEvent is not null)
        {
            _ = analyticsService.TrackEventAsync(analyticsEvent.Value);
        }
        Decompile(SelectedNode);
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        PersistLastAssemblies();
    }

    partial void OnShowCompilerGeneratedMembersChanged(bool value)
    {
        // Rebuild the tree to account for the new filter.
        var openedAssemblies = assemblyLookup.Values.Select(a => a.FilePath).ToArray();
        ClearAssemblyList();
        LoadAssemblies(openedAssemblies);
    }

    private void UpdateLanguageVersions(DecompilationLanguage language)
    {
        LanguageVersions.Clear();

        if (language == DecompilationLanguage.CSharp)
        {
            foreach (var option in GetCSharpLanguageVersions())
            {
                LanguageVersions.Add(option);
            }

            SelectedLanguageVersion = LanguageVersions.FirstOrDefault();
        }
        else
        {
            SelectedLanguageVersion = null;
        }

        OnPropertyChanged(nameof(HasLanguageVersions));
    }

    private static IEnumerable<LanguageVersionOption> GetCSharpLanguageVersions()
    {
        yield return new LanguageVersionOption("C# (Latest)", LanguageVersion.Latest);
        yield return new LanguageVersionOption("C# 13.0", LanguageVersion.CSharp13_0);
        yield return new LanguageVersionOption("C# 12.0", LanguageVersion.CSharp12_0);
        yield return new LanguageVersionOption("C# 11.0", LanguageVersion.CSharp11_0);
        yield return new LanguageVersionOption("C# 10.0", LanguageVersion.CSharp10_0);
        yield return new LanguageVersionOption("C# 9.0", LanguageVersion.CSharp9_0);
        yield return new LanguageVersionOption("C# 8.0", LanguageVersion.CSharp8_0);
        yield return new LanguageVersionOption("C# 7.3", LanguageVersion.CSharp7_3);
        yield return new LanguageVersionOption("C# 7.2", LanguageVersion.CSharp7_2);
        yield return new LanguageVersionOption("C# 7.0", LanguageVersion.CSharp7);
        yield return new LanguageVersionOption("C# 6.0", LanguageVersion.CSharp6);
        yield return new LanguageVersionOption("C# 5.0", LanguageVersion.CSharp5);
        yield return new LanguageVersionOption("C# 4.0", LanguageVersion.CSharp4);
        yield return new LanguageVersionOption("C# 3.0", LanguageVersion.CSharp3);
        yield return new LanguageVersionOption("C# 2.0", LanguageVersion.CSharp2);
        yield return new LanguageVersionOption("C# 1.0", LanguageVersion.CSharp1);
    }

    private void OnLanguageVersionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasLanguageVersions));
    }

    private void Decompile(Node? node)
    {
        if (node == null)
        {
            Document = null;
            MainWindow.references.Clear();
            return;
        }

        var assemblyNode = GetAssemblyNode(node);
        if (assemblyNode == null || !assemblyLookup.TryGetValue(assemblyNode, out var assembly))
        {
            return;
        }

        if (node is AssemblyNode)
        {
            Document = new TextDocument($"// Assembly: {assembly.PeFile.FileName}{System.Environment.NewLine}// Select a type or member to decompile.");
            MainWindow.references.Clear();
            return;
        }

        if (node is MetadataNode metadataNode)
        {
            Document = new TextDocument($"// Metadata for {metadataNode.PeFile.FileName}{System.Environment.NewLine}// TODO: Display metadata details.");
            MainWindow.references.Clear();
            return;
        }

        if (node is DebugMetadataNode debugMetadataNode)
        {
            Document = new TextDocument($"// Debug metadata for {debugMetadataNode.PeFile.FileName}{System.Environment.NewLine}// TODO: Display debug symbols or PDB info.");
            MainWindow.references.Clear();
            return;
        }

        if (node is ResourceEntryNode resourceNode)
        {
            Document = new TextDocument($"// Resource: {resourceNode.Name}{System.Environment.NewLine}// Embedded resource viewing is not implemented yet.");
            MainWindow.references.Clear();
            return;
        }

        if (node is BaseTypesNode)
        {
            Document = new TextDocument($"// Base Types");
            MainWindow.references.Clear();
            return;
        }

        if (node is NamespaceNode nsNode)
        {
            var content = nsNode.Name == "-" ? "//" : $"// {nsNode.Name}";
            Document = new TextDocument(content);
            MainWindow.references.Clear();
            return;
        }

        if (node is not MemberNode memberNode)
        {
            return;
        }

        try
        {
            var settings = BuildDecompilerSettings();
            var text = ilSpyBackend.DecompileMember(assembly, memberNode.MetadataToken, SelectedLanguage.Language, settings);
            Document = new TextDocument(text);
            MainWindow.references.Clear();
            AddReferenceSegments(text, memberNode);
        }
        catch (Exception ex)
        {
            Document = new TextDocument($"// Failed to decompile {memberNode.Name}:{System.Environment.NewLine}// {ex.Message}");
        }
    }

    private static AssemblyNode? GetAssemblyNode(Node node)
    {
        var current = node;
        while (current.Parent != null)
        {
            current = current.Parent;
        }

        return current as AssemblyNode;
    }

    private DecompilerSettings BuildDecompilerSettings()
    {
        var languageVersion = SelectedLanguage.Language == DecompilationLanguage.CSharp
            ? SelectedLanguageVersion?.Version ?? LanguageVersion.Latest
            : LanguageVersion.Latest;

        var settings = new DecompilerSettings(languageVersion);
        if (ShowCompilerGeneratedMembers)
        {
            settings.ExpandMemberDefinitions = true;
        }

        return settings;
    }

    private void AddReferenceSegments(string text, MemberNode memberNode)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var name = memberNode.Name;
        if (string.IsNullOrEmpty(name))
            return;

        var start = text.IndexOf(name, StringComparison.Ordinal);
        if (start < 0)
            return;

        var segment = new ReferenceTextSegment
        {
            StartOffset = start,
            EndOffset = start + name.Length,
            MemberReference = memberNode.MetadataToken.IsNil ? memberNode.Entity.FullName : memberNode.MetadataToken,
            Resolved = true
        };

        MainWindow.references.Add(segment);
    }

    partial void OnSelectedSearchModeChanged(SearchMode value)
    {
        RunSearch();
        PersistLastAssemblies();
    }

    [RelayCommand]
    private void OpenSearchPane()
    {
        SelectedPaneIndex = 1;
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var root in AssemblyNodes)
        {
            CollapseNode(root);
        }

        PersistLastAssemblies();
    }

    private void CollapseNode(Node node)
    {
        node.IsExpanded = false;
        foreach (var child in GetChildren(node))
        {
            CollapseNode(child);
        }
    }

    private static IEnumerable<Node> GetChildren(Node node) =>
        node switch
        {
            AssemblyNode assemblyNode => assemblyNode.Children,
            ReferencesNode referencesNode => referencesNode.Items,
            ResourcesNode resourcesNode => resourcesNode.Items,
            NamespaceNode namespaceNode => namespaceNode.Types,
            TypeNode typeNode => typeNode.Members,
            BaseTypesNode baseTypesNode => baseTypesNode.Items,
            _ => Enumerable.Empty<Node>()
        };

    private StartupState LoadStartupState()
    {
        try
        {
            if (!File.Exists(startupStateFilePath))
                return new StartupState();

            var json = File.ReadAllText(startupStateFilePath);
            return JsonSerializer.Deserialize<StartupState>(json) ?? new StartupState();
        }
        catch
        {
            return new StartupState();
        }
    }

    private void SaveStartupState(StartupState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(startupStateFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(startupStateFilePath, json);
        }
        catch
        {
            // Best-effort persistence only.
        }
    }

    private sealed class StartupState
    {
        public string? Theme { get; set; }
        public bool IsSearchDockVisible { get; set; }
        public string? SearchMode { get; set; }
        public bool ShowCompilerGeneratedMembers { get; set; }
        public bool ShowInternalApi { get; set; }
        public string[]? LastAssemblies { get; set; }
    }

    private void RestoreLastAssemblies()
    {
        if (!startupOptions.RestoreAssemblies)
            return;

        var state = LoadStartupState();

        if (!string.IsNullOrEmpty(state.Theme))
        {
            var savedTheme = Themes.FirstOrDefault(t => string.Equals(t.Variant.ToString(), state.Theme, StringComparison.OrdinalIgnoreCase))
                             ?? Themes.FirstOrDefault(t => string.Equals(t.Name, state.Theme, StringComparison.OrdinalIgnoreCase));
            if (savedTheme != null)
            {
                SelectedTheme = savedTheme;
            }
        }

        var persistedAssemblies = ilSpyBackend.GetPersistedAssemblyFiles();
        if (persistedAssemblies.Any())
            LoadAssemblies(persistedAssemblies);

        if (!string.IsNullOrEmpty(state.SearchMode))
        {
            var mode = SearchModes.FirstOrDefault(m => string.Equals(m.Name, state.SearchMode, StringComparison.OrdinalIgnoreCase));
            if (mode != null)
            {
                SelectedSearchMode = mode;
            }
        }

        IsSearchDockVisible = state.IsSearchDockVisible;
        ShowCompilerGeneratedMembers = state.ShowCompilerGeneratedMembers;
        ShowInternalApi = state.ShowInternalApi;

        if (state.LastAssemblies?.Length > 0)
        {
            LoadAssemblies(state.LastAssemblies);
        }
    }

    private void PersistLastAssemblies()
    {
        var files = AssemblyNodes
            .Select(a => assemblyLookup.TryGetValue(a, out var asm) ? asm.FilePath : null)
            .Where(p => p != null)
            .ToArray();

        SaveStartupState(new StartupState
        {
            Theme = SelectedTheme?.Variant.ToString(),
            IsSearchDockVisible = IsSearchDockVisible,
            SearchMode = SelectedSearchMode?.Name,
            ShowCompilerGeneratedMembers = ShowCompilerGeneratedMembers,
            ShowInternalApi = ShowInternalApi,
            LastAssemblies = files!
        });
    }

    partial void OnSelectedPaneIndexChanged(int value)
    {
        if (!SearchPaneSelected)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var mainWindow = (App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow as MainWindow;
            mainWindow?.SearchTextBox.Focus();
            mainWindow?.SearchTextBox.SelectAll();
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        NumberOfResultsText = null;
        IsSearching = false;
        RunSearch();
    }

    partial void OnSelectedSearchResultChanged(SearchResult? value)
    {
        if (value == null)
            return;

        NavigateToSearchResult(value);
    }

    public void NavigateToSearchResult(SearchResult? result)
    {
        if (result is not BasicSearchResult basic || basic.TargetNode is not { } target)
            return;

        SelectedNode = target;
        ExpandParents(target);
    }

    private void RunSearch()
    {
        var term = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            SearchResults.Clear();
            NumberOfResultsText = null;
            return;
        }

        var adapter = new IlSpyXSearchAdapter();
        var ilspyAssemblies = assemblyLookup.Values.Select(a => a.LoadedAssembly).ToList();
        var results = adapter.Search(ilspyAssemblies, term, SelectedSearchMode.Name, ResolveNode, includeInternal: ShowInternalApi, includeCompilerGenerated: ShowCompilerGeneratedMembers);

        SearchResults.Clear();
        foreach (var r in results)
        {
            SearchResults.Add(r);
        }
        NumberOfResultsText = $"Found {results.Count} result(s)";
    }

    partial void OnIsSearchDockVisibleChanged(bool value)
    {
        PersistLastAssemblies();
    }

    [RelayCommand(CanExecute = nameof(CanGenerateProject))]
    private void GenerateProject(object? _)
    {
        notificationService.ShowNotification(new Notification
        {
            Message = "Project export is not available in the ILSpy-based build yet.",
            Level = NotificationLevel.Information
        });
    }

    private bool CanGenerateProject() => false;

    [RelayCommand]
    private void OpenAboutDialog()
    {
        _ = analyticsService.TrackEventAsync(AnalyticsEvents.About);
        dialogService.ShowDialog<AboutWindow>();
    }

    [RelayCommand]
    private void ToggleShowCompilerGeneratedMembers()
    {
        ShowCompilerGeneratedMembers = !ShowCompilerGeneratedMembers;
        _ = analyticsService.TrackEventAsync(ShowCompilerGeneratedMembers ? AnalyticsEvents.CompilerGeneratedMembersOn : AnalyticsEvents.CompilerGeneratedMembersOff);
    }

    // Legacy tree build logic is superseded by IlSpyXTreeAdapter.BuildAssemblyNode.

    private static bool IsPublicApi(IEntity entity) =>
        entity.Accessibility == Accessibility.Public
        || entity.Accessibility == Accessibility.Protected
        || entity.Accessibility == Accessibility.ProtectedOrInternal;

    private bool IsCompilerGenerated(IEntity entity)
    {
        // TODO: far from enough
        if (HasDebuggerNonUserCode(entity))
            return true;

        if (entity is ITypeDefinition stateMachine && ImplementsAsyncStateMachine(stateMachine))
            return true;

        var name = entity switch
        {
            ITypeDefinition typeDef => typeDef.MetadataName ?? typeDef.Name,
            _ => entity.Name
        };

        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("<>", StringComparison.Ordinal)
               || name.StartsWith("__App_", StringComparison.Ordinal)
               || name.StartsWith("__StaticArrayInitTypeSize", StringComparison.Ordinal);
    }

    private static bool ImplementsAsyncStateMachine(ITypeDefinition typeDefinition) =>
        typeDefinition.DirectBaseTypes.Any(t => t.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine");

    private static bool HasDebuggerNonUserCode(IEntity entity) =>
        entity.GetAttributes().Any(a => string.Equals(a.AttributeType.FullName, "System.Diagnostics.DebuggerNonUserCodeAttribute", StringComparison.Ordinal));

    private static string GetAccessibilitySuffix(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "Public",
        Accessibility.Protected or Accessibility.ProtectedOrInternal => "Protected",
        Accessibility.Internal => "Internal",
        Accessibility.Private or Accessibility.ProtectedAndInternal => "Private",
        _ => "Public"
    };

    private static string GetClassIconKey(ITypeDefinition typeDefinition)
    {
        if (typeDefinition.IsSealed && (typeDefinition.Accessibility == Accessibility.Public || typeDefinition.Accessibility == Accessibility.ProtectedOrInternal))
            return "ClassSealedIcon";
        return typeDefinition.Accessibility switch
        {
            Accessibility.Public or Accessibility.ProtectedOrInternal => "ClassIcon",
            Accessibility.Protected => "ClassProtectedIcon",
            Accessibility.Internal => "ClassInternalIcon",
            _ => "ClassPrivateIcon"
        };
    }

    private string GetMemberIconKey(IEntity entity)
    {
        if (entity is IMethod { IsConstructor: true })
            return "ConstructorIcon";

        var suffix = GetAccessibilitySuffix(entity.Accessibility);

        var prefix = entity switch
        {
            IField { IsConst: true } => "ConstantIcon",
            IField => "FieldIcon",
            IProperty => "PropertyIcon",
            IEvent => "EventIcon",
            IMethod => "MethodIcon",
            _ => "MethodIcon"
        };

        return $"{prefix}{suffix}";
    }

    private static void BuildMetadataChildren(MetadataNode metadataNode, PEFile peFile)
    {
        var reader = peFile.Metadata;
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "DOS Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "COFF Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "Optional Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataDirectoryNode { Name = "Data Directories", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataDirectoryNode { Name = "Debug Directory", Parent = metadataNode });

        metadataNode.Items.Add(BuildTablesNode(metadataNode, reader));
        metadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"String Heap ({reader.GetHeapSize(HeapIndex.String)})",
            Parent = metadataNode
        });
        metadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"UserString Heap ({reader.GetHeapSize(HeapIndex.UserString)})",
            Parent = metadataNode
        });
        metadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"Guid Heap ({reader.GetHeapSize(HeapIndex.Guid)})",
            Parent = metadataNode
        });
        metadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"Blob Heap ({reader.GetHeapSize(HeapIndex.Blob)})",
            Parent = metadataNode
        });
    }

    private static void BuildDebugMetadataChildren(DebugMetadataNode debugMetadataNode, PEFile peFile)
    {
        var reader = peFile.Metadata;
        debugMetadataNode.Items.Add(BuildTablesNode(debugMetadataNode, reader));
        debugMetadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"String Heap ({reader.GetHeapSize(HeapIndex.String)})",
            Parent = debugMetadataNode
        });
        debugMetadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"UserString Heap ({reader.GetHeapSize(HeapIndex.UserString)})",
            Parent = debugMetadataNode
        });
        debugMetadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"Guid Heap ({reader.GetHeapSize(HeapIndex.Guid)})",
            Parent = debugMetadataNode
        });
        debugMetadataNode.Items.Add(new MetadataHeapNode
        {
            Name = $"Blob Heap ({reader.GetHeapSize(HeapIndex.Blob)})",
            Parent = debugMetadataNode
        });
    }

    private static MetadataTablesNode BuildTablesNode(Node parent, MetadataReader reader)
    {
        var tablesNode = new MetadataTablesNode { Name = "Tables", Parent = parent };

        foreach (var table in Enum.GetValues<TableIndex>())
        {
            var rowCount = reader.GetTableRowCount(table);
            if (rowCount <= 0)
                continue;

            tablesNode.Items.Add(new MetadataTableNode
            {
                Name = $"{(byte)table:X2} {table} ({rowCount})",
                Parent = tablesNode,
                Table = table,
                RowCount = rowCount
            });
        }

        return tablesNode;
    }

    private void BuildReferences(IlSpyAssembly assembly, AssemblyNode assemblyNode)
    {
        var reader = assembly.PeFile.Metadata;
        var resolver = assembly.Resolver;
        IsResolving = true;
        try
        {
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                var name = reader.GetString(reference.Name);
                Node node;
                try
                {
                    var metadataRef = new MetadataAssemblyReference(reference, reader);
                    var resolved = resolver.Resolve(metadataRef);
                    node = resolved is not null
                        ? new ResolvedReferenceNode { Name = name, Parent = assemblyNode.References, FilePath = resolved.FileName }
                        : new UnresolvedReferenceNode { Name = name, Parent = assemblyNode.References };
                }
                catch
                {
                    node = new UnresolvedReferenceNode { Name = name, Parent = assemblyNode.References };
                }

                assemblyNode.References.Items.Add(node);
            }
        }
        finally
        {
            IsResolving = false;
        }
    }

    private void BuildResources(IlSpyAssembly assembly, AssemblyNode assemblyNode)
    {
        var reader = assembly.PeFile.Metadata;
        if (!reader.ManifestResources.Any())
            return;

        var resourcesNode = new ResourcesNode
        {
            Name = "Resources",
            Parent = assemblyNode
        };
        assemblyNode.Children.Add(resourcesNode);

        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            var name = reader.GetString(resource.Name);
            resourcesNode.Items.Add(new ResourceEntryNode
            {
                Name = name,
                Parent = resourcesNode,
                ResourceName = name
            });
        }
    }

    private void BuildTypes(IlSpyAssembly assembly, AssemblyNode assemblyNode)
    {
        var typeSystem = assembly.TypeSystem;
        var module = typeSystem.MainModule;
        var types = module.TypeDefinitions
            .Where(t => ShowCompilerGeneratedMembers || !IsCompilerGenerated(t));

        foreach (var namespaceGroup in types.GroupBy(t => t.Namespace).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var typesInNamespace = namespaceGroup.OrderBy(t => t.Name).ToList();
            if (typesInNamespace.Count == 0)
                continue;

            var namespaceNode = new NamespaceNode
            {
                Name = string.IsNullOrEmpty(namespaceGroup.Key) ? "-" : namespaceGroup.Key,
                Parent = assemblyNode,
                IsPublicAPI = typesInNamespace.Any(IsPublicApi)
            };

            foreach (var typeDefinition in typesInNamespace)
            {
                var typeNode = BuildTypeSubtree(typeDefinition, namespaceNode, assembly);
                if (typeNode != null)
                {
                    namespaceNode.Types.Add(typeNode);
                }
            }

            if (namespaceNode.Types.Count > 0)
            {
                assemblyNode.AddNamespace(namespaceNode);
            }
        }
    }

    private TypeNode? BuildTypeSubtree(ITypeDefinition typeDefinition, Node parentNode, IlSpyAssembly assembly)
    {
        if (!ShowCompilerGeneratedMembers && IsCompilerGenerated(typeDefinition))
            return null;

        TypeNode typeNode = typeDefinition.Kind switch
        {
            TypeKind.Enum => new EnumNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition,
                IsPublicAPI = IsPublicApi(typeDefinition),
                IconKey = GetClassIconKey(typeDefinition),
                Entity = typeDefinition
            },
            TypeKind.Struct => new StructNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition,
                IsPublicAPI = IsPublicApi(typeDefinition),
                IconKey = GetClassIconKey(typeDefinition),
                Entity = typeDefinition
            },
            TypeKind.Interface => new InterfaceNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition,
                IsPublicAPI = IsPublicApi(typeDefinition),
                IconKey = GetClassIconKey(typeDefinition),
                Entity = typeDefinition
            },
            _ => new ClassNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition,
                IsPublicAPI = IsPublicApi(typeDefinition),
                IconKey = GetClassIconKey(typeDefinition),
                Entity = typeDefinition
            }
        };

        RegisterHandle(typeNode, typeDefinition.MetadataToken, assembly.FilePath);

        var directBases = typeDefinition.DirectBaseTypes.Where(t => t.Kind != TypeKind.Unknown).ToList();
        if (directBases.Count > 0)
        {
            var baseTypesNode = new BaseTypesNode
            {
                Name = "Base Types",
                Parent = typeNode
            };
            typeNode.Members.Add(baseTypesNode);

            foreach (var baseType in directBases)
            {
                baseTypesNode.Items.Add(new BaseTypeNode
                {
                    Name = baseType.FullName,
                    Parent = baseTypesNode,
                    Type = baseType
                });
            }
        }

        foreach (var member in GetMembers(typeDefinition))
        {
            switch (member)
            {
                case IField fieldDefinition:
                    var fieldNode = new FieldNode
                    {
                        Name = fieldDefinition.Name,
                        FieldDefinition = fieldDefinition,
                        Parent = typeNode,
                        Entity = fieldDefinition,
                        IsPublicAPI = IsPublicApi(fieldDefinition),
                        IconKey = GetMemberIconKey(fieldDefinition)
                    };
                    typeNode.Members.Add(fieldNode);
                    RegisterHandle(fieldNode, fieldDefinition.MetadataToken, assembly.FilePath);
                    break;
                case IMethod { IsConstructor: true } methodDefinition:
                    var ctorNode = new ConstructorNode
                    {
                        Name = $"{typeDefinition.Name}()",
                        MethodDefinition = methodDefinition,
                        Parent = typeNode,
                        Entity = methodDefinition,
                        IsPublicAPI = IsPublicApi(methodDefinition),
                        IconKey = GetMemberIconKey(methodDefinition)
                    };
                    typeNode.Members.Add(ctorNode);
                    RegisterHandle(ctorNode, methodDefinition.MetadataToken, assembly.FilePath);
                    break;
                case IMethod methodDefinition:
                    var methodNode = new MethodNode
                    {
                        Name = methodDefinition.Name,
                        MethodDefinition = methodDefinition,
                        Parent = typeNode,
                        Entity = methodDefinition,
                        IsPublicAPI = IsPublicApi(methodDefinition),
                        IconKey = GetMemberIconKey(methodDefinition)
                    };
                    typeNode.Members.Add(methodNode);
                    RegisterHandle(methodNode, methodDefinition.MetadataToken, assembly.FilePath);
                    break;
                case IProperty propertyDefinition:
                    var propNode = new PropertyNode
                    {
                        Name = propertyDefinition.Name,
                        PropertyDefinition = propertyDefinition,
                        Parent = typeNode,
                        Entity = propertyDefinition,
                        IsPublicAPI = IsPublicApi(propertyDefinition),
                        IconKey = GetMemberIconKey(propertyDefinition)
                    };
                    typeNode.Members.Add(propNode);
                    RegisterHandle(propNode, propertyDefinition.MetadataToken, assembly.FilePath);
                    break;
                case IEvent eventDefinition:
                    var evtNode = new EventNode
                    {
                        Name = eventDefinition.Name,
                        EventDefinition = eventDefinition,
                        Parent = typeNode,
                        Entity = eventDefinition,
                        IsPublicAPI = IsPublicApi(eventDefinition),
                        IconKey = GetMemberIconKey(eventDefinition)
                    };
                    typeNode.Members.Add(evtNode);
                    RegisterHandle(evtNode, eventDefinition.MetadataToken, assembly.FilePath);
                    break;
                case ITypeDefinition nestedTypeDefinition:
                    var nested = BuildTypeSubtree(nestedTypeDefinition, typeNode, assembly);
                    if (nested != null)
                    {
                        typeNode.Members.Add(nested);
                    }
                    break;
            }
        }

        return typeNode;
    }

    private IEnumerable<IEntity> GetMembers(ITypeDefinition typeDefinition)
    {
        IEnumerable<IEntity> Filter(IEnumerable<IEntity> members) =>
            ShowCompilerGeneratedMembers ? members : members.Where(m => !IsCompilerGenerated(m));

        static IOrderedEnumerable<IEntity> OrderByToken(IEnumerable<IEntity> members) =>
            members.OrderBy(m => MetadataTokens.GetToken(m.MetadataToken));

        var fields = OrderByToken(Filter(typeDefinition.Fields));
        var properties = OrderByToken(Filter(typeDefinition.Properties));
        var constructors = OrderByToken(Filter(typeDefinition.Methods.Where(m => m.IsConstructor)));
        var methods = OrderByToken(Filter(typeDefinition.Methods.Where(m => !m.IsConstructor)));
        var events = OrderByToken(Filter(typeDefinition.Events));
        var nestedTypes = OrderByToken(Filter(typeDefinition.NestedTypes));

        var fieldList = fields.ToList();
        var constants = fieldList.Where(f => ((IField)f).IsConst);
        var instanceFields = fieldList.Where(f => ((IField)f).IsConst == false && !((IField)f).IsStatic);
        var staticFields = fieldList.Where(f => ((IField)f).IsConst == false && ((IField)f).IsStatic);

        var propertyList = properties.ToList();
        var instanceProperties = propertyList.Where(p => !((IProperty)p).IsStatic);
        var staticProperties = propertyList.Where(p => ((IProperty)p).IsStatic);

        var methodList = methods.ToList();
        var instanceMethods = methodList.Where(m => !((IMethod)m).IsStatic);
        var staticMethods = methodList.Where(m => ((IMethod)m).IsStatic);

        return nestedTypes
            .Concat<IEntity>(constants)
            .Concat(instanceFields)
            .Concat(staticFields)
            .Concat(instanceProperties)
            .Concat(staticProperties)
            .Concat(constructors)
            .Concat(instanceMethods)
            .Concat(staticMethods)
            .Concat(events);
    }

    private void IndexAssemblyHandles(AssemblyNode assemblyNode, string assemblyPath)
    {
        foreach (var ns in assemblyNode.Children.OfType<NamespaceNode>())
        {
            foreach (var type in ns.Types)
            {
                IndexTypeHandles(type, assemblyPath);
            }
        }
    }

    private void IndexTypeHandles(TypeNode typeNode, string assemblyPath)
    {
        RegisterHandle(typeNode, typeNode.TypeDefinition.MetadataToken, assemblyPath);

        foreach (var member in typeNode.Members)
        {
            switch (member)
            {
                case TypeNode nested:
                    IndexTypeHandles(nested, assemblyPath);
                    break;
                case MemberNode memberNode:
                    RegisterHandle(memberNode, memberNode.MetadataToken, assemblyPath);
                    break;
            }
        }
    }

    private void RegisterHandle(Node node, EntityHandle metadataToken, string assemblyPath)
    {
        if (metadataToken.IsNil)
            return;
        var keyPath = assemblyPath ?? string.Empty;
        handleToNodeMap[(keyPath, metadataToken)] = node;
    }

    private Node? ResolveNode(EntityHandle handle) => TryResolveHandle(handle);

    private Node? TryResolveHandle(EntityHandle handle) =>
        handleToNodeMap.FirstOrDefault(kvp => kvp.Key.Handle.Equals(handle)).Value;

    public record ThemeOption(string Name, ThemeVariant Variant);

    public record LanguageOption(string Name, DecompilationLanguage Language);

    public record LanguageVersionOption(string Name, LanguageVersion Version);
}
