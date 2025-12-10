using System.Collections.ObjectModel;

namespace ProjectRover.Nodes;

public class BaseTypesNode : Node
{
    public ObservableCollection<Node> Items { get; } = new();
}
