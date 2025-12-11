using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.Abstractions;
using ICSharpCode.ILSpyX.Search;
using ProjectRover.Nodes;
using ProjectRover.SearchResults;

namespace ProjectRover.Services.IlSpyX;

/// <summary>
/// Adapts ILSpyX search strategies to Rover search results.
/// </summary>
public class IlSpyXSearchAdapter
{
    private readonly ILanguage language = new BasicLanguage();
    private readonly DecompilerSettings decompilerSettings = new();

public IList<BasicSearchResult> Search(IEnumerable<LoadedAssembly> assemblies, string term, string modeName, Func<EntityHandle, Node?>? resolveNode = null, Func<string, Node?>? resolveAssembly = null, Func<string, string, Node?>? resolveResource = null, bool includeInternal = false, bool includeCompilerGenerated = true)
{
        var trimmed = term?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Array.Empty<BasicSearchResult>();

        var searchMode = MapMode(modeName);
        var memberKind = MapMemberKind(searchMode);
        var constantOnly = string.Equals(modeName, "Constant", StringComparison.OrdinalIgnoreCase);
        var apiVisibility = includeInternal ? ApiVisibility.PublicAndInternal : ApiVisibility.PublicOnly;

        var request = new SearchRequest
        {
            DecompilerSettings = decompilerSettings,
            TreeNodeFactory = new DummyTreeNodeFactory(),
            SearchResultFactory = new RoverSearchResultFactory(),
            Mode = searchMode,
            AssemblySearchKind = AssemblySearchKind.NameOrFileName,
            MemberSearchKind = memberKind,
            Keywords = new[] { trimmed },
            RegEx = (Regex?)null,
            FullNameSearch = false,
            OmitGenerics = false,
            InNamespace = string.Empty,
            InAssembly = string.Empty
        };

        var results = new ConcurrentBag<ICSharpCode.ILSpyX.Search.SearchResult>();
        var strategies = CreateStrategies(request, results, apiVisibility, memberKind);
        var tokenSource = new CancellationTokenSource();

        foreach (var asm in assemblies)
        {
            var module = asm.GetMetadataFileOrNull();
            if (module == null)
                continue;

            foreach (var strategy in strategies)
            {
                strategy.Search(module, tokenSource.Token);
            }
        }

        return results
            .Where(r => FilterByMode(r, searchMode, constantOnly, includeCompilerGenerated))
            .Select(r => MapToBasicResult(r, resolveNode, resolveAssembly, resolveResource))
            .ToList();
}

    private IEnumerable<AbstractSearchStrategy> CreateStrategies(SearchRequest request, IProducerConsumerCollection<ICSharpCode.ILSpyX.Search.SearchResult> results, ApiVisibility apiVisibility, MemberSearchKind memberKind)
    {
        if (request.Mode is SearchMode.Type or SearchMode.Member or SearchMode.TypeAndMember or SearchMode.Method or SearchMode.Field or SearchMode.Property or SearchMode.Event)
        {
            yield return new MemberSearchStrategy(language, apiVisibility, request, results, memberKind);
        }

        if (request.Mode == SearchMode.Namespace)
        {
            yield return new NamespaceSearchStrategy(request, results);
        }

        if (request.Mode == SearchMode.Assembly)
        {
            yield return new AssemblySearchStrategy(request, results, AssemblySearchKind.NameOrFileName);
        }

        if (request.Mode == SearchMode.Resource)
        {
            yield return new ResourceSearchStrategy(apiVisibility, request, results);
        }

        if (request.Mode == SearchMode.Literal)
        {
            yield return new LiteralSearchStrategy(language, apiVisibility, request, results);
        }

        if (request.Mode == SearchMode.Token)
        {
            yield return new MetadataTokenSearchStrategy(language, apiVisibility, request, results);
        }
    }

    private static bool FilterByMode(ICSharpCode.ILSpyX.Search.SearchResult result, SearchMode mode, bool constantOnly, bool includeCompilerGenerated)
    {
        if (result is MemberSearchResult m)
        {
            if (!includeCompilerGenerated && IsCompilerGenerated(m.Member))
                return false;

            return mode switch
            {
                SearchMode.Method => m.Member is IMethod,
                SearchMode.Field => m.Member is IField f && (!constantOnly || f.IsConst),
                SearchMode.Property => m.Member is IProperty,
                SearchMode.Event => m.Member is IEvent,
                SearchMode.Type => m.Member is ITypeDefinition,
                SearchMode.Member or SearchMode.TypeAndMember => true,
                _ => true
            };
        }

        return mode switch
        {
            SearchMode.Namespace => result is NamespaceSearchResult,
            SearchMode.Assembly => result is AssemblySearchResult,
            SearchMode.Resource => result is ResourceSearchResult,
            SearchMode.Literal => true,
            SearchMode.Token => true,
            _ => true
        };
    }

