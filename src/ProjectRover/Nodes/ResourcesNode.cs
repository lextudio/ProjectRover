using System.Collections.ObjectModel;

namespace ProjectRover.Nodes;

public class ResourcesNode : Node
{
    public ObservableCollection<ResourceEntryNode> Items { get; } = new();
}
