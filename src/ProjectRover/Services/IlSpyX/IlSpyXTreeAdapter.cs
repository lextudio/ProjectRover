using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Resources;
using System.Resources.NetStandard;
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
    
public static AssemblyNode? BuildAssemblyNode(LoadedAssembly loaded, bool includeCompilerGenerated = false, bool includeInternal = false)
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

    // References
    PopulateReferences(assemblyNode, loaded);

    // Resources
    PopulateResources(assemblyNode, (PEFile)metadata);

    var namespaces = new Dictionary<string, NamespaceNode>(System.StringComparer.Ordinal);
    foreach (var typeHandle in reader.TypeDefinitions)
    {
        var typeDef = reader.GetTypeDefinition(typeHandle);
        if (!typeHandle.IsNil && typeDef.IsNested)
        {
            continue;
        }

        var fullTypeName = typeHandle.GetFullTypeName(reader);
        var resolved = compilation.FindType(fullTypeName).GetDefinition();
        if (resolved == null || (!includeCompilerGenerated && IsCompilerGenerated(resolved)))
            continue;

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

        var typeNode = BuildTypeSubtree(resolved, nsNode, includeCompilerGenerated, includeInternal);
        if (typeNode != null)
        {
            nsNode.Types.Add(typeNode);
        }
    }

    return assemblyNode;
}

private static void PopulateReferences(AssemblyNode assemblyNode, LoadedAssembly loaded)
{
    var referencesNode = assemblyNode.References;
    var assemblies = loaded.AssemblyList.GetAssemblies();
    foreach (var reference in assemblies)
    {
        // Skip self
        if (string.Equals(reference.ShortName, loaded.ShortName, System.StringComparison.OrdinalIgnoreCase))
            continue;

        referencesNode.Items.Add(new ResolvedReferenceNode
        {
            Name = reference.ShortName,
            Parent = referencesNode,
            FilePath = reference.FileName
        });
    }

    var reader = loaded.GetMetadataFileOrNull()?.Metadata;
    if (reader != null)
    {
        foreach (var handle in reader.AssemblyReferences)
        {
            var asmRef = reader.GetAssemblyReference(handle);
            var name = reader.GetString(asmRef.Name);
            if (referencesNode.Items.OfType<ResolvedReferenceNode>().Any(r => string.Equals(r.Name, name, System.StringComparison.OrdinalIgnoreCase)))
                continue;

            referencesNode.Items.Add(new UnresolvedReferenceNode
            {
                Name = name,
                Parent = referencesNode
            });
        }
    }
}

private static void PopulateResources(AssemblyNode assemblyNode, PEFile peFile)
    {
        var resourcesNode = new ResourcesNode
        {
            Name = "Resources",
            Parent = assemblyNode
    };

    foreach (var resource in peFile.Resources)
    {
        var entryNode = new ResourceEntryNode
        {
            Name = resource.Name,
            ResourceName = resource.Name,
            Parent = resourcesNode,
            Resource = resource,
            IconKey = "ResourceFileIcon",
            Raw = resource
        };
        PopulateResourceEntries(entryNode);
        resourcesNode.Items.Add(entryNode);
    }

    if (resourcesNode.Items.Count > 0)
        assemblyNode.Children.Add(resourcesNode);
}

private static void PopulateResourceEntries(ResourceEntryNode entryNode)
{
    if (entryNode.Resource == null)
        return;

    var resourceName = entryNode.Resource.Name;
    try
    {
        using var stream = entryNode.Resource.TryOpenStream();
        if (stream == null)
            return;

        if (resourceName.EndsWith(".resources", System.StringComparison.OrdinalIgnoreCase))
        {
            if (stream.CanSeek)
                stream.Position = 0;
            // IMPORTANT: match ILSpy WPF resource parsing to expand entries in the tree.
            using var resources = new ICSharpCode.Decompiler.Util.ResourcesFile(stream);
            foreach (var child in resources)
            {
                var iconKey = child.Value is string ? "StringIcon" : "ResourceFileIcon";
                var childNode = new ResourceEntryNode
                {
                    Name = child.Key,
                    ResourceName = child.Key,
                    Parent = entryNode,
                    IconKey = iconKey,
                    Raw = child.Value
                };
                AddPackageChildren(childNode, child.Value);
                entryNode.Children.Add(childNode);
            }
        }
        else if (resourceName.EndsWith(".resx", System.StringComparison.OrdinalIgnoreCase)
                 || resourceName.EndsWith(".resw", System.StringComparison.OrdinalIgnoreCase))
        {
            if (stream.CanSeek)
                stream.Position = 0;
            using var resx = new ResXResourceReader(stream);
            foreach (DictionaryEntry child in resx)
            {
                var name = child.Key?.ToString() ?? string.Empty;
                var iconKey = child.Value is string ? "StringIcon" : "ResourceFileIcon";
                entryNode.Children.Add(new ResourceEntryNode
                {
                    Name = name,
                    ResourceName = name,
                    Parent = entryNode,
                    IconKey = iconKey,
                    Raw = child.Value
                });
            }
        }
    }
    catch
    {
        // Keep the root resource node even if entries cannot be expanded.
    }
}

