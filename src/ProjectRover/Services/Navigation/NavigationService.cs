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
}
