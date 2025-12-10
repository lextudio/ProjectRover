using ICSharpCode.Decompiler.TypeSystem;

namespace ProjectRover.Nodes;

public class BaseTypeNode : Node
{
    public required IType Type { get; init; }

    public string IconKey => Type.Kind == TypeKind.Interface ? "InterfaceIcon" : "ClassIcon";
}
