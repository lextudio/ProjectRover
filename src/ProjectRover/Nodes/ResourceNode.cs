using Mono.Cecil;

namespace ProjectRover.Nodes;

public class ResourceNode : Node
{
    public required Resource Resource { get; init; }
}
