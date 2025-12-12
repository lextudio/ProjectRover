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
        Serilog.Log.Debug("[IlSpyXSearchAdapter] Search called. term='{Term}', modeName='{ModeName}', includeInternal={IncludeInternal}, includeCompilerGenerated={IncludeCompilerGenerated}", term, modeName, includeInternal, includeCompilerGenerated);
        var trimmed = term?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Array.Empty<BasicSearchResult>();

        var searchMode = MapMode(modeName);
        var memberKind = MapMemberKind(searchMode);
        var constantOnly = string.Equals(modeName, "Constant", StringComparison.OrdinalIgnoreCase);
        var apiVisibility = includeInternal ? ApiVisibility.PublicAndInternal : ApiVisibility.PublicOnly;

        var results = new ConcurrentBag<ICSharpCode.ILSpyX.Search.SearchResult>();
        var tokenSource = new CancellationTokenSource();

        foreach (var asm in assemblies)
        {
            var module = asm.GetMetadataFileOrNull();
            if (module == null)
                continue;

            var treeNodeFactory = new DummyTreeNodeFactory(asm);
            var request = new SearchRequest
            {
                DecompilerSettings = decompilerSettings,
                TreeNodeFactory = treeNodeFactory,
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

            var strategies = CreateStrategies(request, results, apiVisibility, memberKind);
            var beforeCount = results.Count;
            foreach (var strategy in strategies)
            {
                strategy.Search(module, tokenSource.Token);
            }
            var added = results.Count - beforeCount;
            Serilog.Log.Debug("[IlSpyXSearchAdapter] Assembly '{Assembly}' added {Added} results (total {Total})", asm.ShortName, added, results.Count);
            if (added > 0)
            {
                foreach (var r in results.Skip(beforeCount).Take(10))
                {
                    try { Serilog.Log.Debug("[IlSpyXSearchAdapter] Sample result: {Name} ({Type})", r.Name, r.GetType().Name); } catch { }
                }
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
            SearchMode.Token => true,
            _ => true
        };
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

    private static BasicSearchResult MapToBasicResult(ICSharpCode.ILSpyX.Search.SearchResult r, Func<EntityHandle, Node?>? resolveNode, Func<string, Node?>? resolveAssembly, Func<string, string, Node?>? resolveResource)
    {
        Serilog.Log.Debug("[IlSpyXSearchAdapter] MapToBasicResult mapping: {Name} ({Type})", r.Name, r.GetType().Name);
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
        private readonly LoadedAssembly loadedAssembly;
        public DummyTreeNodeFactory(LoadedAssembly loadedAssembly)
        {
            this.loadedAssembly = loadedAssembly;
            Serilog.Log.Debug("[DummyTreeNodeFactory] Constructor called for assembly: {Assembly}", loadedAssembly.ShortName);
        }
        public ITreeNode CreateDecompilerTreeNode(ILanguage language, System.Reflection.Metadata.EntityHandle handle, MetadataFile module)
        {
            Serilog.Log.Debug("[DummyTreeNodeFactory] CreateDecompilerTreeNode called. HandleKind: {HandleKind}, Handle: {Handle}", handle.Kind, handle);
            var text = language.GetEntityName(module, handle, fullName: false, omitGenerics: false);
            Serilog.Log.Debug("[DummyTreeNodeFactory] HandleKind: {HandleKind}, Handle: {Handle}", handle.Kind, handle);
            var typeSystem = loadedAssembly.GetTypeSystemOrNull();
            IEntity? entity = null;
            ITypeDefinition? typeDef = null;
            if (typeSystem != null && typeSystem.MainModule is MetadataModule metadataModule)
            {
                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        var typeHandle = (TypeDefinitionHandle)handle;
                        typeDef = metadataModule.GetDefinition(typeHandle) as ITypeDefinition;
                        entity = typeDef;
                        Serilog.Log.Debug("[DummyTreeNodeFactory] TypeDefinition resolved: {TypeDef}", typeDef?.FullName);
                        break;
                    case HandleKind.MethodDefinition:
                        var methodHandle = (MethodDefinitionHandle)handle;
                        entity = metadataModule.GetDefinition(methodHandle) as IMethod;
                        typeDef = entity?.DeclaringType?.GetDefinition();
                        Serilog.Log.Debug("[DummyTreeNodeFactory] MethodDefinition resolved: {Entity}", entity?.FullName);
                        break;
                    case HandleKind.FieldDefinition:
                        var fieldHandle = (FieldDefinitionHandle)handle;
                        entity = metadataModule.GetDefinition(fieldHandle) as IField;
                        typeDef = entity?.DeclaringType?.GetDefinition();
                        Serilog.Log.Debug("[DummyTreeNodeFactory] FieldDefinition resolved: {Entity}", entity?.FullName);
                        break;
                    case HandleKind.PropertyDefinition:
                        var propertyHandle = (PropertyDefinitionHandle)handle;
                        entity = metadataModule.GetDefinition(propertyHandle) as IProperty;
                        typeDef = entity?.DeclaringType?.GetDefinition();
                        Serilog.Log.Debug("[DummyTreeNodeFactory] PropertyDefinition resolved: {Entity}", entity?.FullName);
                        break;
                    case HandleKind.EventDefinition:
                        var eventHandle = (EventDefinitionHandle)handle;
                        entity = metadataModule.GetDefinition(eventHandle) as IEvent;
                        typeDef = entity?.DeclaringType?.GetDefinition();
                        Serilog.Log.Debug("[DummyTreeNodeFactory] EventDefinition resolved: {Entity}", entity?.FullName);
                        break;
                    default:
                        Serilog.Log.Debug("[DummyTreeNodeFactory] Unknown handle kind: {HandleKind}", handle.Kind);
                        break;
                }
                if (entity == null)
                {
                    Serilog.Log.Debug("[DummyTreeNodeFactory] Entity resolution failed for handle kind: {HandleKind}, handle: {Handle}", handle.Kind, handle);
                }
            }
            return new DummyTreeNode { Text = text, Member = entity, Type = typeDef };
        }

        public ITreeNode CreateResourcesList(MetadataFile module)
        {
            Serilog.Log.Debug("[DummyTreeNodeFactory] CreateResourcesList called for module: {Module}", module.FileName);
            return new DummyTreeNode { Text = "Resources" };
        }

        public ITreeNode Create(Resource resource)
        {
            Serilog.Log.Debug("[DummyTreeNodeFactory] Create(Resource) called for resource: {Resource}", resource.Name);
            return new DummyTreeNode { Text = resource.Name, Resource = resource };
        }

        public ITreeNode CreateNamespace(string namespaceName)
        {
            Serilog.Log.Debug("[DummyTreeNodeFactory] CreateNamespace called for namespace: {Namespace}", namespaceName);
            return new DummyTreeNode { Text = namespaceName };
        }
        public ITreeNode CreateAssembly(string assemblyName)
        {
            Serilog.Log.Debug("[DummyTreeNodeFactory] CreateAssembly called for assembly: {Assembly}", assemblyName);
            return new DummyTreeNode { Text = assemblyName };
        }
    }

}


