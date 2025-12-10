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
using ProjectRover.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using MemberReference = Mono.Cecil.MemberReference;
using TypeDefinition = Mono.Cecil.TypeDefinition;
using FieldDefinition = Mono.Cecil.FieldDefinition;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using PropertyDefinition = Mono.Cecil.PropertyDefinition;
using EventDefinition = Mono.Cecil.EventDefinition;

namespace ProjectRover.ViewModels;

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
    private readonly Dictionary<IMemberDefinition, Node> memberDefinitionToNodeMap = new();
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

        ilSpyBackend = new IlSpyBackend();

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

        LanguageVersions.CollectionChanged += OnLanguageVersionsChanged;
        UpdateLanguageVersions(selectedLanguage.Language);

        RestoreLastAssemblies();
    }

    public ObservableCollection<AssemblyNode> AssemblyNodes { get; } = new();

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<LanguageVersionOption> LanguageVersions { get; } = new();

    public bool HasLanguageVersions => LanguageVersions.Count > 0;

    public ObservableCollection<SearchResult> SearchResults { get; } = new();

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
    private string searchText = string.Empty;

    [RelayCommand]
    private void SetTheme(ThemeOption theme)
    {
        SelectedTheme = theme;
    }

    internal void SelectNodeByMemberReference(MemberReference memberReference)
    {
        // Placeholder for reference navigation; ILSpy mapping will be added later.
        notificationService.ShowNotification(new Notification
        {
            Message = "Go to definition is not wired to the ILSpy backend yet.",
            Level = NotificationLevel.Information
        });
    }

    internal async void TryLoadUnresolvedReference(MemberReference memberReference)
    {
        var storageProvider = (App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow!.StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Load {memberReference.DeclaringType?.FullName ?? "reference"}",
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

    internal void LoadAssemblies(IEnumerable<string> filePaths, bool loadDependencies = false)
    {
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
                var assemblyNode = BuildAssemblyNode(assembly);
                AssemblyNodes.Add(assemblyNode);
                assemblyLookup[assemblyNode] = assembly;
            }

            if (loadDependencies)
            {
                // Dependency loading can be triggered explicitly when needed.
            }
        }

        PersistLastAssemblies();
    }

    [RelayCommand(CanExecute = nameof(CanClearAssemblyList))]
    private void ClearAssemblyList()
    {
        _ = analyticsService.TrackEventAsync(AnalyticsEvents.CloseAll);

        SelectedNode = null;
        AssemblyNodes.Clear();
        memberDefinitionToNodeMap.Clear();
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

    partial void OnSelectedNodeChanged(Node? oldNode, Node? newNode)
    {
        if (!isBackForwardNavigation && oldNode != null)
        {
            backStack.Push(oldNode);
            forwardStack.Clear();
            BackCommand.NotifyCanExecuteChanged();
            ForwardCommand.NotifyCanExecuteChanged();
        }

        Decompile(newNode);
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

    partial void OnSelectedThemeChanged(ThemeOption _)
    {
        PersistLastAssemblies();
    }

    partial void OnShowCompilerGeneratedMembersChanged(bool _)
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
            Document = new TextDocument($"// Assembly: {assembly.AssemblyDefinition.Name.FullName}{System.Environment.NewLine}// Select a type or member to decompile.");
            MainWindow.references.Clear();
            return;
        }

        if (node is MetadataNode metadataNode)
        {
            Document = new TextDocument($"// Metadata for {metadataNode.AssemblyDefinition.Name.FullName}{System.Environment.NewLine}// TODO: Display metadata details.");
            MainWindow.references.Clear();
            return;
        }

        if (node is DebugMetadataNode debugMetadataNode)
        {
            Document = new TextDocument($"// Debug metadata for {debugMetadataNode.AssemblyDefinition.Name.FullName}{System.Environment.NewLine}// TODO: Display debug symbols or PDB info.");
            MainWindow.references.Clear();
            return;
        }

        if (node is ResourceNode resourceNode)
        {
            Document = new TextDocument($"// Resource: {resourceNode.Name}{System.Environment.NewLine}// Type: {resourceNode.Resource.ResourceType}{System.Environment.NewLine}// Embedded resource viewing is not implemented yet.");
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
            var text = ilSpyBackend.DecompileMember(assembly, memberNode.MemberDefinition, selectedLanguage.Language, settings);
            Document = new TextDocument(text);
            MainWindow.references.Clear();
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
        var languageVersion = selectedLanguage.Language == DecompilationLanguage.CSharp
            ? SelectedLanguageVersion?.Version ?? LanguageVersion.Latest
            : LanguageVersion.Latest;

        var settings = new DecompilerSettings(languageVersion);
        if (ShowCompilerGeneratedMembers)
        {
            settings.ExpandMemberDefinitions = true;
        }

        return settings;
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
            _ => Enumerable.Empty<Node>()
        };

    private StartupState LoadStartupState()
    {
        try
        {
            if (!File.Exists(startupStateFilePath))
                return new StartupState();

            var json = File.ReadAllText(startupStateFilePath);
            var state = JsonSerializer.Deserialize<StartupState>(json) ?? new StartupState();
            state.LastAssemblies = (state.LastAssemblies ?? Array.Empty<string>()).Where(File.Exists).ToArray();
            return state;
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
        public string[] LastAssemblies { get; set; } = Array.Empty<string>();
        public string? Theme { get; set; }
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

        if (state.LastAssemblies.Length == 0)
            return;

        LoadAssemblies(state.LastAssemblies);
    }

    private void PersistLastAssemblies()
    {
        var files = AssemblyNodes
            .Select(a => assemblyLookup.TryGetValue(a, out var asm) ? asm.FilePath : null)
            .Where(p => p != null)
            .ToArray();

        SaveStartupState(new StartupState
        {
            LastAssemblies = files!,
            Theme = SelectedTheme?.Variant.ToString()
        });
    }

    partial void OnSelectedPaneIndexChanged(int _)
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
        // Search is not yet implemented on top of the ILSpy backend.
        SearchResults.Clear();
        NumberOfResultsText = string.IsNullOrWhiteSpace(value)
            ? null
            : "Searching assemblies is not available yet.";
        IsSearching = false;
    }

    partial void OnSelectedSearchResultChanged(SearchResult? value)
    {
        if (value == null)
            return;

        // Navigation from search results will be added once ILSpy search is wired up.
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

    private AssemblyNode BuildAssemblyNode(IlSpyAssembly assembly)
    {
        var assemblyNode = new AssemblyNode
        {
            Name = $"{assembly.AssemblyDefinition.Name.Name} ({assembly.AssemblyDefinition.Name.Version})",
            Parent = null,
            AssemblyDefinition = assembly.AssemblyDefinition
        };

        // Clear default children (References) to re-order
        assemblyNode.Children.Clear();

        var metadataNode = new MetadataNode
        {
            Name = "Metadata",
            Parent = assemblyNode,
            AssemblyDefinition = assembly.AssemblyDefinition
        };
        var debugMetadataNode = new DebugMetadataNode
        {
            Name = "Debug Metadata",
            Parent = assemblyNode,
            AssemblyDefinition = assembly.AssemblyDefinition
        };
        BuildMetadataChildren(metadataNode, assembly.PeFile);
        BuildDebugMetadataChildren(debugMetadataNode, assembly.PeFile);

        assemblyNode.Children.Add(metadataNode);
        assemblyNode.Children.Add(debugMetadataNode);
        assemblyNode.Children.Add(assemblyNode.References);

        foreach (var reference in assembly.AssemblyDefinition.MainModule.AssemblyReferences.OrderBy(r => r.Name))
        {
            assemblyNode.References.Items.Add(new UnresolvedReferenceNode
            {
                Name = reference.Name,
                Parent = assemblyNode.References
            });
        }

        var resourcesNode = new ResourcesNode
        {
            Name = "Resources",
            Parent = assemblyNode
        };
        assemblyNode.Children.Add(resourcesNode);

        foreach (var resource in assembly.AssemblyDefinition.MainModule.Resources.OrderBy(r => r.Name))
        {
            resourcesNode.Items.Add(new ResourceNode
            {
                Name = resource.Name,
                Parent = resourcesNode,
                Resource = resource
            });
        }

        foreach (var namespaceGroup in assembly.AssemblyDefinition.MainModule.Types.GroupBy(t => t.Namespace).OrderBy(g => g.Key))
        {
            var namespaceNode = new NamespaceNode
            {
                Name = namespaceGroup.Key == string.Empty ? "<Default namespace>" : namespaceGroup.Key,
                Parent = assemblyNode
            };

            foreach (var typeDefinition in namespaceGroup.OrderBy(t => t.Name))
            {
                var typeNode = BuildTypeSubtree(typeDefinition, namespaceNode);
                namespaceNode.Types.Add(typeNode);
                memberDefinitionToNodeMap[typeDefinition] = typeNode;
            }

            assemblyNode.AddNamespace(namespaceNode);
        }

        return assemblyNode;
    }

    private static void BuildMetadataChildren(MetadataNode metadataNode, PEFile peFile)
    {
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "DOS Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "COFF Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataHeaderNode { Name = "Optional Header", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataDirectoryNode { Name = "Data Directories", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataDirectoryNode { Name = "Debug Directory", Parent = metadataNode });
        metadataNode.Items.Add(new MetadataTablesNode { Name = "Tables", Parent = metadataNode });

        var reader = peFile.Metadata;
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
        debugMetadataNode.Items.Add(new MetadataTablesNode { Name = "Tables", Parent = debugMetadataNode });

        var reader = peFile.Metadata;
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

    private TypeNode BuildTypeSubtree(TypeDefinition typeDefinition, Node parentNode)
    {
        TypeNode typeNode = typeDefinition switch
        {
            { IsEnum: true } => new EnumNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition
            },
            { IsValueType: true } => new StructNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition
            },
            { IsClass: true } => new ClassNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition
            },
            { IsInterface: true } => new InterfaceNode
            {
                Name = typeDefinition.Name,
                Parent = parentNode,
                TypeDefinition = typeDefinition
            },
            _ => throw new NotSupportedException()
        };

        foreach (var memberDefinition in GetMembers(typeDefinition))
        {
            MemberNode node = memberDefinition switch
            {
                FieldDefinition fieldDefinition => new FieldNode
                {
                    Name = fieldDefinition.Name,
                    FieldDefinition = fieldDefinition,
                    Parent = typeNode
                },
                MethodDefinition { IsConstructor: true } methodDefinition => new ConstructorNode
                {
                    Name = methodDefinition.Name,
                    MethodDefinition = methodDefinition,
                    Parent = typeNode
                },
                PropertyDefinition propertyDefinition => new PropertyNode
                {
                    Name = propertyDefinition.Name,
                    PropertyDefinition = propertyDefinition,
                    Parent = typeNode
                },
                MethodDefinition methodDefinition => new MethodNode
                {
                    Name = methodDefinition.Name,
                    MethodDefinition = methodDefinition,
                    Parent = typeNode
                },
                EventDefinition eventDefinition => new EventNode
                {
                    Name = eventDefinition.Name,
                    EventDefinition = eventDefinition,
                    Parent = typeNode
                },
                TypeDefinition nestedTypeDefinition => BuildTypeSubtree(nestedTypeDefinition, typeNode),
                _ => throw new NotSupportedException()
            };

            typeNode.Members.Add(node);
            memberDefinitionToNodeMap[memberDefinition] = node;
        }

        return typeNode;
    }

    private IEnumerable<IMemberDefinition> GetMembers(TypeDefinition typeDefinition)
    {
        IEnumerable<IMemberDefinition> members = typeDefinition.Fields
            .Cast<IMemberDefinition>()
            .Concat(typeDefinition.Methods)
            .Concat(typeDefinition.Properties)
            .Concat(typeDefinition.Events)
            .Concat(typeDefinition.NestedTypes);

        if (!ShowCompilerGeneratedMembers)
        {
            members = members.Where(m => !IsCompilerGenerated(m));
        }

        return members.OrderBy(m => m.MetadataToken.RID);
    }

    private static bool IsCompilerGenerated(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    public record ThemeOption(string Name, ThemeVariant Variant);

    public record LanguageOption(string Name, DecompilationLanguage Language);

    public record LanguageVersionOption(string Name, LanguageVersion Version);
}
