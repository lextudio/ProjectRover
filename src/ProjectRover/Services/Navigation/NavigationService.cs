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

        // Attempt to load assembly asynchronously
        progress.Report($"Loading assembly: {assemblyPath}");
        try
        {
            var loaded = await assemblyTreeModel.LoadAssembliesAsync(new[] { assemblyPath }, false, false, false, progress, cancellationToken).ConfigureAwait(false);
            var loadedCandidate = assemblyTreeModel.FindAnyNodeForAssembly(assemblyPath);
            if (loadedCandidate != null)
            {
                progress.Report($"Resolved after load: {assemblyPath}");
                // Ensure selection and UI operations happen on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { /* caller will set SelectedNode */ });
                return loadedCandidate;
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
