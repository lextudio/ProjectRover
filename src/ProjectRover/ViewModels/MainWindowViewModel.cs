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
using System.Collections;
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
    private readonly AssemblyTreeModel assemblyTreeModel;
    private readonly IlSpyBackend ilSpyBackend;
    private readonly INotificationService notificationService;
    private readonly IAnalyticsService analyticsService;
    private readonly IDialogService dialogService;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly ProjectRover.Services.Navigation.INavigationService navigationService;
    private readonly ISettingsService roverSettingsService;
    private readonly ICommandCatalog commandCatalog;
    private readonly RoverSessionSettings roverSessionSettings;
    private readonly string startupStateFilePath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "ProjectRover",
        "startup.json");

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        INotificationService notificationService,
        IAnalyticsService analyticsService,
        IDialogService dialogService,
        ISettingsService roverSettingsService,
        ICommandCatalog commandCatalog,
        AssemblyTreeModel assemblyTreeModel,
        ProjectRover.Services.Navigation.INavigationService navigationService)
    {
        this.logger = logger;
        this.notificationService = notificationService;
        this.analyticsService = analyticsService;
        this.dialogService = dialogService;
        this.roverSettingsService = roverSettingsService;
        this.commandCatalog = commandCatalog;
        this.assemblyTreeModel = assemblyTreeModel ?? throw new ArgumentNullException(nameof(assemblyTreeModel));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        var startupSettings = roverSettingsService.StartupSettings;
        var sessionSettings = roverSettingsService.SessionSettings;
        roverSessionSettings = sessionSettings;

        ilSpyBackend = assemblyTreeModel.Backend;
        ilSpyBackend.UseDebugSymbols = startupSettings.UseDebugSymbols;
        ilSpyBackend.ApplyWinRtProjections = startupSettings.ApplyWinRtProjections;
        assemblyTreeModel.PropertyChanged += AssemblyTreeModelOnPropertyChanged;

        // initialize AutoLoadReferencedAssemblies from persisted startup settings
        autoLoadReferencedAssemblies = startupSettings.AutoLoadReferencedAssemblies;

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

    private void AssemblyTreeModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssemblyTreeModel.SelectedNode))
        {
            OnPropertyChanged(nameof(SelectedNode));
            GenerateProjectCommand.NotifyCanExecuteChanged();
            Decompile(assemblyTreeModel.SelectedNode);
            UpdateNavigationCommands();
            return;
        }

        if (e.PropertyName == nameof(AssemblyTreeModel.CanGoBack) || e.PropertyName == nameof(AssemblyTreeModel.CanGoForward))
        {
            UpdateNavigationCommands();
        }
    }

    internal void NavigateByFullName(string fullName)
    {
        var match = assemblyTreeModel.FindByFullName(fullName);
        if (match != null)
        {
            SelectedNode = match;
            ExpandParents(match);
            return;
        }

        notificationService.ShowNotification(new Notification
        {
            Message = "Definition not found in the current tree.",
            Level = NotificationLevel.Warning
        });
    }

    public ObservableCollection<AssemblyNode> AssemblyNodes => assemblyTreeModel.AssemblyNodes;

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

    public IReadOnlyList<CommandDescriptor> SharedCommands => commandCatalog.Commands;

    public Node? SelectedNode
    {
        get => assemblyTreeModel.SelectedNode;
        set
        {
            if (assemblyTreeModel.SelectedNode != value)
            {
                assemblyTreeModel.SelectedNode = value;
                OnPropertyChanged();
                GenerateProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool autoLoadReferencedAssemblies;
    public bool AutoLoadReferencedAssemblies
    {
        get => autoLoadReferencedAssemblies;
        set
        {
            if (autoLoadReferencedAssemblies != value)
            {
                autoLoadReferencedAssemblies = value;
                roverSettingsService.StartupSettings.AutoLoadReferencedAssemblies = value;
                OnPropertyChanged();
            }
        }
    }

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
        assemblyTreeModel.RemoveAssembly(assemblyNode);
        UpdateNavigationCommands();
        PersistLastAssemblies();
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

        var node = assemblyTreeModel.ResolveNode(handle);
        if (node != null)
        {
            SelectedNode = node;
            ExpandParents(node);
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
        var node = assemblyTreeModel.ResolveNode(typeHandle);
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
        assemblyTreeModel.GoBack();
        UpdateNavigationCommands();
    }

    private bool CanGoBack() => assemblyTreeModel.CanGoBack;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void Forward()
    {
        assemblyTreeModel.GoForward();
        UpdateNavigationCommands();
    }

    private bool CanGoForward() => assemblyTreeModel.CanGoForward;

    private void UpdateNavigationCommands()
    {
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    internal AssemblyNode? FindAssemblyNodeByFilePath(string filePath)
    {
        return assemblyTreeModel.FindAssemblyNodeByPath(filePath);
    }

    internal IReadOnlyList<AssemblyNode> LoadAssemblies(IEnumerable<string> filePaths, bool loadDependencies = false)
    {
        var addedAssemblies = assemblyTreeModel.LoadAssemblies(filePaths, ShowCompilerGeneratedMembers, ShowInternalApi, loadDependencies);
        PersistLastAssemblies();
        return addedAssemblies;
    }

    [RelayCommand(CanExecute = nameof(CanClearAssemblyList))]
    private void ClearAssemblyList()
    {
        _ = analyticsService.TrackEventAsync(AnalyticsEvents.CloseAll);

        assemblyTreeModel.ClearAssemblies();
        UpdateNavigationCommands();
        PersistLastAssemblies();
    }

    private bool CanClearAssemblyList() => AssemblyNodes.Count > 0;

    [RelayCommand]
    private void SortAssemblies()
    {
        assemblyTreeModel.SortAssemblies();
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
        var openedAssemblies = assemblyTreeModel.GetAssemblyFilePaths()
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToArray();
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

        if (!assemblyTreeModel.TryGetAssembly(node, out var assembly))
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
                    // No preview available for this resource type.
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            document = new TextDocument($"// Error reading resource: {ex.Message}");
            InlineObjects = new List<InlineObjectSpec>();
            return true;
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
        var files = assemblyTreeModel.GetAssemblyFilePaths()
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
        logger.LogInformation("[NavigateToSearchResult] Called with: {Type}, DisplayName: {DisplayName}", result?.GetType().Name, (result as BasicSearchResult)?.DisplayName);
        if (result is not BasicSearchResult basic)
        {
            logger.LogWarning("[NavigateToSearchResult] Result is not BasicSearchResult or is null.");
            return;
        }
        var targetNode = navigationService.ResolveSearchResultTarget(basic);
        if (targetNode is { })
        {
            SelectedNode = targetNode;
            ExpandParents(targetNode);
            return;
        }

        logger.LogWarning("[NavigateToSearchResult] No valid target node found for search result {DisplayName}", basic.DisplayName);
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
        var ilspyAssemblies = assemblyTreeModel.GetAssemblies().Select(a => a.LoadedAssembly).ToList();
        var results = adapter.Search(ilspyAssemblies, term, SelectedSearchMode.Name, assemblyTreeModel.ResolveNode, assemblyTreeModel.ResolveAssemblyNode, assemblyTreeModel.ResolveResourceNode, includeInternal: ShowInternalApi, includeCompilerGenerated: ShowCompilerGeneratedMembers);

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
        dialogService.ShowDialog<AboutDialog>();
    }

    [RelayCommand]
    private void FindUsages()
    {
        if (SelectedNode is not MemberNode member)
        {
            notificationService.ShowNotification(new Notification
            {
                Message = "Select a member to find usages.",
                Level = NotificationLevel.Warning
            });
            return;
        }

        if (!assemblyTreeModel.TryGetAssembly(member, out var ilspyAssembly))
        {
            notificationService.ShowNotification(new Notification
            {
                Message = "Assembly not loaded for this member.",
                Level = NotificationLevel.Warning
            });
            return;
        }

        SearchResults.Clear();
        NumberOfResultsText = null;

        var refs = ilSpyBackend.AnalyzeSymbolReferences(ilspyAssembly, member.MetadataToken, SelectedLanguage.Language).ToList();
        foreach (var (handle, displayName, assemblyPath) in refs)
        {
            // Try to resolve node in current tree
            var node = assemblyTreeModel.ResolveNode(handle);

            var basic = new BasicSearchResult
            {
                MatchedString = displayName,
                DisplayName = displayName,
                DisplayLocation = node?.ToString() ?? string.Empty,
                DisplayAssembly = assemblyPath ?? string.Empty,
                IconPath = string.Empty,
                LocationIconPath = string.Empty,
                AssemblyIconPath = string.Empty,
                TargetNode = node,
                MetadataToken = handle
            };
            SearchResults.Add(basic);
            if (basic.TargetNode == null)
            {
                OfferBackgroundResolve(basic);
            }
        }

        NumberOfResultsText = $"Found {SearchResults.Count} usage(s)";
    }

    private async void OfferBackgroundResolve(BasicSearchResult basic)
    {
        if (basic == null)
            return;

        // Create a notification with an action to attempt background resolution
        // Gather candidates
        var simpleName = System.IO.Path.GetFileNameWithoutExtension(basic.DisplayAssembly);
        var candidates = assemblyTreeModel.Backend.ResolveAssemblyCandidates(simpleName, null).ToList();
        if (!candidates.Contains(basic.DisplayAssembly, System.StringComparer.OrdinalIgnoreCase))
            candidates.Add(basic.DisplayAssembly);

            if (candidates.Count > 1)
        {
            // Present chooser dialog to the user for explicit selection
            var chooserVm = new AssemblyCandidateChooserViewModel(candidates);
            var chooser = new ProjectRover.Views.AssemblyCandidateChooserDialog();
            chooser.DataContext = chooserVm;
            var owner = App.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            var picked = await chooser.ShowDialog<string?>(owner);
            if (!string.IsNullOrEmpty(picked))
            {
                var progress = new Progress<string>(s => notificationService.ShowNotification(new Notification { Message = s, Level = NotificationLevel.Information }));
                var cts = new System.Threading.CancellationTokenSource();
                var temp = new ProjectRover.SearchResults.BasicSearchResult { MatchedString = basic.MatchedString, DisplayAssembly = picked, DisplayName = basic.DisplayName, MetadataToken = basic.MetadataToken, DisplayLocation = string.Empty, IconPath = string.Empty, LocationIconPath = string.Empty, AssemblyIconPath = string.Empty };
                var resolved = await System.Threading.Tasks.Task.Run(async () => await assemblyTreeModel.TryBackgroundResolveAsync(temp, progress, cts.Token));
                if (resolved != null)
                {
                    basic.TargetNode = resolved;
                    basic.DisplayLocation = resolved.ToString();
                    notificationService.ShowNotification(new Notification { Message = $"Resolved {basic.DisplayName} in {picked}", Level = NotificationLevel.Success });
                }
                else
                {
                    notificationService.ShowNotification(new Notification { Message = $"Background resolve failed for {picked}", Level = NotificationLevel.Warning });
                }
            }
            return;
        }

        // Single candidate: either autoload (if enabled) or offer the action
        if (AutoLoadReferencedAssemblies)
        {
            var chosen = candidates.FirstOrDefault();
            var progress = new Progress<string>(s => notificationService.ShowNotification(new Notification { Message = s, Level = NotificationLevel.Information }));
            var cts = new System.Threading.CancellationTokenSource();
            var temp = new ProjectRover.SearchResults.BasicSearchResult { MatchedString = basic.MatchedString, DisplayAssembly = chosen ?? basic.DisplayAssembly, DisplayName = basic.DisplayName, MetadataToken = basic.MetadataToken, DisplayLocation = string.Empty, IconPath = string.Empty, LocationIconPath = string.Empty, AssemblyIconPath = string.Empty };
            var resolved = await System.Threading.Tasks.Task.Run(async () => await assemblyTreeModel.TryBackgroundResolveAsync(temp, progress, cts.Token));
            if (resolved != null)
            {
                basic.TargetNode = resolved;
                basic.DisplayLocation = resolved.ToString();
                notificationService.ShowNotification(new Notification { Message = $"Resolved {basic.DisplayName} in {basic.DisplayAssembly}", Level = NotificationLevel.Success });
                return;
            }
        }

        // Single candidate: proceed as before (offer action)
        var action = new NotificationAction
        {
            Title = "Attempt background resolve",
            Action = async () =>
            {
                var chosen = candidates.FirstOrDefault();
                var progress = new Progress<string>(s => notificationService.ShowNotification(new Notification { Message = s, Level = NotificationLevel.Information }));
                var cts = new System.Threading.CancellationTokenSource();
                var temp = new ProjectRover.SearchResults.BasicSearchResult { MatchedString = basic.MatchedString, DisplayAssembly = chosen ?? basic.DisplayAssembly, DisplayName = basic.DisplayName, MetadataToken = basic.MetadataToken, DisplayLocation = string.Empty, IconPath = string.Empty, LocationIconPath = string.Empty, AssemblyIconPath = string.Empty };
                var resolved = await System.Threading.Tasks.Task.Run(async () => await assemblyTreeModel.TryBackgroundResolveAsync(temp, progress, cts.Token));
                if (resolved != null)
                {
                    basic.TargetNode = resolved;
                    basic.DisplayLocation = resolved.ToString();
                    notificationService.ShowNotification(new Notification { Message = $"Resolved {basic.DisplayName} in {basic.DisplayAssembly}", Level = NotificationLevel.Success });
                }
                else
                {
                    notificationService.ShowNotification(new Notification { Message = $"Background resolve failed for {basic.DisplayAssembly}", Level = NotificationLevel.Warning });
                }
            }
        };

        var notification = new Notification { Message = $"Unresolved result in {basic.DisplayAssembly}: {basic.DisplayName}", Level = NotificationLevel.Information, Actions = new[] { action } };
        notificationService.ShowNotification(notification);
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

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

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
