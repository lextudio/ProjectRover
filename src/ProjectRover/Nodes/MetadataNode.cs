using System.Collections.ObjectModel;
using ICSharpCode.Decompiler.Metadata;

namespace ProjectRover.Nodes;

public class MetadataNode : Node
{
    public required PEFile PeFile { get; init; }
    public ObservableCollection<Node> Items { get; } = new();
}