private static void AddPackageChildren(ResourceEntryNode parent, object? value)
{
    // IMPORTANT: follow ILSpy WPF behavior by expanding package-like resources into child nodes.
    try
    {
        if (value is Resource res)
        {
            using var stream = res.TryOpenStream();
            AddZipEntries(parent, stream);
        }
        else if (value is ZipArchiveEntry zipEntry)
        {
            using var stream = zipEntry.Open();
            AddZipEntries(parent, stream);
        }
    }
    catch
    {
        // ignore package expansion failures
    }
}

private static void AddZipEntries(ResourceEntryNode parent, Stream? stream)
{
    if (stream == null)
        return;

    Stream zipStream = stream;
    if (!zipStream.CanSeek)
    {
        // ZipArchive needs seekable stream; copy if necessary.
        var buffer = new MemoryStream();
        zipStream.CopyTo(buffer);
        buffer.Position = 0;
        zipStream = buffer;
    }

    try
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            parent.Children.Add(new ResourceEntryNode
            {
                Name = entry.FullName,
                ResourceName = entry.FullName,
                Parent = parent,
                IconKey = "ResourceFileIcon",
                Raw = entry
            });
        }
    }
    catch
    {
        // ignore malformed zip streams
    }
}

private static TypeNode? BuildTypeSubtree(ITypeDefinition resolved, Node parent, bool includeCompilerGenerated, bool includeInternal)
{
    if (!includeCompilerGenerated && IsCompilerGenerated(resolved))
        return null;
    if (!IsVisible(resolved, includeInternal))
        return null;

    TypeNode typeNode = resolved.Kind switch
    {
        TypeKind.Enum => new EnumNode
        {
            Name = resolved.Name,
            Parent = parent,
            TypeDefinition = resolved,
            IsPublicAPI = resolved.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetClassIconKey(resolved),
            Entity = resolved
        },
        TypeKind.Struct => new StructNode
        {
            Name = resolved.Name,
            Parent = parent,
            TypeDefinition = resolved,
            IsPublicAPI = resolved.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetClassIconKey(resolved),
            Entity = resolved
        },
        TypeKind.Interface => new InterfaceNode
        {
            Name = resolved.Name,
            Parent = parent,
            TypeDefinition = resolved,
            IsPublicAPI = resolved.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetClassIconKey(resolved),
            Entity = resolved
        },
        _ => new ClassNode
        {
            Name = resolved.Name,
            Parent = parent,
            TypeDefinition = resolved,
            IsPublicAPI = resolved.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetClassIconKey(resolved),
            Entity = resolved
        }
    };

    var members = new List<Node>();

    // Fields / const
    foreach (var field in resolved.Fields)
    {
        if (!includeCompilerGenerated && IsCompilerGenerated(field))
            continue;
        if (!IsVisible(field, includeInternal))
            continue;

        members.Add(new FieldNode
        {
            Name = field.Name,
            Parent = typeNode,
            FieldDefinition = field,
            Entity = field,
            IsPublicAPI = field.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetMemberIconKey(field)
        });
    }

    // Properties
    foreach (var property in resolved.Properties)
    {
        if (!includeCompilerGenerated && IsCompilerGenerated(property))
            continue;
        if (!IsVisible(property, includeInternal))
            continue;

        members.Add(new PropertyNode
        {
            Name = property.Name,
            Parent = typeNode,
            PropertyDefinition = property,
            Entity = property,
            IsPublicAPI = property.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetMemberIconKey(property)
        });
    }

    // Events
    foreach (var ev in resolved.Events)
    {
        if (!includeCompilerGenerated && IsCompilerGenerated(ev))
            continue;
        if (!IsVisible(ev, includeInternal))
            continue;

        members.Add(new EventNode
        {
            Name = ev.Name,
            Parent = typeNode,
            EventDefinition = ev,
            Entity = ev,
            IsPublicAPI = ev.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
            IconKey = GetMemberIconKey(ev)
        });
    }

    // Methods
    foreach (var method in resolved.Methods)
    {
        if (!includeCompilerGenerated && IsCompilerGenerated(method))
            continue;
        if (!IsVisible(method, includeInternal))
            continue;

        if (method.IsConstructor)
        {
            members.Add(new ConstructorNode
            {
                Name = $"{resolved.Name}()",
                Parent = typeNode,
                MethodDefinition = method,
                Entity = method,
                IsPublicAPI = method.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                IconKey = "ConstructorIcon"
            });
        }
        else
        {
            members.Add(new MethodNode
            {
                Name = method.Name,
                Parent = typeNode,
                MethodDefinition = method,
                Entity = method,
                IsPublicAPI = method.Accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                IconKey = GetMemberIconKey(method)
            });
        }
    }

    // Nested types
    foreach (var nested in resolved.NestedTypes)
    {
        var nestedDef = nested.GetDefinition();
        if (nestedDef == null)
            continue;

        var nestedNode = BuildTypeSubtree(nestedDef, typeNode, includeCompilerGenerated, includeInternal);
        if (nestedNode != null)
        {
            members.Add(nestedNode);
        }
    }

    typeNode.Members.Clear();
    foreach (var member in OrderMembers(members))
    {
        typeNode.Members.Add(member);
    }

    return typeNode;
}

