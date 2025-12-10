using Mono.Cecil;

namespace ProjectRover.Nodes;

public class BaseTypeNode : Node
{
    public required TypeReference TypeReference { get; init; }
    public string IconKey => TypeReference.Resolve()?.IsInterface == true ? "InterfaceIcon" : "ClassIcon";
}
