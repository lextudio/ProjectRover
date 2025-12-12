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
}
