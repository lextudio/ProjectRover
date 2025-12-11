namespace ProjectRover.Nodes;

public class ResourceEntryNode : MetadataLeafNode
{
    public string? ResourceName { get; init; }
    public string IconKey { get; init; } = "ResourceFileIcon";
}
