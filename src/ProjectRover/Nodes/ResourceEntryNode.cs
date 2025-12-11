using ICSharpCode.Decompiler.Metadata;
using System.Collections.ObjectModel;

namespace ProjectRover.Nodes;

public class ResourceEntryNode : MetadataLeafNode
{
    public string? ResourceName { get; init; }
    public string IconKey { get; init; } = "ResourceFileIcon";
    public Resource? Resource { get; init; }
    public object? Raw { get; init; }
    public ObservableCollection<Node> Children { get; } = new();
}