    private static BasicSearchResult MapToBasicResult(ICSharpCode.ILSpyX.Search.SearchResult r, Func<EntityHandle, Node?>? resolveNode, Func<string, Node?>? resolveAssembly, Func<string, string, Node?>? resolveResource)
    {
        string name = r.Name;
        string location = r.Location;
        string assembly = r.Assembly;

        string iconKey = string.Empty;
        string locationIconKey = string.Empty;
        string assemblyIconKey = string.IsNullOrEmpty(assembly) ? string.Empty : "AssemblyIcon";
        Node? targetNode = null;

        if (r is MemberSearchResult m)
        {
            targetNode = resolveNode?.Invoke(m.Member.MetadataToken);
            assembly = m.Member.ParentModule?.AssemblyName ?? assembly;

            switch (m.Member)
            {
                case ITypeDefinition type:
                    name = type.Name;
                    location = string.IsNullOrEmpty(type.Namespace) ? "-" : type.Namespace;
                    iconKey = GetTypeIconKey(type);
                    locationIconKey = "NamespaceIcon";
                    break;
                case IMethod { IsConstructor: true } ctor:
                    name = $"{ctor.DeclaringType?.Name ?? name}()";
                    location = ctor.DeclaringType?.FullName ?? location;
                    iconKey = GetMemberIconKey(ctor);
                    locationIconKey = GetTypeIconKey(ctor.DeclaringType?.GetDefinition());
                    break;
                case IMethod method:
                    name = $"{method.DeclaringType?.Name ?? string.Empty}.{method.Name}".TrimStart('.');
                    location = method.DeclaringType?.FullName ?? location;
                    iconKey = GetMemberIconKey(method);
                    locationIconKey = GetTypeIconKey(method.DeclaringType?.GetDefinition());
                    break;
                case IField field:
                    name = $"{field.DeclaringType?.Name ?? string.Empty}.{field.Name}".TrimStart('.');
                    location = field.DeclaringType?.FullName ?? location;
                    iconKey = GetMemberIconKey(field);
                    locationIconKey = GetTypeIconKey(field.DeclaringType?.GetDefinition());
                    break;
                case IProperty property:
                    name = $"{property.DeclaringType?.Name ?? string.Empty}.{property.Name}".TrimStart('.');
                    location = property.DeclaringType?.FullName ?? location;
                    iconKey = GetMemberIconKey(property);
                    locationIconKey = GetTypeIconKey(property.DeclaringType?.GetDefinition());
                    break;
                case IEvent @event:
                    name = $"{@event.DeclaringType?.Name ?? string.Empty}.{@event.Name}".TrimStart('.');
                    location = @event.DeclaringType?.FullName ?? location;
                    iconKey = GetMemberIconKey(@event);
                    locationIconKey = GetTypeIconKey(@event.DeclaringType?.GetDefinition());
                    break;
                default:
                    iconKey = GetMemberIconKey(m.Member);
                    break;
            }
        }
        else if (r is NamespaceSearchResult)
        {
            iconKey = "NamespaceIcon";
            locationIconKey = "AssemblyIcon";
        }
        else if (r is AssemblySearchResult assemblyResult)
        {
            iconKey = "AssemblyIcon";
            assemblyIconKey = "AssemblyIcon";
            targetNode = resolveAssembly?.Invoke(assemblyResult.Assembly);
        }
        else if (r is ResourceSearchResult resourceResult)
        {
            iconKey = "ResourceFileIcon";
            targetNode = resolveResource?.Invoke(resourceResult.Assembly, resourceResult.Name);
        }

        return new BasicSearchResult
        {
            MatchedString = r.Name,
            DisplayName = name,
            DisplayLocation = location,
            DisplayAssembly = assembly,
            IconPath = iconKey,
            LocationIconPath = locationIconKey,
            AssemblyIconPath = assemblyIconKey,
            TargetNode = targetNode
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
            IField => $"FieldIcon{suffix}",
            IProperty => $"PropertyIcon{suffix}",
            IEvent => $"EventIcon{suffix}",
            IMethod => $"MethodIcon{suffix}",
            _ => "MemberIcon"
        };
    }

