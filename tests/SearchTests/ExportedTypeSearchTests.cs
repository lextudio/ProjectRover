using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpyX.Search;
using Xunit;

namespace ProjectRover.Tests.SearchTests
{
    public class ExportedTypeSearchTests
    {
        [Fact]
        public void MemberSearch_ShouldFind_ComplexClass_In_TestAssembly()
        {
            // Arrange - locate TestAssembly.dll anywhere under the repo
            string FindTestAssembly()
            {
                var cwd = Directory.GetCurrentDirectory();
                // try walking up to repo root
                var dir = new DirectoryInfo(cwd);
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    var candidate = Path.Combine(dir.FullName, "TestAssembly", "bin");
                    if (Directory.Exists(candidate))
                    {
                        // find any net*/TestAssembly.dll underneath
                        foreach (var netDir in Directory.EnumerateDirectories(candidate))
                        {
                            var dll = Path.Combine(netDir, "TestAssembly.dll");
                            if (File.Exists(dll))
                                return dll;
                        }
                        // also try Debug folders
                        var dbg = Path.Combine(candidate, "Debug");
                        if (Directory.Exists(dbg))
                        {
                            foreach (var netDir in Directory.EnumerateDirectories(dbg))
                            {
                                var dll = Path.Combine(netDir, "TestAssembly.dll");
                                if (File.Exists(dll))
                                    return dll;
                            }
                        }
                    }
                    dir = dir.Parent;
                }
                // fallback: search whole repo (expensive)
                var repoRoot = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (repoRoot.Parent != null && repoRoot.GetDirectories().Length > 0)
                    repoRoot = repoRoot.Parent;
                var found = Directory.GetFiles(Directory.GetCurrentDirectory(), "TestAssembly.dll", SearchOption.AllDirectories).FirstOrDefault();
                return found ?? string.Empty;
            }

            var assemblyPath = FindTestAssembly();
            Assert.True(!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath), $"Test assembly not found (searched) - last candidate: {assemblyPath}");

            using var fs = File.OpenRead(assemblyPath);
            // Use PEReader for PE files so we correctly locate the embedded metadata
            using var peReader = new System.Reflection.PortableExecutable.PEReader(fs);
            if (!peReader.HasMetadata)
                throw new InvalidOperationException("Assembly does not contain CLI metadata.");
            var module = new ICSharpCode.Decompiler.Metadata.PEFile(assemblyPath, peReader);

            // Build type system directly by creating a SimpleCompilation (avoids needing LoadedAssembly registration)
            var typeSystem = new ICSharpCode.Decompiler.TypeSystem.Implementation.SimpleCompilation(
                module.WithOptions(ICSharpCode.Decompiler.TypeSystem.TypeSystemOptions.Default),
                ICSharpCode.Decompiler.TypeSystem.MinimalCorlib.Instance
            );
            // Register with internal LoadedAssembly mapping so search helper can access typeSystem
            try
            {
                var loadedAssembliesField = typeof(ICSharpCode.ILSpyX.LoadedAssembly).GetField("loadedAssemblies", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (loadedAssembliesField != null)
                {
                    var table = loadedAssembliesField.GetValue(null)!;
                    var add = table.GetType().GetMethod("Add", new Type[] { typeof(ICSharpCode.Decompiler.Metadata.MetadataFile), typeof(object) });
                    if (add != null)
                    {
                        var ctors = typeof(ICSharpCode.ILSpyX.LoadedAssembly).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length >= 2);
                        if (ctor != null)
                        {
                            var asmList = Activator.CreateInstance(typeof(ICSharpCode.ILSpyX.AssemblyList), nonPublic: true);
                            var loadedAsm = ctor.Invoke(new object?[] { asmList, assemblyPath, null, null, null, null, false, false });
                            add.Invoke(table, new object[] { module, loadedAsm! });
                        }
                    }
                }
            }
            catch { }

            // Quick metadata sanity-check: enumerate type definitions and exported types
            var results = new ConcurrentQueue<SearchResult>();
            // Minimal headless test: run MemberSearchStrategy and assert result contains OuterClass
            var factory = new SimpleSearchResultFactory();
            var request = new SearchRequest {
                DecompilerSettings = new ICSharpCode.Decompiler.DecompilerSettings(),
                SearchResultFactory = factory,
                Keywords = new[] { "OuterClass" },
                FullNameSearch = false,
                OmitGenerics = false,
                Mode = SearchMode.Type,
                MemberSearchKind = MemberSearchKind.Type
            };

            var strategy = new MemberSearchStrategy(new DummyLanguage(), ICSharpCode.ILSpyX.ApiVisibility.All, request, results, MemberSearchKind.Type);

            // Act
            strategy.Search(module, CancellationToken.None);

            // Assert
            Assert.Contains(results, r => r.Name.Contains("OuterClass"));
        }

            class SimpleSearchResultFactory : ISearchResultFactory
        {
            public MemberSearchResult Create(IEntity entity)
            {
                return new MemberSearchResult {
                    Member = entity,
                    Name = entity.Name,
                    Assembly = entity.ParentModule?.FullAssemblyName ?? "",
                    Location = entity.Namespace ?? "",
                    Image = new object(),
                    LocationImage = new object(),
                    AssemblyImage = new object()
                };
            }
            public ResourceSearchResult Create(MetadataFile module, Resource resource, ICSharpCode.ILSpyX.Abstractions.ITreeNode node, ICSharpCode.ILSpyX.Abstractions.ITreeNode parent)
            {
                throw new NotImplementedException();
            }

            public AssemblySearchResult Create(MetadataFile module)
            {
                throw new NotImplementedException();
            }

            public NamespaceSearchResult Create(MetadataFile module, INamespace @namespace)
            {
                throw new NotImplementedException();
            }
        }

        class DummyLanguage : ICSharpCode.ILSpyX.Abstractions.ILanguage
        {
            public string EntityToString(IEntity entity, ICSharpCode.Decompiler.Output.ConversionFlags conversionFlags) => entity.FullName ?? entity.Name;
            public string GetEntityName(MetadataFile module, System.Reflection.Metadata.EntityHandle handle, bool fullName, bool omitGenerics)
            {
                try
                {
                    if (handle.Kind == System.Reflection.Metadata.HandleKind.TypeDefinition)
                    {
                        var td = module.Metadata.GetTypeDefinition((System.Reflection.Metadata.TypeDefinitionHandle)handle);
                        return module.Metadata.GetString(td.Name);
                    }
                    if (handle.Kind == System.Reflection.Metadata.HandleKind.ExportedType)
                    {
                        var et = module.Metadata.GetExportedType((System.Reflection.Metadata.ExportedTypeHandle)handle);
                        return module.Metadata.GetString(et.Name);
                    }
                }
                catch { }
                return null;
            }
            public string GetTooltip(IEntity entity) => EntityToString(entity, 0);
            public string TypeToString(IType type, ICSharpCode.Decompiler.Output.ConversionFlags conversionFlags) => type.Name;
            public ICSharpCode.Decompiler.Metadata.CodeMappingInfo GetCodeMappingInfo(MetadataFile module, System.Reflection.Metadata.EntityHandle handle)
            {
                return null;
            }
            public bool ShowMember(IEntity member) => true;
        }
    }
}
