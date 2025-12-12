using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ICSharpCode.ILSpy;
using ICSharpCode.Decompiler.TypeSystem;
using ProjectRover.Nodes;
using ProjectRover.Notifications;
using ProjectRover.Services;
using ProjectRover.Services.IlSpyX;
using System.Diagnostics.CodeAnalysis;

namespace ProjectRover.ViewModels;

public partial class AssemblyTreeModel : ObservableObject
{
    private readonly IlSpyBackend ilSpyBackend;
    private readonly INotificationService notificationService;
    private readonly ILogger<AssemblyTreeModel> logger;
    private readonly IPlatformService platformService;
    private readonly Dictionary<AssemblyNode, IlSpyAssembly> assemblyLookup;
    private readonly Dictionary<(string AssemblyPath, EntityHandle Handle), Node> handleToNodeMap;
    private readonly Stack<Node> backStack;
    private readonly Stack<Node> forwardStack;
    private bool isBackForwardNavigation;

    public AssemblyTreeModel(IlSpyBackend backend, INotificationService notificationService, ILogger<AssemblyTreeModel> logger, IPlatformService platformService)
    {
        ilSpyBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
        AssemblyNodes = new ObservableCollection<AssemblyNode>();
        assemblyLookup = new Dictionary<AssemblyNode, IlSpyAssembly>();
        handleToNodeMap = new Dictionary<(string AssemblyPath, EntityHandle Handle), Node>();
        backStack = new Stack<Node>();
        forwardStack = new Stack<Node>();
    }

    public ObservableCollection<AssemblyNode> AssemblyNodes { get; }

    public IlSpyBackend Backend => ilSpyBackend;

    [ObservableProperty]
    private Node? selectedNode;

    public bool CanGoBack => backStack.Any();

    public bool CanGoForward => forwardStack.Any();

