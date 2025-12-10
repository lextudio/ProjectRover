using System.Collections.ObjectModel;

namespace ProjectRover.Nodes;

public class ResourcesNode : Node
{
    public ObservableCollection<ResourceNode> Items { get; } = new();
}