private static IEnumerable<Node> OrderMembers(IEnumerable<Node> members)
{
    var nestedTypes = members.OfType<TypeNode>().OrderBy(m => m.Name, System.StringComparer.Ordinal).ToList();

    var fields = members.OfType<FieldNode>().ToList();
    var constants = fields.Where(f => f.FieldDefinition.IsConst).ToList();
    var instanceFields = fields.Where(f => !f.FieldDefinition.IsConst && !f.FieldDefinition.IsStatic).ToList();
    var staticFields = fields.Where(f => !f.FieldDefinition.IsConst && f.FieldDefinition.IsStatic).ToList();

    var properties = members.OfType<PropertyNode>().ToList();
    var instanceProperties = properties.Where(p => !p.PropertyDefinition.IsStatic).ToList();
    var staticProperties = properties.Where(p => p.PropertyDefinition.IsStatic).ToList();

    var constructors = members.OfType<ConstructorNode>().ToList();

    var methods = members.OfType<MethodNode>().ToList();
    var instanceMethods = methods.Where(m => !m.MethodDefinition.IsStatic).ToList();
    var staticMethods = methods.Where(m => m.MethodDefinition.IsStatic).ToList();

    var events = members.OfType<EventNode>().ToList();

    IEnumerable<Node> ordered = Enumerable.Empty<Node>();
    ordered = ordered.Concat(nestedTypes)
        .Concat(constants)
        .Concat(instanceFields)
        .Concat(staticFields)
        .Concat(instanceProperties)
        .Concat(staticProperties)
        .Concat(constructors)
        .Concat(instanceMethods)
        .Concat(staticMethods)
        .Concat(events);

    return ordered;
}

private static bool IsCompilerGenerated(IEntity entity)
{
    foreach (var attr in entity.GetAttributes())
    {
        if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
            return true;
    }
    return false;
}

private static bool IsVisible(IEntity entity, bool includeInternal)
{
    return entity.Accessibility switch
    {
        Accessibility.Public => true,
        Accessibility.Protected => true,
        Accessibility.ProtectedOrInternal => true,
        Accessibility.Internal => includeInternal,
        Accessibility.ProtectedAndInternal => includeInternal,
        Accessibility.Private => true, // keep private members consistent with Rover behavior
        _ => true
    };
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
        IField field when field.IsStatic => $"FieldStaticIcon{suffix}",
        IField => $"FieldIcon{suffix}",
        IProperty property when property.IsStatic => $"PropertyStaticIcon{suffix}",
        IProperty => $"PropertyIcon{suffix}",
        IEvent @event when @event.IsStatic => $"EventStaticIcon{suffix}",
        IEvent => $"EventIcon{suffix}",
        IMethod method when method.IsStatic => $"MethodStaticIcon{suffix}",
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
