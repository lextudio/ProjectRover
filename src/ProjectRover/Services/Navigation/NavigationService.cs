using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProjectRover.SearchResults;
using ProjectRover.ViewModels;
using ProjectRover.Nodes;

namespace ProjectRover.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly AssemblyTreeModel assemblyTreeModel;
    private readonly ILogger<NavigationService> logger;

    public NavigationService(AssemblyTreeModel assemblyTreeModel, ILogger<NavigationService> logger)
    {
        this.assemblyTreeModel = assemblyTreeModel;
        this.logger = logger;
    }

    public Node? ResolveSearchResultTarget(BasicSearchResult basicResult)
    {
        if (basicResult.TargetNode is { } target)
            return target;

        var assemblyPath = basicResult.DisplayAssembly;
        if (string.IsNullOrWhiteSpace(assemblyPath))
            return null;

        var existing = assemblyTreeModel.FindAssemblyNodeByPath(assemblyPath);
        if (existing != null)
        {
            assemblyTreeModel.ReindexAssembly(existing);
            var candidate = assemblyTreeModel.FindAnyNodeForAssembly(assemblyPath);
            if (candidate != null)
                return candidate;
        }

        var added = assemblyTreeModel.LoadAssemblies(new[] { assemblyPath }, false, false);
        var loadedCandidate = assemblyTreeModel.FindAnyNodeForAssembly(assemblyPath);
        return loadedCandidate;
    }

    public async System.Threading.Tasks.Task<Node?> JumpToReferenceAsync(BasicSearchResult basicResult, System.Threading.CancellationToken cancellationToken = default)
    {
        IProgress<string>? progress = new Progress<string>(s => logger.LogInformation("[JumpToReferenceAsync] {Message}", s));

        // If target node already exists on the result, select it immediately
        if (basicResult.TargetNode is { } immediate)
        {
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { /* selection handled by caller */ });
            }
            catch
            {
                // ignore
            }
            return immediate;
        }

        var assemblyPath = basicResult.DisplayAssembly;
        if (string.IsNullOrWhiteSpace(assemblyPath))
            return null;

        progress.Report($"Resolving assembly path: {assemblyPath}");

        // Try existing assembly node first
        var existing = assemblyTreeModel.FindAssemblyNodeByPath(assemblyPath);
        if (existing != null)
        {
            progress.Report($"Reindexing existing assembly: {assemblyPath}");
            // Reindex on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => assemblyTreeModel.ReindexAssembly(existing));

            var candidate = assemblyTreeModel.FindAnyNodeForAssembly(assemblyPath);
            if (candidate != null)
            {
                progress.Report($"Resolved in existing assembly: {assemblyPath}");
                return candidate;
            }
        }

        // Attempt to probe candidate assemblies before loading
        progress.Report($"Probing candidate assemblies for: {assemblyPath}");
        try
        {
            var token = basicResult.MetadataToken;
            // Derive simple name from assembly path
            var simpleName = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);
            var candidates = assemblyTreeModel.Backend.ResolveAssemblyCandidates(simpleName, null).ToList();

            // If search result provided an explicit assembly path (often analyzers return full path), ensure it's included
            if (!string.IsNullOrWhiteSpace(assemblyPath) && !candidates.Contains(assemblyPath))
                candidates.Add(assemblyPath);

            // If no candidates found, fallback to attempting to load the provided path
            if (candidates.Count == 0)
                candidates.Add(assemblyPath);

            string? toLoad = null;
            if (token.HasValue && token.Value.IsNil == false)
            {
                // Attempt resolver which may do MVID/symbol probing
                try
                {
                    var resolved = await assemblyTreeModel.Backend.ResolveAssemblyForHandleAsync(token.Value, simpleName, basicResult.DisplayLocation, null, progress, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        toLoad = resolved;
                    }
                }
                catch { }

                // If resolver didn't return a path, fall back to probing candidates
                if (toLoad == null)
                {
                    foreach (var c in candidates)
                    {
                        if (await assemblyTreeModel.Backend.ProbeAssemblyForHandleAsync(c, token.Value, progress, cancellationToken).ConfigureAwait(false))
                        {
                            toLoad = c;
                            break;
                        }
                    }
                }
            }

            // If we didn't find by probing, pick first candidate (best-effort)
            if (toLoad == null)
                toLoad = candidates.FirstOrDefault();

            if (toLoad != null)
            {
                progress.Report($"Loading assembly: {toLoad}");
                var loaded = await assemblyTreeModel.LoadAssembliesAsync(new[] { toLoad }, false, false, false, progress, cancellationToken).ConfigureAwait(false);
                var loadedCandidate = assemblyTreeModel.FindAnyNodeForAssembly(toLoad);
                if (loadedCandidate != null)
                {
                    progress.Report($"Resolved after load: {toLoad}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { /* caller will set SelectedNode */ });
                    return loadedCandidate;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[JumpToReferenceAsync] Operation canceled for {Path}", assemblyPath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[JumpToReferenceAsync] Failed to load or resolve assembly {Path}", assemblyPath);
            return null;
        }

        progress.Report($"Could not resolve assembly: {assemblyPath}");
        return null;
    }
}
