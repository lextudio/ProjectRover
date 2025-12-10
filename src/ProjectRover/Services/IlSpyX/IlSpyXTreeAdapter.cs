using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpyX;
using ProjectRover.Nodes;

namespace ProjectRover.Services.IlSpyX;

/// <summary>
/// Builds Rover tree nodes from ILSpyX loaded assemblies.
/// </summary>
public static class IlSpyXTreeAdapter
{
    
public static AssemblyNode? BuildAssemblyNode(LoadedAssembly loaded)
{
    var metadata = loaded.GetMetadataFileOrNull();
    var compilation = loaded.GetTypeSystemOrNull();
    if (metadata == null || compilation == null)
        return null;

    var reader = metadata.Metadata;
    var assemblyNode = new AssemblyNode
    {
        Name = loaded.ShortName,
        Parent = null,
        IsPublicAPI = true
    };

    // Metadata nodes
    var metadataNode = new MetadataNode
    {
        Name = "Metadata",
        Parent = assemblyNode,
        PeFile = (PEFile)metadata
    };
    assemblyNode.Children.Add(metadataNode);

    var debugMetadataNode = new DebugMetadataNode
    {
        Name = "Debug Metadata",
        Parent = assemblyNode,
        PeFile = (PEFile)metadata
    };
    assemblyNode.Children.Add(debugMetadataNode);

    // References placeholder (ILSpyX resolver handles resolution)
    assemblyNode.Children.Add(assemblyNode.References);

    var namespaces = new Dictionary<string, NamespaceNode>(System.StringComparer.Ordinal);
    foreach (var typeHandle in reader.TypeDefinitions)
    {
        var typeDef = reader.GetTypeDefinition(typeHandle);
        if (!typeHandle.IsNil && typeDef.IsNested)
        {
            // Nested types are added under parent below
            continue;
        }
        AddType(compilation, assemblyNode, namespaces, typeHandle, reader);
    }

    return assemblyNode;
}

private static void AddType(ICompilation compilation, AssemblyNode assemblyNode, Dictionary<string, NamespaceNode> namespaces, TypeDefinitionHandle handle, MetadataReader reader)
{
    var typeDef = reader.GetTypeDefinition(handle);
    var fullTypeName = handle.GetFullTypeName(reader);
    var resolved = compilation.FindType(fullTypeName).GetDefinition();
    if (resolved == null)
        return;

    var nsName = resolved.Namespace;
    if (!namespaces.TryGetValue(nsName, out var nsNode))
    {
        nsNode = new NamespaceNode
        {
            Name = string.IsNullOrEmpty(nsName) ? "-" : nsName,
            Parent = assemblyNode,
            IsPublicAPI = true
        };
        namespaces[nsName] = nsNode;
        assemblyNode.AddNamespace(nsNode);
    }

    var typeNode = new ClassNode
    {
        Name = resolved.Name,
        Parent = nsNode,
        TypeDefinition = resolved,
        IsPublicAPI = resolved.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
        IconKey = GetClassIconKey(resolved),
        Entity = resolved
    };
    nsNode.Types.Add(typeNode);

    // Members
    foreach (var member in resolved.Members)
    {
        switch (member)
        {
            case IField field:
                typeNode.Members.Add(new FieldNode
                {
                    Name = field.Name,
                    Parent = typeNode,
                    FieldDefinition = field,
                    Entity = field,
                    IsPublicAPI = field.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                    IconKey = GetMemberIconKey(field)
                });
                break;
            case IProperty property:
                typeNode.Members.Add(new PropertyNode
                {
                    Name = property.Name,
                    Parent = typeNode,
                    PropertyDefinition = property,
                    Entity = property,
                    IsPublicAPI = property.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                    IconKey = GetMemberIconKey(property)
                });
                break;
            case IEvent ev:
                typeNode.Members.Add(new EventNode
                {
                    Name = ev.Name,
                    Parent = typeNode,
                    EventDefinition = ev,
                    Entity = ev,
                    IsPublicAPI = ev.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                    IconKey = GetMemberIconKey(ev)
                });
                break;
            case IMethod method when method.IsConstructor:
                typeNode.Members.Add(new ConstructorNode
                {
                    Name = $"{resolved.Name}()",
                    Parent = typeNode,
                    MethodDefinition = method,
                    Entity = method,
                    IsPublicAPI = method.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                    IconKey = "ConstructorIcon"
                });
                break;
            case IMethod method:
                typeNode.Members.Add(new MethodNode
                {
                    Name = method.Name,
                    Parent = typeNode,
                    MethodDefinition = method,
                    Entity = method,
                    IsPublicAPI = method.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                    IconKey = GetMemberIconKey(method)
                });
                break;
        }
    }

    // Nested types
    foreach (var nestedHandle in typeDef.GetNestedTypes())
    {
        AddType(compilation, assemblyNode, namespaces, nestedHandle, reader);
    }
}

private static string GetMemberIconKey(IEntity entity)
{
    if (entity is IMethod { IsConstructor: true })
        return "ConstructorIcon";

    var suffix = entity.Accessibility switch
    {
        Accessibility.Public => "Public",
        Accessibility.Protected or Accessibility.ProtectedOrInternal => "Protected",
        Accessibility.Internal => "Internal",
        Accessibility.Private or Accessibility.ProtectedAndInternal => "Private",
        _ => "Public"
    };

    return entity switch
    {
        IField field when field.IsConst => $"ConstantIcon{suffix}",
        IField => $"FieldIcon{suffix}",
        IProperty => $"PropertyIcon{suffix}",
        IEvent => $"EventIcon{suffix}",
        IMethod => $"MethodIcon{suffix}",
        _ => "MethodIcon"
    };
}
    private static string GetClassIconKey(ITypeDefinition typeDefinition)
    {
        if (typeDefinition.IsSealed && (typeDefinition.Accessibility == Accessibility.Public || typeDefinition.Accessibility == Accessibility.ProtectedOrInternal))
            return "ClassSealedIcon";

        return typeDefinition.Accessibility switch
        {
            Accessibility.Public or Accessibility.ProtectedOrInternal => "ClassIcon",
            Accessibility.Protected => "ClassProtectedIcon",
            Accessibility.Internal => "ClassInternalIcon",
            _ => "ClassPrivateIcon"
        };
    }
}

