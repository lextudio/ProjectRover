using ProjectRover.SearchResults;
using ProjectRover.Nodes;

namespace ProjectRover.Services.Navigation;

public interface INavigationService
{
    /// <summary>
    /// Resolve a BasicSearchResult to a <see cref="Node"/> if possible. This may load or reindex assemblies.
    /// Returns the resolved node or null if unresolved.
    /// </summary>
    Node? ResolveSearchResultTarget(BasicSearchResult basicResult);

    /// <summary>
    /// Async variant that attempts to resolve a search result target, possibly loading assemblies and indexing in background.
    /// Returns resolved <see cref="Node"/> or null if unresolved.
    /// </summary>
    System.Threading.Tasks.Task<Node?> JumpToReferenceAsync(BasicSearchResult basicResult, System.Threading.CancellationToken cancellationToken = default);
}
