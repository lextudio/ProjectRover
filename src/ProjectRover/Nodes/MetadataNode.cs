using System.Collections.ObjectModel;
using Mono.Cecil;

namespace ProjectRover.Nodes;

public class MetadataNode : Node
{
    public required AssemblyDefinition AssemblyDefinition { get; init; }
    public ObservableCollection<Node> Items { get; } = new();
}