    private static string GetTypeIconKey(ITypeDefinition? typeDefinition)
    {
        if (typeDefinition == null)
            return "ClassIcon";

        if (typeDefinition.Kind == TypeKind.Interface)
            return "InterfaceIcon";

        if (typeDefinition.Kind == TypeKind.Struct)
            return "StructIcon";

        if (typeDefinition.Kind == TypeKind.Enum)
            return "EnumIcon";

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

    private static SearchMode MapMode(string modeName) => modeName switch
    {
        "Type" => SearchMode.Type,
        "Member" => SearchMode.Member,
        "Method" => SearchMode.Method,
        "Field" => SearchMode.Field,
        "Property" => SearchMode.Property,
        "Event" => SearchMode.Event,
        "Constant" => SearchMode.Field,
        "Resource" => SearchMode.Resource,
        "Assembly" => SearchMode.Assembly,
        "Namespace" => SearchMode.Namespace,
        "Types and Members" => SearchMode.TypeAndMember,
        "Metadata Token" => SearchMode.Token,
        _ => SearchMode.TypeAndMember
    };

    private static MemberSearchKind MapMemberKind(SearchMode mode) => mode switch
    {
        SearchMode.Method => MemberSearchKind.Method,
        SearchMode.Field => MemberSearchKind.Field,
        SearchMode.Property => MemberSearchKind.Property,
        SearchMode.Event => MemberSearchKind.Event,
        SearchMode.Type => MemberSearchKind.Type,
        _ => MemberSearchKind.All
    };

    private sealed class DummyTreeNodeFactory : ITreeNodeFactory
    {
        public ITreeNode CreateDecompilerTreeNode(ILanguage language, System.Reflection.Metadata.EntityHandle handle, MetadataFile module) => new DummyTreeNode();

        public ITreeNode CreateResourcesList(MetadataFile module) => new DummyTreeNode();

        public ITreeNode Create(Resource resource) => new DummyTreeNode();
    }

    private sealed class RoverSearchResultFactory : ISearchResultFactory
    {
        public MemberSearchResult Create(IEntity entity) => new MemberSearchResult { Member = entity, Name = entity.Name, Location = entity.DeclaringType?.FullName ?? string.Empty, Assembly = entity.ParentModule?.AssemblyName ?? string.Empty, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };

        public ResourceSearchResult Create(MetadataFile module, Resource resource, ITreeNode node, ITreeNode parent)
        {
            var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
            return new ResourceSearchResult { Resource = resource, Name = resource.Name, Location = node?.ToString() ?? string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
        }

        public AssemblySearchResult Create(MetadataFile module)
        {
            var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
            return new AssemblySearchResult { Module = module, Name = assemblyName, Location = string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
        }

        public NamespaceSearchResult Create(MetadataFile module, INamespace @namespace)
        {
            var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
            return new NamespaceSearchResult { Namespace = @namespace, Name = @namespace.FullName, Location = string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
        }
    }

    private sealed class DummyTreeNode : ITreeNode
    {
        public object Icon => string.Empty;
        public object Text => string.Empty;
        public IEnumerable<ITreeNode> Children => Enumerable.Empty<ITreeNode>();
        public bool IsExpanded => false;
        public void EnsureLazyChildren() { }
    }

    private sealed class BasicLanguage : ILanguage
    {
        public bool ShowMember(IEntity member) => true;

        public CodeMappingInfo GetCodeMappingInfo(MetadataFile module, System.Reflection.Metadata.EntityHandle member) => CSharpDecompiler.GetCodeMappingInfo(module, member);

        public string GetEntityName(MetadataFile module, System.Reflection.Metadata.EntityHandle handle, bool fullName, bool omitGenerics)
        {
            var reader = module.Metadata;
            return handle.Kind switch
            {
                System.Reflection.Metadata.HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
                System.Reflection.Metadata.HandleKind.MethodDefinition => GetMethodName(reader, (MethodDefinitionHandle)handle),
                System.Reflection.Metadata.HandleKind.FieldDefinition => reader.GetString(reader.GetFieldDefinition((FieldDefinitionHandle)handle).Name),
                System.Reflection.Metadata.HandleKind.PropertyDefinition => reader.GetString(reader.GetPropertyDefinition((PropertyDefinitionHandle)handle).Name),
                System.Reflection.Metadata.HandleKind.EventDefinition => reader.GetString(reader.GetEventDefinition((EventDefinitionHandle)handle).Name),
                _ => handle.ToString() ?? string.Empty
            };
        }

        public string GetTooltip(IEntity entity) => entity.FullName ?? entity.Name;

        public string TypeToString(IType type, bool includeNamespace) => includeNamespace ? type.FullName : type.Name;

        public string MethodToString(IMethod method, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            var decl = includeDeclaringTypeName ? method.DeclaringType?.FullName + "." : string.Empty;
            return $"{decl}{method.Name}";
        }

        public string FieldToString(IField field, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            var decl = includeDeclaringTypeName ? field.DeclaringType?.FullName + "." : string.Empty;
            return $"{decl}{field.Name}";
        }

        public string PropertyToString(IProperty property, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            var decl = includeDeclaringTypeName ? property.DeclaringType?.FullName + "." : string.Empty;
            return $"{decl}{property.Name}";
        }

        public string EventToString(IEvent @event, bool includeDeclaringTypeName, bool includeNamespace, bool includeNamespaceOfDeclaringTypeName)
        {
            var decl = includeDeclaringTypeName ? @event.DeclaringType?.FullName + "." : string.Empty;
            return $"{decl}{@event.Name}";
        }

        private static string GetMethodName(System.Reflection.Metadata.MetadataReader reader, MethodDefinitionHandle handle)
        {
            var method = reader.GetMethodDefinition(handle);
            var name = reader.GetString(method.Name);
            if (name == ".ctor" || name == ".cctor")
            {
                var type = reader.GetTypeDefinition(method.GetDeclaringType());
                var typeName = reader.GetString(type.Name);
                return typeName ?? name ?? string.Empty;
            }

            return name ?? string.Empty;
        }
    }

    private static bool IsCompilerGenerated(IEntity entity)
    {
        foreach (var attr in entity.GetAttributes())
        {
            if (attr?.AttributeType?.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
                return true;
        }
        return false;
    }
}