internal sealed class RoverSearchResultFactory : ISearchResultFactory
{
    public RoverSearchResultFactory()
    {
        Serilog.Log.Debug("[RoverSearchResultFactory] Constructor called");
    }

    public MemberSearchResult Create(IEntity entity)
    {
        Serilog.Log.Debug("[RoverSearchResultFactory] Create(Member) called for entity: {Entity}", entity?.FullName);
        var name = entity?.Name ?? string.Empty;
        var location = entity?.DeclaringType?.FullName ?? string.Empty;
        var assembly = entity?.ParentModule?.AssemblyName ?? string.Empty;
        return new MemberSearchResult { Member = entity, Name = name, Location = location, Assembly = assembly, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
    }

    public ResourceSearchResult Create(MetadataFile module, Resource resource, ITreeNode node, ITreeNode parent)
    {
        Serilog.Log.Debug("[RoverSearchResultFactory] Create(Resource) called for resource: {Resource}, module: {Module}", resource.Name, module.FileName);
        var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
        return new ResourceSearchResult { Resource = resource, Name = resource.Name, Location = node?.ToString() ?? string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
    }


    public AssemblySearchResult Create(MetadataFile module)
    {
        Serilog.Log.Debug("[RoverSearchResultFactory] Create(Assembly) called for module: {Module}", module.FileName);
        var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
        return new AssemblySearchResult { Module = module, Name = assemblyName, Location = string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
    }


    public NamespaceSearchResult Create(MetadataFile module, INamespace @namespace)
    {
        Serilog.Log.Debug("[RoverSearchResultFactory] Create(Namespace) called for namespace: {Namespace}, module: {Module}", @namespace.FullName, module.FileName);
        var assemblyName = module.Metadata.GetAssemblyDefinition().GetAssemblyName().Name ?? string.Empty;
        return new NamespaceSearchResult { Namespace = @namespace, Name = @namespace.FullName, Location = string.Empty, Assembly = assemblyName, Image = string.Empty, LocationImage = string.Empty, AssemblyImage = string.Empty };
    }
}

internal sealed class DummyTreeNode : ITreeNode, IResourcesFileTreeNode
{
    public object Icon => string.Empty;
    public object Text { get; init; } = string.Empty;
    public IEntity? Member { get; init; }
    public ITypeDefinition? Type { get; init; }
    public IEnumerable<ITreeNode> Children => Enumerable.Empty<ITreeNode>();
    public bool IsExpanded => false;
    public void EnsureLazyChildren() { }
    public Resource Resource { get; init; } = null!;
}

internal sealed class BasicLanguage : ILanguage
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
