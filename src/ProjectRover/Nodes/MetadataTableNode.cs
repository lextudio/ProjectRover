using System.Reflection.Metadata.Ecma335;

namespace ProjectRover.Nodes;

public class MetadataTableNode : MetadataLeafNode
{
    public required TableIndex Table { get; init; }
    public required int RowCount { get; init; }
}
