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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Resources.NetStandard;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Document;
using ProjectRover.Nodes;
using ProjectRover.Notifications;
using ProjectRover.SearchResults;
using ProjectRover.Services;
using ProjectRover.Services.IlSpyX;
using ProjectRover.Settings;
using ProjectRover.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace ProjectRover.ViewModels;

public record SearchMode(string Name, string IconKey);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IlSpyBackend ilSpyBackend;
    private readonly INotificationService notificationService;
    private readonly IAnalyticsService analyticsService;
    private readonly IDialogService dialogService;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IRoverSettingsService roverSettingsService;
    private readonly ICommandCatalog commandCatalog;
    private readonly RoverSessionSettings roverSessionSettings;
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
        IRoverSettingsService roverSettingsService,
        ICommandCatalog commandCatalog)
    {
        this.logger = logger;
        this.notificationService = notificationService;
        this.analyticsService = analyticsService;
        this.dialogService = dialogService;
        this.roverSettingsService = roverSettingsService;
        this.commandCatalog = commandCatalog;

        var startupSettings = roverSettingsService.StartupSettings;
        var sessionSettings = roverSettingsService.SessionSettings;
        roverSessionSettings = sessionSettings;

        ilSpyBackend = new IlSpyBackend
        {
            UseDebugSymbols = startupSettings.UseDebugSymbols,
            ApplyWinRtProjections = startupSettings.ApplyWinRtProjections
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

        var savedMode = SearchModes.FirstOrDefault(m => string.Equals(m.Name, sessionSettings.SelectedSearchMode, StringComparison.OrdinalIgnoreCase));
        selectedSearchMode = savedMode ?? SearchModes[0];

        LanguageVersions.CollectionChanged += OnLanguageVersionsChanged;
        UpdateLanguageVersions(selectedLanguage.Language);

        ShowCompilerGeneratedMembers = startupSettings.ShowCompilerGeneratedMembers;
        ShowInternalApi = startupSettings.ShowInternalApi;
        IsSearchDockVisible = sessionSettings.IsSearchDockVisible;

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
        new SearchMode("Literal", "StringIcon"),
        new SearchMode("Metadata Token", "MetadataIcon"),
        new SearchMode("Resource", "ResourceFileIcon"),
        new SearchMode("Assembly", "ReferenceIcon"),
        new SearchMode("Namespace", "NamespaceIcon")
    });

    [ObservableProperty]
    private SearchMode selectedSearchMode;

    public bool SearchPaneSelected => SelectedPaneIndex == 1;

    public ObservableCollection<ThemeOption> Themes { get; }

    public IReadOnlyList<CommandDescriptor> SharedCommands => commandCatalog.Commands;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateProjectCommand))]
    private Node? selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDocument))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDocument))]
    private Bitmap? resourceImage;

    [ObservableProperty]
    private string? resourceInfo;

    [ObservableProperty]
    private IHighlightingDefinition? documentHighlighting;

    [ObservableProperty]
    private List<InlineObjectSpec> inlineObjects = new();

    public bool ShowDocument => Document is not null && ResourceImage is null;

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

    [RelayCommand(CanExecute = nameof(CanSaveResource))]
    private async Task SaveResourceAsync(ResourceEntryNode resourceNode)
    {
        if (resourceNode.Resource is null)
            return;

        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = resourceNode.Resource.Name
        });
        if (file == null)
            return;

        try
        {
            await using var target = File.Create(file.Path.LocalPath);
            using var source = resourceNode.Resource.TryOpenStream();
            if (source == null)
                return;
            await source.CopyToAsync(target);
            notificationService.ShowNotification(new Notification { Message = $"Saved resource to {file.Path.LocalPath}", Level = NotificationLevel.Information });
        }
        catch (Exception ex)
        {
            notificationService.ShowNotification(new Notification { Message = $"Failed to save resource: {ex.Message}", Level = NotificationLevel.Error });
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportResources))]
    private async Task ExportResourcesAsync(ResourceEntryNode resourceNode)
    {
        if (resourceNode.Resource is null || !resourceNode.Resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return;

        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = Path.ChangeExtension(resourceNode.Resource.Name, ".resx")
        });
        if (file == null)
            return;

        try
        {
            using var stream = resourceNode.Resource.TryOpenStream();
            if (stream == null)
                return;

            using var resources = new ResourcesFile(stream);
            using var writer = new System.Resources.NetStandard.ResXResourceWriter(file.Path.LocalPath);
            foreach (var entry in resources)
            {
                writer.AddResource(entry.Key, entry.Value);
            }
            writer.Generate();
            notificationService.ShowNotification(new Notification { Message = $"Exported resources to {file.Path.LocalPath}", Level = NotificationLevel.Information });
        }
        catch (Exception ex)
        {
            notificationService.ShowNotification(new Notification { Message = $"Failed to export: {ex.Message}", Level = NotificationLevel.Error });
        }
    }

    private bool CanSaveResource(ResourceEntryNode? node) => node?.Resource != null;

    private bool CanExportResources(ResourceEntryNode? node) =>
        node?.Resource != null && node.Resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanExportAllResources))]
    private async Task ExportAllResourcesAsync(ResourcesNode resourcesNode)
    {
        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var folder = folders?.FirstOrDefault();
        if (folder == null)
            return;

        int success = 0;
        foreach (var entry in resourcesNode.Items)
        {
            if (entry.Resource == null)
                continue;

            try
            {
                var safeName = Path.GetFileName(entry.Resource.Name);
                var targetPath = Path.Combine(folder.Path.LocalPath, safeName);
                using var source = entry.Resource.TryOpenStream();
                if (source == null)
                    continue;
                await using var target = File.Create(targetPath);
                await source.CopyToAsync(target);
                success++;
            }
            catch
            {
                // Ignore individual failures to continue exporting others.
            }
        }

        notificationService.ShowNotification(new Notification
        {
            Message = $"Exported {success}/{resourcesNode.Items.Count} resource(s) to {folder.Path.LocalPath}",
            Level = success > 0 ? NotificationLevel.Information : NotificationLevel.Warning
        });
    }

    private bool CanExportAllResources(ResourcesNode? node) => node?.Items?.Any() == true;

    [RelayCommand(CanExecute = nameof(CanExtractResourceEntries))]
    private async Task ExtractResourceEntriesAsync(ResourceEntryNode resourceNode)
    {
        if (resourceNode.Resource is null || !resourceNode.Resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return;

        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var folder = folders?.FirstOrDefault();
        if (folder == null)
            return;

        int success = 0;
        try
        {
            using var stream = resourceNode.Resource.TryOpenStream();
            if (stream == null)
                return;
            using var resources = new ResourcesFile(stream);

            foreach (var entry in resources)
            {
                var safeName = SanitizeFileName(entry.Key);
                var targetPath = Path.Combine(folder.Path.LocalPath, safeName);
                try
                {
                    await WriteResourceEntryAsync(entry.Value, targetPath);
                    success++;
                }
                catch
                {
                    // skip failing entry
                }
            }
        }
        catch
        {
            // ignore and report via notification
        }

        notificationService.ShowNotification(new Notification
        {
            Message = $"Extracted {success} entries to {folder.Path.LocalPath}",
            Level = success > 0 ? NotificationLevel.Information : NotificationLevel.Warning
        });
    }

    private bool CanExtractResourceEntries(ResourceEntryNode? node) =>
        node?.Resource != null && node.Resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase);

    public async void SaveImageEntryAsync(ResourceImageEntry entry)
    {
        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = SanitizeFileName(entry.Name) + ".png"
        });
        if (file == null)
            return;

        await using var target = File.Create(file.Path.LocalPath);
        entry.Image.Save(target);
        notificationService.ShowNotification(new Notification
        {
            Message = $"Saved {entry.Name}",
            Level = NotificationLevel.Information
        });
    }

    [RelayCommand]
    public async Task SaveObjectEntryAsync(object entryObj)
    {
        ResourceObjectEntry? entry = entryObj as ResourceObjectEntry;
        if (entry == null && entryObj is ResourceEntryNode resourceNode && resourceNode.Raw != null)
        {
            entry = new ResourceObjectEntry(resourceNode.Name, DescribeType(resourceNode.Raw), DescribeResourceValue(resourceNode.Raw), resourceNode.Raw);
        }
        if (entry == null)
            return;

        var storageProvider = GetMainWindow()?.StorageProvider;
        if (storageProvider == null || entry.Raw == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = SanitizeFileName(entry.Name)
        });
        if (file == null)
            return;

        await WriteResourceEntryAsync(entry.Raw, file.Path.LocalPath);
        notificationService.ShowNotification(new Notification
        {
            Message = $"Saved {entry.Name}",
            Level = NotificationLevel.Information
        });
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
        roverSettingsService.StartupSettings.ShowCompilerGeneratedMembers = value;
        ReloadAssemblies();
    }

    partial void OnShowInternalApiChanged(bool value)
    {
        roverSettingsService.StartupSettings.ShowInternalApi = value;
        ReloadAssemblies();
    }

    private void ReloadAssemblies()
    {
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
            DocumentHighlighting = null;
            ResourceImage = null;
            ResourceInfo = null;
            InlineObjects = new List<InlineObjectSpec>();
            MainWindow.references.Clear();
            return;
        }

        var assemblyNode = GetAssemblyNode(node);
        if (assemblyNode == null || !assemblyLookup.TryGetValue(assemblyNode, out var assembly))
        {
            return;
        }

        DocumentHighlighting = null;
        ResourceImage = null;
        ResourceInfo = null;
        InlineObjects = new List<InlineObjectSpec>();

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
            // IMPORTANT: Inspired by ILSpy WPF resource viewers, prefer specialized previews for common resource types.
            if (TryRenderResource(resourceNode, out var resourceDocument))
            {
                Document = resourceDocument;
            }
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
            DocumentHighlighting = GetHighlightingForLanguage(SelectedLanguage.Language);
            MainWindow.references.Clear();
            AddReferenceSegments(text, memberNode);
        }
        catch (Exception ex)
        {
            Document = new TextDocument($"// Failed to decompile {memberNode.Name}:{System.Environment.NewLine}// {ex.Message}");
            DocumentHighlighting = null;
        }
    }

    private bool TryRenderResource(ResourceEntryNode resourceNode, out TextDocument? document)
    {
        document = null;

        if (resourceNode.Resource is null && resourceNode.Raw != null)
        {
            return TryRenderResourceValue(resourceNode, resourceNode.Raw, out document);
        }

        if (resourceNode.Resource is null)
        {
            document = new TextDocument("// Resource metadata is not available.");
            DocumentHighlighting = null;
            ResourceImage = null;
            ResourceInfo = null;
            InlineObjects = new List<InlineObjectSpec>();
            return true;
        }

        var extension = Path.GetExtension(resourceNode.Resource.Name);

        try
        {
            using var stream = resourceNode.Resource.TryOpenStream();
            if (stream == null)
            {
                document = new TextDocument("// Unable to open resource stream.");
                InlineObjects = new List<InlineObjectSpec>();
                return true;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;

            if (IsImageExtension(extension))
            {
                var header = BuildResourceHeader(resourceNode, buffer.Length);
                var placeholders = new StringBuilder(header);
                var inlineObjects = new List<InlineObjectSpec>();

                var multiFrames = ExtractImageFrames(buffer).ToList();
                if (multiFrames.Count > 1)
                {
                    for (int i = 0; i < multiFrames.Count; i++)
                    {
                        placeholders.AppendLine($"// Frame {i + 1}: {multiFrames[i].Image.PixelSize.Width}x{multiFrames[i].Image.PixelSize.Height}");
                        placeholders.Append('\uFFFC').AppendLine();
                        inlineObjects.Add(new InlineObjectSpec(placeholders.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.Image, multiFrames[i].Image, null, $"Frame {i + 1} ({multiFrames[i].Label})"));
                    }
                    DocumentHighlighting = null;
                    ResourceImage = null;
                    ResourceInfo = $"Image with {multiFrames.Count} frame(s)";
                    InlineObjects = inlineObjects;
                    document = new TextDocument(placeholders.ToString());
                    return true;
                }

                if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    var frames = ExtractIconFrames(buffer).ToList();
                    if (frames.Count > 0)
                    {
                        for (int i = 0; i < frames.Count; i++)
                        {
                            placeholders.AppendLine($"// Frame {i + 1}: {frames[i].Image.PixelSize.Width}x{frames[i].Image.PixelSize.Height}");
                            placeholders.Append('\uFFFC').AppendLine();
                            inlineObjects.Add(new InlineObjectSpec(placeholders.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.Image, frames[i].Image, null, $"Frame {i + 1}"));
                        }
                        DocumentHighlighting = null;
                        ResourceImage = null;
                        ResourceInfo = $"Icon with {frames.Count} frame(s)";
                        InlineObjects = inlineObjects;
                        document = new TextDocument(placeholders.ToString());
                        return true;
                    }
                }

                // Fallback single-frame image
                if (TryLoadBitmap(buffer, out var bitmap))
                {
                    placeholders.AppendLine();
                    placeholders.Append('\uFFFC');
                    var idx = placeholders.ToString().IndexOf('\uFFFC');
                    DocumentHighlighting = null;
                    ResourceImage = null;
                    ResourceInfo = extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
                        ? $"Icon preview (first frame) {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height} ({extension})"
                        : $"Image {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height} ({extension})";
                    InlineObjects = new List<InlineObjectSpec> { new InlineObjectSpec(idx, InlineObjectKind.Image, bitmap, null, null) };
                    document = new TextDocument(placeholders.ToString());
                    return true;
                }
            }

            buffer.Position = 0;
            var text = RenderResourceText(resourceNode, buffer, extension);
            document = new TextDocument(text);
            DocumentHighlighting = GetHighlightingForExtension(extension);
            ResourceImage = null;
            InlineObjects = new List<InlineObjectSpec>();
            return true;
        }
        catch (Exception ex)
        {
            document = new TextDocument($"// Error reading resource: {ex.Message}");
            InlineObjects = new List<InlineObjectSpec>();
            return true;
        }
    }

    private bool TryRenderResourceValue(ResourceEntryNode resourceNode, object value, out TextDocument? document)
    {
        document = null;
        var extension = Path.GetExtension(resourceNode.Name ?? resourceNode.ResourceName ?? string.Empty);
        try
        {
            switch (value)
            {
                case string s:
                {
                    DocumentHighlighting = null;
                    ResourceImage = null;
                    ResourceInfo = "String entry";
                    InlineObjects = new List<InlineObjectSpec>();
                    var header = BuildResourceHeader(resourceNode, s.Length);
                    document = new TextDocument($"{header}{s}");
                    return true;
                }
                case byte[] bytes:
                {
                    using var ms = new MemoryStream(bytes);
                    var text = RenderResourceText(resourceNode, ms, extension);
                    document = new TextDocument(text);
                    return true;
                }
                case Stream stream:
                {
                    using var buffer = new MemoryStream();
                    if (stream.CanSeek)
                        stream.Position = 0;
                    stream.CopyTo(buffer);
                    buffer.Position = 0;
                    var text = RenderResourceText(resourceNode, buffer, extension);
                    document = new TextDocument(text);
                    return true;
                }
                case Resource res:
                {
                    using var resStream = res.TryOpenStream();
                    if (resStream == null)
                        break;

                    using var buffer = new MemoryStream();
                    resStream.CopyTo(buffer);
                    buffer.Position = 0;
                    var text = RenderResourceText(resourceNode, buffer, extension);
                    document = new TextDocument(text);
                    return true;
                }
                default:
                {
                    DocumentHighlighting = null;
                    ResourceImage = null;
                    ResourceInfo = DescribeResourceValue(value);
                    InlineObjects = new List<InlineObjectSpec>();
                    var header = BuildResourceHeader(resourceNode, 0);
                    document = new TextDocument($"{header}// Value: {DescribeResourceValue(value)}");
                    return true;
                }
            }
        }
        catch
        {
            // fall through to a generic message
        }

        document = new TextDocument("// Unable to render resource entry.");
        DocumentHighlighting = null;
        ResourceImage = null;
        ResourceInfo = null;
        InlineObjects = new List<InlineObjectSpec>();
        return true;
    }

    private static bool TryReadTextPreview(Stream stream, int maxBytes, bool preferText, out string preview, out string encodingName, out bool truncated)
    {
        preview = string.Empty;
        encodingName = "utf-8";
        truncated = false;

        if (!stream.CanRead)
            return false;

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek)
            stream.Position = 0;

        try
        {
            var buffer = new byte[Math.Min(maxBytes, (int)Math.Max(1, stream.CanSeek ? stream.Length : maxBytes))];
            var read = stream.Read(buffer, 0, buffer.Length);

            // Determine if more data exists
            truncated = stream.CanSeek ? stream.Length > read : stream.ReadByte() != -1;

            var (encoding, offset) = DetectEncoding(buffer, read);
            encodingName = encoding.WebName;

            string text;
            try
            {
                text = encoding.GetString(buffer, offset, read - offset);
            }
            catch
            {
                return false;
            }

            if (!LooksLikeText(text) && !preferText)
                return false;

            preview = read == 0 ? string.Empty : text;
            if (preview.Length > 4000)
            {
                preview = preview.Substring(0, 4000);
                truncated = true;
            }

            return true;
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPosition;
        }
    }

    private string RenderResourceText(ResourceEntryNode resourceNode, MemoryStream stream, string extension)
    {
        var sb = new StringBuilder();
        sb.Append(BuildResourceHeader(resourceNode, stream.Length));
        stream.Position = 0;

        // IMPORTANT: Mirror ILSpy WPF resource handling for structured formats where possible.
        if (IsResx(extension) && TryRenderResx(stream, sb, out var resxText))
            return resxText ?? sb.ToString();

        if (resourceNode.Resource?.ResourceType == ResourceType.Embedded
            && resourceNode.Resource.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        {
            var entries = new ResourcesFile(stream).ToList();
            sb.AppendLine($"// Entries in .resources (inline below): {entries.Count}");

            var strings = new List<KeyValuePair<string, string>>();
            var others = new List<KeyValuePair<string, string>>();
            var objects = new List<ResourceObjectEntry>();
            var images = new List<ResourceImageEntry>();
            var packages = new List<ResourceObjectEntry>();
            foreach (var entry in entries)
            {
                if (entry.Value is string str)
                {
                    strings.Add(new KeyValuePair<string, string>(entry.Key, str));
                }
                else if (TryExtractImage(entry.Key, entry.Value, out var imageEntry))
                {
                    images.Add(imageEntry);
                }
                else if (IsPackageEntry(entry.Value, out var packageEntry))
                {
                    packages.Add(packageEntry);
                }
                else if (IsPrimitiveEntry(entry.Value))
                {
                    others.Add(new KeyValuePair<string, string>($"{entry.Key} [{DescribeType(entry.Value)}]", DescribeResourceValue(entry.Value)));
                }
                else
                {
                    objects.Add(new ResourceObjectEntry(entry.Key, DescribeType(entry.Value), DescribeResourceValue(entry.Value), entry.Value));
                }
            }

            var textBuilder = new StringBuilder(sb.ToString());
            var inlineObjects = new List<InlineObjectSpec>();
            if (strings.Count > 0)
            {
                textBuilder.AppendLine("// String entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                var placeholderIndex = textBuilder.ToString().IndexOf('\uFFFC');
                inlineObjects.Add(new InlineObjectSpec(placeholderIndex, InlineObjectKind.Table, null, strings, "Strings"));
            }
            if (others.Count > 0)
            {
                textBuilder.AppendLine("// Other entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                var placeholderIndex = textBuilder.ToString().LastIndexOf('\uFFFC');
                inlineObjects.Add(new InlineObjectSpec(placeholderIndex, InlineObjectKind.Table, null, others, "Objects/Binary"));
            }
            if (objects.Count > 0)
            {
                textBuilder.AppendLine("// Complex objects:");
                textBuilder.Append("\uFFFC").AppendLine();
                var placeholderIndex = textBuilder.ToString().LastIndexOf('\uFFFC');
                inlineObjects.Add(new InlineObjectSpec(placeholderIndex, InlineObjectKind.ObjectTable, null, null, "Objects", objects));
            }
            if (images.Count > 0)
            {
                textBuilder.AppendLine("// Image list entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                var placeholderIndex = textBuilder.ToString().LastIndexOf('\uFFFC');
                inlineObjects.Add(new InlineObjectSpec(placeholderIndex, InlineObjectKind.Image, null, null, "Image List", null, images));
            }
            if (packages.Count > 0)
            {
                textBuilder.AppendLine("// Package entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                var placeholderIndex = textBuilder.ToString().LastIndexOf('\uFFFC');
                inlineObjects.Add(new InlineObjectSpec(placeholderIndex, InlineObjectKind.ObjectTable, null, null, "Packages", packages));
            }

            InlineObjects = inlineObjects;
            return textBuilder.ToString();
        }
        else
        {
            var preferText = ShouldTreatAsText(extension);
            // IMPORTANT: Inspired by ILSpy WPF resource node factories: prefer text preview for known text-like extensions.
            if (TryReadTextPreview(stream, 4096, preferText, out var textPreview, out var encodingName, out var truncated))
            {
                if (ShouldFormatXml(extension) && TryFormatXml(textPreview, out var formatted))
                {
                    textPreview = formatted;
                }
                sb.AppendLine($"// Text preview ({encodingName}{(truncated ? ", truncated" : string.Empty)}):");
                sb.AppendLine(textPreview);
            }
            else
            {
                sb.AppendLine("// Hex preview (first 256 bytes):");
                sb.AppendLine(RenderHexPreview(stream, 256));
            }
        }

        return sb.ToString();
    }

    private static string BuildResourceHeader(ResourceEntryNode resourceNode, long size)
    {
        var sb = new StringBuilder();
        var name = resourceNode.ResourceName ?? resourceNode.Name;
        sb.AppendLine($"// Resource: {name}");
        var type = resourceNode.Resource?.ResourceType.ToString() ?? (resourceNode.Raw != null ? DescribeType(resourceNode.Raw) : null);
        if (!string.IsNullOrWhiteSpace(type))
            sb.AppendLine($"// Type: {type}");
        if (resourceNode.Resource != null)
            sb.AppendLine($"// Attributes: {resourceNode.Resource.Attributes}");
        sb.AppendLine($"// Size: {size} bytes");
        return sb.ToString();
    }

    private static bool IsImageExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryLoadBitmap(MemoryStream stream, [NotNullWhen(true)] out Bitmap? bitmap)
    {
        bitmap = null;
        try
        {
            stream.Position = 0;
            bitmap = new Bitmap(stream);
            return true;
        }
        catch
        {
            bitmap = null;
            return false;
        }
    }

    private static IEnumerable<(Bitmap Image, string Label)> ExtractIconFrames(MemoryStream stream)
    {
        var frames = new List<(Bitmap, string)>();
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt16() != 0)
                return frames;
            if (reader.ReadUInt16() != 1)
                return frames;
            var count = reader.ReadUInt16();
            var entries = new List<(byte Width, byte Height, uint Size, uint Offset)>();
            for (int i = 0; i < count; i++)
            {
                byte width = reader.ReadByte();
                byte height = reader.ReadByte();
                reader.ReadByte(); // color count
                reader.ReadByte(); // reserved
                reader.ReadUInt16(); // planes
                reader.ReadUInt16(); // bitcount
                uint size = reader.ReadUInt32();
                uint offset = reader.ReadUInt32();
                entries.Add((width == 0 ? (byte)255 : width, height == 0 ? (byte)255 : height, size, offset));
            }

            foreach (var entry in entries)
            {
                var bytes = new byte[entry.Size];
                long previous = stream.Position;
                stream.Position = entry.Offset;
                stream.Read(bytes, 0, bytes.Length);
                stream.Position = previous;
                using var frameStream = new MemoryStream(bytes);
                if (TryLoadBitmap(frameStream, out var bmp) && bmp != null)
                {
                    frames.Add((bmp, $"{entry.Width}x{entry.Height}"));
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return frames;
    }

    private static IEnumerable<(Bitmap Image, string Label)> ExtractImageFrames(MemoryStream buffer)
    {
        var frames = new List<(Bitmap, string)>();
        try
        {
            var data = buffer.ToArray();
            using var skData = SKData.CreateCopy(data);
            using var codec = SKCodec.Create(skData);
            if (codec == null)
                return frames;

            var frameInfos = codec.FrameInfo;
            if (frameInfos.Length <= 1)
                return frames;

            for (int i = 0; i < frameInfos.Length; i++)
            {
                using var skBitmap = new SKBitmap(codec.Info.Width, codec.Info.Height);
                codec.GetPixels(skBitmap.Info, skBitmap.GetPixels(), new SKCodecOptions(i));
                using var skImage = SKImage.FromBitmap(skBitmap);
                using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                encoded.SaveTo(ms);
                ms.Position = 0;
                var avaloniaBmp = new Bitmap(ms);
                frames.Add((avaloniaBmp, $"{codec.Info.Width}x{codec.Info.Height}"));
            }
        }
        catch
        {
            // ignore decoding errors
        }

        return frames;
    }

    private static bool TryExtractImage(string name, object? value, [NotNullWhen(true)] out ResourceImageEntry? imageEntry)
    {
        imageEntry = null;
        if (value is byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            if (TryLoadBitmap(ms, out var bmp) && bmp != null)
            {
                imageEntry = new ResourceImageEntry(name, bmp, $"{bmp.PixelSize.Width}x{bmp.PixelSize.Height}");
                return true;
            }
        }

        if (value is Stream stream)
        {
            var pos = stream.CanSeek ? stream.Position : 0;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            if (TryLoadBitmap(ms, out var bmp) && bmp != null)
            {
                imageEntry = new ResourceImageEntry(name, bmp, $"{bmp.PixelSize.Width}x{bmp.PixelSize.Height}");
                if (stream.CanSeek)
                    stream.Position = pos;
                return true;
            }
            if (stream.CanSeek)
                stream.Position = pos;
        }

        return false;
    }

    private static bool IsPackageEntry(object? value, [NotNullWhen(true)] out ResourceObjectEntry? entry)
    {
        entry = null;
        if (value is Resource { ResourceType: ResourceType.AssemblyLinked or ResourceType.Linked or ResourceType.Embedded } res
            && res.Name.StartsWith("bundle://", StringComparison.OrdinalIgnoreCase))
        {
            entry = new ResourceObjectEntry(res.Name, DescribeType(res), "Package Entry", res);
            return true;
        }

        if (value is ZipArchiveEntry zip)
        {
            entry = new ResourceObjectEntry(zip.FullName, "ZipArchiveEntry", "Package Entry", zip);
            return true;
        }

        return false;
    }

    private static bool IsResx(string extension) =>
        extension.Equals(".resx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".resw", StringComparison.OrdinalIgnoreCase);

    private bool TryRenderResx(MemoryStream stream, StringBuilder sb, out string? textWithInline)
    {
        textWithInline = null;
        try
        {
            stream.Position = 0;
            using var reader = new ResXResourceReader(stream);
            var entries = reader.OfType<DictionaryEntry>().ToList();
            sb.AppendLine($"// RESX entries (inline below): {entries.Count}");

            var strings = new List<KeyValuePair<string, string>>();
            var others = new List<KeyValuePair<string, string>>();
            var objects = new List<ResourceObjectEntry>();
            var images = new List<ResourceImageEntry>();
            var packages = new List<ResourceObjectEntry>();
            foreach (DictionaryEntry entry in entries)
            {
                if (entry.Value is string str)
                {
                    strings.Add(new KeyValuePair<string, string>(entry.Key?.ToString() ?? string.Empty, str));
                }
                else
                {
                    if (TryExtractImage(entry.Key?.ToString() ?? string.Empty, entry.Value, out var imageEntry))
                    {
                        images.Add(imageEntry);
                    }
                    else if (IsPackageEntry(entry.Value, out var packageEntry))
                    {
                        packages.Add(packageEntry);
                    }
                    else if (IsPrimitiveEntry(entry.Value))
                    {
                        others.Add(new KeyValuePair<string, string>(
                            $"{entry.Key?.ToString() ?? string.Empty} [{DescribeType(entry.Value)}]",
                            DescribeResourceValue(entry.Value)));
                    }
                    else
                    {
                        objects.Add(new ResourceObjectEntry(entry.Key?.ToString() ?? string.Empty, DescribeType(entry.Value), DescribeResourceValue(entry.Value), entry.Value));
                    }
                }
            }

            var textBuilder = new StringBuilder(sb.ToString());
            var inlineObjects = new List<InlineObjectSpec>();
            if (strings.Count > 0)
            {
                textBuilder.AppendLine("// String entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                inlineObjects.Add(new InlineObjectSpec(textBuilder.ToString().IndexOf('\uFFFC'), InlineObjectKind.Table, null, strings, "Strings"));
            }
            if (others.Count > 0)
            {
                textBuilder.AppendLine("// Other entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                inlineObjects.Add(new InlineObjectSpec(textBuilder.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.Table, null, others, "Objects/Binary"));
            }
            if (objects.Count > 0)
            {
                textBuilder.AppendLine("// Complex objects:");
                textBuilder.Append("\uFFFC").AppendLine();
                inlineObjects.Add(new InlineObjectSpec(textBuilder.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.ObjectTable, null, null, "Objects", objects));
            }
            if (images.Count > 0)
            {
                textBuilder.AppendLine("// Image list entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                inlineObjects.Add(new InlineObjectSpec(textBuilder.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.Image, null, null, "Image List", null, images));
            }
            if (packages.Count > 0)
            {
                textBuilder.AppendLine("// Package entries:");
                textBuilder.Append("\uFFFC").AppendLine();
                inlineObjects.Add(new InlineObjectSpec(textBuilder.ToString().LastIndexOf('\uFFFC'), InlineObjectKind.ObjectTable, null, null, "Packages", packages));
            }

            InlineObjects = inlineObjects;
            textWithInline = textBuilder.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (Encoding Encoding, int Offset) DetectEncoding(byte[] buffer, int count)
    {
        if (count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return (Encoding.UTF8, 3);
        if (count >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            return (Encoding.Unicode, 2);
        if (count >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
            return (Encoding.BigEndianUnicode, 2);

        return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false), 0);
    }

    private static bool LooksLikeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var controlCount = 0;
        foreach (var ch in text)
        {
            if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
                controlCount++;
        }

        return controlCount <= text.Length / 10 + 1;
    }

    // IMPORTANT: Borrowed idea from ILSpy WPF resource handlers: use file extension hints to decide on text rendering.
    private static bool ShouldTreatAsText(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xslt", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".resx", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".resw", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".nuspec", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".resjson", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldFormatXml(string extension) =>
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xslt", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".resx", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".resw", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".nuspec", StringComparison.OrdinalIgnoreCase);

    private static bool TryFormatXml(string text, out string formatted)
    {
        formatted = text;
        try
        {
            var doc = XDocument.Parse(text);
            formatted = doc.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IHighlightingDefinition? GetHighlightingForExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        try
        {
            return HighlightingManager.Instance.GetDefinitionByExtension(extension);
        }
        catch
        {
            return null;
        }
    }

    private static IHighlightingDefinition? GetHighlightingForLanguage(DecompilationLanguage language)
    {
        try
        {
            return language == DecompilationLanguage.CSharp
                ? HighlightingManager.Instance.GetDefinition("C#")
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeResourceValue(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"\"{(s.Length > 120 ? s[..120] + "..." : s)}\"{(s.Length > 120 ? " (truncated)" : string.Empty)}",
            byte[] bytes => $"byte[{bytes.Length}]",
            Stream stream => $"Stream (length {(stream.CanSeek ? stream.Length.ToString() : "unknown")})",
            _ => value?.ToString() ?? "(unknown)"
        };
    }

    private static string DescribeType(object? value)
    {
        return value switch
        {
            null => "null",
            byte[] bytes => $"byte[] ({bytes.Length} bytes)",
            string => "string",
            Stream stream => stream.CanSeek ? $"Stream ({stream.Length} bytes)" : "Stream",
            Resource res => $"Resource ({res.ResourceType})",
            ZipArchiveEntry => "ZipArchiveEntry",
            _ => value?.GetType().FullName ?? "unknown"
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "resource" : sanitized;
    }

    private static bool IsPrimitiveEntry(object? value) =>
        value is null or string or byte[] or Stream or bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static async Task WriteResourceEntryAsync(object? value, string targetPath)
    {
        switch (value)
        {
            case null:
                await File.WriteAllTextAsync(targetPath + ".txt", "null");
                break;
            case string s:
                await File.WriteAllTextAsync(targetPath + ".txt", s);
                break;
            case byte[] bytes:
                await File.WriteAllBytesAsync(targetPath, bytes);
                break;
            case Stream stream:
            {
                await using var target = File.Create(targetPath);
                if (stream.CanSeek)
                    stream.Position = 0;
                await stream.CopyToAsync(target);
                break;
            }
            case Resource res:
            {
                using var source = res.TryOpenStream();
                if (source == null)
                    break;
                await using var target = File.Create(targetPath);
                await source.CopyToAsync(target);
                break;
            }
            case ZipArchiveEntry zipEntry:
            {
                await using var target = File.Create(targetPath);
                await using var source = zipEntry.Open();
                await source.CopyToAsync(target);
                break;
            }
            default:
                await File.WriteAllTextAsync(targetPath + ".txt", value.ToString() ?? string.Empty);
                break;
        }
    }

    private static string RenderHexPreview(Stream stream, int maxBytes)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var buffer = new byte[Math.Min(maxBytes, (int)(stream.CanSeek ? stream.Length : maxBytes))];
        var read = stream.Read(buffer, 0, buffer.Length);
        var sb = new StringBuilder();

        for (int i = 0; i < read; i += 16)
        {
            sb.Append(i.ToString("X4")).Append(": ");
            var lineLength = Math.Min(16, read - i);
            for (int j = 0; j < lineLength; j++)
            {
                sb.Append(buffer[i + j].ToString("X2")).Append(' ');
            }
            sb.Append("  ");
            for (int j = 0; j < lineLength; j++)
            {
                var b = buffer[i + j];
                sb.Append(b is >= 32 and <= 126 ? (char)b : '.');
            }
            sb.AppendLine();
        }

        return sb.ToString();
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

        var declarations = new List<(string Identifier, object Reference)>();

        if (!memberNode.MetadataToken.IsNil)
            declarations.Add((memberNode.Name, memberNode.MetadataToken));

        if (memberNode.Entity?.DeclaringType != null)
        {
            var declaring = memberNode.Entity.DeclaringType;
            declarations.Add((declaring.Name, declaring));
            if (!string.IsNullOrEmpty(declaring.FullName))
                declarations.Add((declaring.FullName, declaring));
        }

        foreach (var (identifier, reference) in declarations.Where(d => !string.IsNullOrWhiteSpace(d.Identifier)))
        {
            foreach (var (start, length) in FindAllOccurrences(text, identifier))
            {
                MainWindow.references.Add(new ReferenceTextSegment
                {
                    StartOffset = start,
                    EndOffset = start + length,
                    MemberReference = reference,
                    Resolved = true
                });
            }
        }
    }

    private static IEnumerable<(int Start, int Length)> FindAllOccurrences(string text, string term)
    {
        var startIndex = 0;
        while (startIndex < text.Length)
        {
            var idx = text.IndexOf(term, startIndex, StringComparison.Ordinal);
            if (idx < 0)
                yield break;
            yield return (idx, term.Length);
            startIndex = idx + term.Length;
        }
    }

    partial void OnSelectedSearchModeChanged(SearchMode value)
    {
        roverSessionSettings.SelectedSearchMode = value?.Name;
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
            public string[]? LastAssemblies { get; set; }
        }

    private void RestoreLastAssemblies()
    {
        var startupSettings = roverSettingsService.StartupSettings;
        if (!startupSettings.RestoreAssemblies)
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
        {
            LoadAssemblies(persistedAssemblies);
        }
        else if (state.LastAssemblies?.Length > 0)
        {
            LoadAssemblies(state.LastAssemblies);
        }

        if (!string.IsNullOrEmpty(roverSessionSettings.SelectedSearchMode))
        {
            var mode = SearchModes.FirstOrDefault(m => string.Equals(m.Name, roverSessionSettings.SelectedSearchMode, StringComparison.OrdinalIgnoreCase));
            if (mode != null)
            {
                SelectedSearchMode = mode;
            }
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
        var results = adapter.Search(ilspyAssemblies, term, SelectedSearchMode.Name, ResolveNode, ResolveAssemblyNode, ResolveResourceNode, includeInternal: ShowInternalApi, includeCompilerGenerated: ShowCompilerGeneratedMembers);

        SearchResults.Clear();
        foreach (var r in results)
        {
            SearchResults.Add(r);
        }
        NumberOfResultsText = $"Found {results.Count} result(s)";
    }

    partial void OnIsSearchDockVisibleChanged(bool value)
    {
        roverSessionSettings.IsSearchDockVisible = value;
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

    private Node? ResolveResourceNode(string assemblyName, string resourceName)
    {
        var assemblyNode = AssemblyNodes.FirstOrDefault(a => string.Equals(a.Name, assemblyName, StringComparison.OrdinalIgnoreCase));
        if (assemblyNode == null)
            return null;

        var resourcesNode = assemblyNode.Children.OfType<ResourcesNode>().FirstOrDefault();
        if (resourcesNode == null)
            return null;

        foreach (var resourceNode in resourcesNode.Items)
        {
            var candidate = FindResourceEntry(resourceNode, resourceName);
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private Node? ResolveAssemblyNode(string assemblyName) =>
        AssemblyNodes.FirstOrDefault(a => string.Equals(a.Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static ResourceEntryNode? FindResourceEntry(ResourceEntryNode node, string resourceName)
    {
        if (string.Equals(node.ResourceName ?? node.Name, resourceName, StringComparison.OrdinalIgnoreCase))
            return node;

        foreach (var child in node.Children)
        {
            if (child is ResourceEntryNode childResource)
            {
                var match = FindResourceEntry(childResource, resourceName);
                if (match != null)
                    return match;
            }
        }

        return null;
    }

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private Node? TryResolveHandle(EntityHandle handle) =>
        handleToNodeMap.FirstOrDefault(kvp => kvp.Key.Handle.Equals(handle)).Value;

    public record ThemeOption(string Name, ThemeVariant Variant);

    public record LanguageOption(string Name, DecompilationLanguage Language);

    public record LanguageVersionOption(string Name, LanguageVersion Version);

    public enum InlineObjectKind
    {
        Image,
        Table,
        ObjectTable
    }

    public record InlineObjectSpec(
        int Offset,
        InlineObjectKind Kind,
        Bitmap? Image = null,
        IReadOnlyList<KeyValuePair<string, string>>? Entries = null,
        string? Caption = null,
        IReadOnlyList<ResourceObjectEntry>? ObjectEntries = null,
        IReadOnlyList<ResourceImageEntry>? ImageEntries = null);

    public record ResourceObjectEntry(string Name, string Type, string Display, object? Raw);

    public record ResourceImageEntry(string Name, Bitmap Image, string Label);
}
