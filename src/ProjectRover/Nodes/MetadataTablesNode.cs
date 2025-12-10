namespace ProjectRover.Nodes;

using System.Collections.ObjectModel;

public class MetadataTablesNode : MetadataLeafNode
{
    public ObservableCollection<MetadataTableNode> Items { get; } = new();
}
