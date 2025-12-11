using System;
using System.Collections.Generic;

namespace ProjectRover.Nodes;

/// <summary>
/// A simplified, Avalonia-friendly view of a tree node that can be shared between Rover and ILSpy.
/// </summary>
public interface ISharedTreeNode
{
    string Name { get; }

    string IconKey { get; }

    IReadOnlyList<ISharedTreeNode> Children { get; }

    bool IsExpanded { get; set; }

    bool IsPublicAPI { get; }

    object? Source { get; }
}

/// <summary>
/// Adapts a Rover <see cref="Node"/> to <see cref="ISharedTreeNode"/> semantics.
/// </summary>
public sealed class NodeSharedTreeNode : ISharedTreeNode
{
    private readonly Node node;
    private readonly IReadOnlyList<ISharedTreeNode>? children;

    public NodeSharedTreeNode(Node node, IReadOnlyList<ISharedTreeNode>? children = null, string iconKey = "ClassIcon")
    {
        this.node = node;
        this.children = children;
        IconKey = iconKey;
    }

    public string Name => node.Name;

    public string IconKey { get; }

    public IReadOnlyList<ISharedTreeNode> Children => children ?? Array.Empty<ISharedTreeNode>();

    public bool IsExpanded
    {
        get => node.IsExpanded;
        set => node.IsExpanded = value;
    }

    public bool IsPublicAPI => node.IsPublicAPI;

    public object? Source => node;
}