    partial void OnSelectedNodeChanged(Node? oldValue, Node? newValue)
    {
        if (!isBackForwardNavigation && oldValue != null)
        {
            backStack.Push(oldValue);
            forwardStack.Clear();
        }

        isBackForwardNavigation = false;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public void GoBack()
    {
        if (!CanGoBack || SelectedNode is null)
            return;

        isBackForwardNavigation = true;
        forwardStack.Push(SelectedNode);
        SelectedNode = backStack.Pop();
    }

    public void GoForward()
    {
        if (!CanGoForward || SelectedNode is null)
            return;

        isBackForwardNavigation = true;
        backStack.Push(SelectedNode);
        SelectedNode = forwardStack.Pop();
    }

    public void ClearHistory()
    {
        backStack.Clear();
        forwardStack.Clear();
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public IlSpyAssembly? GetIlSpyAssembly(AssemblyNode node) =>
        assemblyLookup.TryGetValue(node, out var asm) ? asm : null;

    public bool TryGetAssembly(Node node, [NotNullWhen(true)] out IlSpyAssembly? assembly)
    {
        var asmNode = GetAssemblyNode(node);
        if (asmNode != null && assemblyLookup.TryGetValue(asmNode, out assembly))
        {
            return true;
        }

        assembly = null;
        return false;
    }

    public AssemblyNode? FindAssemblyNodeByPath(string path)
    {
        return AssemblyNodes.FirstOrDefault(a =>
            string.Equals(assemblyLookup.TryGetValue(a, out var p) ? p.FilePath : string.Empty, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<AssemblyNode> LoadAssemblies(IEnumerable<string> filePaths, bool includeCompilerGenerated, bool includeInternal, bool loadDependencies = false)
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

            if (assemblyLookup.Values.Any(existing => string.Equals(existing.FilePath, assembly.FilePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            var assemblyNode = IlSpyXTreeAdapter.BuildAssemblyNode(assembly.LoadedAssembly, includeCompilerGenerated, includeInternal);
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

            if (loadDependencies)
            {
                // Dependency loading can be triggered explicitly when needed.
            }
        }

        return addedAssemblies;
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<AssemblyNode>> LoadAssembliesAsync(IEnumerable<string> filePaths, bool includeCompilerGenerated, bool includeInternal, bool loadDependencies = false, IProgress<string>? progress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        var addedAssemblies = new List<AssemblyNode>();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!processed.Add(filePath))
                continue;

            progress?.Report($"Loading {filePath}...");

                if (!File.Exists(filePath))
                {
                    await platformService.InvokeOnUIAsync(() =>
                    {
                        notificationService.ShowNotification(new Notification
                        {
                            Message = $"The file \"{filePath}\" does not exist.",
                            Level = NotificationLevel.Error
                        });

                        return System.Threading.Tasks.Task.CompletedTask;
                    });
                    continue;
                }

            // Load PE/metadata off the UI thread
            IlSpyAssembly? assembly = null;
            try
            {
                assembly = await System.Threading.Tasks.Task.Run(() => ilSpyBackend.LoadAssembly(filePath), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
                catch (Exception ex)
                {
                    await platformService.InvokeOnUIAsync(() =>
                    {
                        notificationService.ShowNotification(new Notification
                        {
                            Message = $"Failed to load \"{filePath}\": {ex.Message}",
                            Level = NotificationLevel.Error
                        });

                        return System.Threading.Tasks.Task.CompletedTask;
                    });
                    continue;
                }

            if (assembly == null)
            {
                await platformService.InvokeOnUIAsync(() =>
                {
                    notificationService.ShowNotification(new Notification
                    {
                        Message = $"Failed to load \"{filePath}\".",
                        Level = NotificationLevel.Error
                    });

                    return System.Threading.Tasks.Task.CompletedTask;
                });
                continue;
            }

            // If already present, skip
            if (assemblyLookup.Values.Any(existing => string.Equals(existing.FilePath, assembly.FilePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Build UI nodes and index on UI thread to avoid threading issues with Node objects
            AssemblyNode? assemblyNode = null;
            try
            {
                await platformService.InvokeOnUIAsync(() =>
                {
                    assemblyNode = IlSpyXTreeAdapter.BuildAssemblyNode(assembly.LoadedAssembly, includeCompilerGenerated, includeInternal);
                    if (assemblyNode == null)
                    {
                        notificationService.ShowNotification(new Notification
                        {
                            Message = $"Failed to build tree for \"{filePath}\".",
                            Level = NotificationLevel.Error
                        });
                        return System.Threading.Tasks.Task.CompletedTask;
                    }

                    IndexAssemblyHandles(assemblyNode, assembly.FilePath);
                    AssemblyNodes.Add(assemblyNode);
                    assemblyLookup[assemblyNode] = assembly;
                    addedAssemblies.Add(assemblyNode);
                    SelectedNode = assemblyNode;

                    return System.Threading.Tasks.Task.CompletedTask;
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await platformService.InvokeOnUIAsync(() =>
                {
                    notificationService.ShowNotification(new Notification
                    {
                        Message = $"Failed to build tree for \"{filePath}\": {ex.Message}",
                        Level = NotificationLevel.Error
                    });

                    return System.Threading.Tasks.Task.CompletedTask;
                });
                continue;
            }

            progress?.Report($"Loaded {filePath}");
        }

        return addedAssemblies;
    }

    public void RemoveAssembly(AssemblyNode assemblyNode)
    {
        if (!AssemblyNodes.Remove(assemblyNode))
            return;

        var assemblyPath = assemblyLookup.TryGetValue(assemblyNode, out var ilSpyAssembly) ? ilSpyAssembly.FilePath : string.Empty;
        if (ilSpyAssembly != null)
        {
            ilSpyBackend.UnloadAssembly(ilSpyAssembly);
            assemblyLookup.Remove(assemblyNode);
        }

        var toRemove = handleToNodeMap
            .Where(kvp => GetAssemblyNode(kvp.Value) == assemblyNode
                          || (!string.IsNullOrEmpty(assemblyPath) && string.Equals(kvp.Key.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase)))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in toRemove)
        {
            handleToNodeMap.Remove(key);
        }

        FilterStack(backStack);
        FilterStack(forwardStack);
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));

        if (SelectedNode is null || GetAssemblyNode(SelectedNode) == assemblyNode)
        {
            isBackForwardNavigation = true;
            SelectedNode = AssemblyNodes.FirstOrDefault();
        }

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

    public void ClearAssemblies()
    {
        isBackForwardNavigation = true;
        SelectedNode = null;
        AssemblyNodes.Clear();
        handleToNodeMap.Clear();
        assemblyLookup.Clear();
        ClearHistory();
        ilSpyBackend.Clear();
    }

    public void SortAssemblies()
    {
        var sorted = AssemblyNodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
        AssemblyNodes.Clear();
        foreach (var n in sorted)
            AssemblyNodes.Add(n);
    }

    public IReadOnlyCollection<IlSpyAssembly> GetAssemblies() => assemblyLookup.Values.ToList();

    public IReadOnlyList<string?> GetAssemblyFilePaths() =>
        assemblyLookup.Values.Select(a => a.FilePath).ToList();

    public Node? FindByFullName(string fullName) =>
        handleToNodeMap.Values.OfType<MemberNode>()
            .FirstOrDefault(member => string.Equals(member.Entity.FullName, fullName, StringComparison.Ordinal));

    public void ReindexAssembly(AssemblyNode node)
    {
        if (assemblyLookup.TryGetValue(node, out var asm))
        {
            IndexAssemblyHandles(node, asm.FilePath);
        }
    }

    public Node? ResolveNode(EntityHandle handle)
    {
        try
        {
            logger.LogInformation("[ResolveNode] Called with handle: Kind={HandleKind}, IsNil={IsNil}, Raw={Handle}", handle.Kind, handle.IsNil, handle);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ResolveNode] Failed to log handle details");
        }

        if (handle.IsNil)
            return null;

        var node = TryResolveHandle(handle);

        if (node == null)
        {
            foreach (var kvp in assemblyLookup)
            {
                var asm = kvp.Value;
                try
                {
                    if (asm.TypeSystem.MainModule is not MetadataModule module)
                        continue;

                    IEntity? def = handle.Kind switch
                    {
                        HandleKind.TypeDefinition => module.GetDefinition((TypeDefinitionHandle)handle),
                        HandleKind.MethodDefinition => module.GetDefinition((MethodDefinitionHandle)handle),
                        HandleKind.FieldDefinition => module.GetDefinition((FieldDefinitionHandle)handle),
                        HandleKind.PropertyDefinition => module.GetDefinition((PropertyDefinitionHandle)handle),
                        HandleKind.EventDefinition => module.GetDefinition((EventDefinitionHandle)handle),
                        _ => null
                    };

                    if (def != null)
                    {
                        var path = asm.FilePath ?? string.Empty;
                        if (handleToNodeMap.TryGetValue((path, handle), out var mapped))
                        {
                            node = mapped;
                            break;
                        }

                        var any = handleToNodeMap.FirstOrDefault(h => h.Key.Handle.Equals(handle)).Value;
                        if (any != null)
                        {
                            node = any;
                            break;
                        }
                    }
                }
                catch
                {
                    // ignore probe failures
                }
            }
        }

        if (node == null)
        {
            logger.LogInformation("[ResolveNode] Could not resolve handle: {Handle}", handle);
        }
        else
        {
            try
            {
                logger.LogInformation("[ResolveNode] Resolved to node: {NodeType}, Name: {NodeName}", node.GetType().Name, node.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[ResolveNode] Resolved node found but failed to log details");
            }
        }

        return node;
    }

    public Node? ResolveResourceNode(string assemblyName, string resourceName)
    {
        logger.LogInformation("[ResolveResourceNode] Searching for resource '{ResourceName}' in assembly '{AssemblyName}'", resourceName, assemblyName);
        var assemblyNode = AssemblyNodes.FirstOrDefault(a => string.Equals(a.Name, assemblyName, StringComparison.OrdinalIgnoreCase));
        if (assemblyNode == null)
        {
            logger.LogWarning("[ResolveResourceNode] Assembly not found: {AssemblyName}", assemblyName);
            return null;
        }

        var resourcesNode = assemblyNode.Children.OfType<ResourcesNode>().FirstOrDefault();
        if (resourcesNode == null)
        {
            logger.LogWarning("[ResolveResourceNode] ResourcesNode not found in assembly: {AssemblyName}", assemblyName);
            return null;
        }

        foreach (var resourceNode in resourcesNode.Items)
        {
            var candidate = FindResourceEntry(resourceNode, resourceName);
            if (candidate != null)
            {
                logger.LogInformation("[ResolveResourceNode] Found resource node: {ResourceNodeName}", candidate.Name);
                return candidate;
            }
        }

        logger.LogWarning("[ResolveResourceNode] Resource not found: {ResourceName}", resourceName);
        return null;
    }

    public Node? ResolveAssemblyNode(string assemblyName) =>
        AssemblyNodes.FirstOrDefault(a => string.Equals(a.Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    public Node? FindAnyNodeForAssembly(string assemblyPath)
    {
        var match = handleToNodeMap.FirstOrDefault(kvp =>
            string.Equals(kvp.Key.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(kvp.Key.AssemblyPath), Path.GetFileName(assemblyPath), StringComparison.OrdinalIgnoreCase)).Value;
        return match;
    }

    public async System.Threading.Tasks.Task<Node?> TryBackgroundResolveAsync(ProjectRover.SearchResults.BasicSearchResult basicResult, IProgress<string>? progress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        if (basicResult == null)
            return null;

        var assemblyPath = basicResult.DisplayAssembly;
        if (string.IsNullOrWhiteSpace(assemblyPath))
            return null;

        var simpleName = Path.GetFileNameWithoutExtension(assemblyPath);
        progress?.Report($"Probing candidates for {simpleName}...");

        // First try backend-assisted resolution using available metadata token or symbol hints
        var token = basicResult.MetadataToken;
        string? resolvedPath = null;
        try
        {
            progress?.Report("Attempting resolver-based assembly discovery...");
            var hintName = simpleName;
            var symbolName = basicResult.DisplayLocation; // can be null or a type/member full name
            // Prefer using the token if available, otherwise try symbol name
            resolvedPath = await Backend.ResolveAssemblyForHandleAsync(token ?? default, hintName, symbolName, null, progress, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // ignore resolver errors and fallback to candidate probing
        }

        var chosen = resolvedPath;

        if (string.IsNullOrEmpty(chosen))
        {
            // Collect candidates from backend
            var candidates = Backend.ResolveAssemblyCandidates(simpleName, null).ToList();
            if (!candidates.Contains(assemblyPath, StringComparer.OrdinalIgnoreCase))
                candidates.Add(assemblyPath);

            // If metadata token exists, try to prefer matches
            if (token.HasValue && !token.Value.IsNil)
            {
                foreach (var c in candidates)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return null;

                    progress?.Report($"Probing {c} for token...");
                    try
                    {
                        if (await Backend.ProbeAssemblyForHandleAsync(c, token.Value, progress, cancellationToken).ConfigureAwait(false))
                        {
                            chosen = c;
                            break;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            if (chosen == null)
                chosen = candidates.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(chosen))
            return null;

        progress?.Report($"Loading chosen candidate: {chosen}");
        try
        {
            var loaded = await LoadAssembliesAsync(new[] { chosen }, false, false, false, progress, cancellationToken).ConfigureAwait(false);
            var resolved = FindAnyNodeForAssembly(chosen);
            if (resolved != null)
                return resolved;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            // ignore load failures
        }

        return null;
    }

    public static AssemblyNode? GetAssemblyNode(Node node)
    {
        var current = node;
        while (current.Parent != null)
        {
            current = current.Parent;
        }

        return current as AssemblyNode;
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

    private Node? TryResolveHandle(EntityHandle handle)
    {
        var exact = handleToNodeMap.FirstOrDefault(kvp => kvp.Key.Handle.Equals(handle)).Value;
        if (exact != null)
            return exact;

        try
        {
            var kind = handle.Kind;
            var handleStr = handle.ToString();
            foreach (var kvp in handleToNodeMap)
            {
                try
                {
                    if (kvp.Key.Handle.Kind == kind && string.Equals(kvp.Key.Handle.ToString(), handleStr, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation("[TryResolveHandle] Fallback matched handle string {HandleStr} kind={Kind} in assembly {Asm}", handleStr, kind, kvp.Key.AssemblyPath);
                        return kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[TryResolveHandle] Error inspecting handle in map for assembly {Asm}", kvp.Key.AssemblyPath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TryResolveHandle] Failed to get token from handle {Handle}", handle);
        }

        return null;
    }
}
