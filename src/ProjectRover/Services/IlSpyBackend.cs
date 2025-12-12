/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ProjectRover.

    ProjectRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProjectRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Linq;
using System.Threading;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.Settings;
using DecompilerSettings = ICSharpCode.Decompiler.DecompilerSettings;
using LanguageVersion = ICSharpCode.Decompiler.CSharp.LanguageVersion;

namespace ProjectRover.Services;

public sealed class IlSpyBackend : IDisposable
{
    private readonly AssemblyList assemblyList;

    public IlSpyBackend()
    {
        if (ILSpySettings.SettingsFilePathProvider == null)
        {
            ILSpySettings.SettingsFilePathProvider = new RoverSettingsFilePathProvider();
        }
        var settings = ILSpySettings.Load();
        var manager = new AssemblyListManager(settings);
        assemblyList = manager.LoadList(AssemblyListManager.DefaultListName);
    }

    public IlSpyAssembly? LoadAssembly(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var loaded = assemblyList.OpenAssembly(filePath);
        var metadata = loaded.GetMetadataFileOrNull();
        if (metadata is not PEFile peFile)
            return null;

        var resolver = loaded.GetAssemblyResolver();
        var typeSystem = new DecompilerTypeSystem(peFile, resolver, new DecompilerSettings(LanguageVersion.Latest));

        return new IlSpyAssembly(filePath, peFile, resolver, typeSystem, loaded);
    }

    public bool UseDebugSymbols
    {
        get => assemblyList.UseDebugSymbols;
        set => assemblyList.UseDebugSymbols = value;
    }

    public bool ApplyWinRtProjections
    {
        get => assemblyList.ApplyWinRTProjections;
        set => assemblyList.ApplyWinRTProjections = value;
    }

    public IEnumerable<string> GetPersistedAssemblyFiles()
    {
        return assemblyList.GetAssemblies()
            .Select(a => a.FileName)
            .Where(File.Exists)
            .ToArray();
    }

    public void UnloadAssembly(IlSpyAssembly assembly)
    {
        assemblyList.Unload(assembly.LoadedAssembly);
    }

public string DecompileMember(IlSpyAssembly assembly, EntityHandle handle, DecompilationLanguage language, DecompilerSettings? settings = null)
{
    if (handle.IsNil)
    {
        return "// Unable to map member token.";
    }

        return language switch
        {
            DecompilationLanguage.IL => DecompileIl(assembly, handle, settings),
            _ => DecompileCSharp(assembly, handle, settings)
        };
    }

    public void Clear()
    {
        assemblyList.Clear();
    }

    public void SaveAssemblyList()
    {
        assemblyList.RefreshSave();
    }

    public void Dispose()
    {
        Clear();
    }

    public IEnumerable<(EntityHandle Handle, string DisplayName, string AssemblyPath)> AnalyzeSymbolReferences(IlSpyAssembly assembly, EntityHandle handle, DecompilationLanguage language)
    {
        Serilog.Log.Debug("[IlSpyBackend] AnalyzeSymbolReferences called for assembly={Assembly} handle={Handle}", assembly.FilePath, handle);
        if (handle.IsNil)
        {
            Serilog.Log.Debug("[IlSpyBackend] AnalyzeSymbolReferences: handle is nil, returning");
            return Array.Empty<(EntityHandle, string, string)>();
        }

        // Resolve IEntity from handle using the assembly's type system
        var typeSystem = assembly.TypeSystem;
        ICSharpCode.Decompiler.TypeSystem.IEntity? entity = null;
        try
        {
            var module = typeSystem.MainModule as ICSharpCode.Decompiler.TypeSystem.MetadataModule;
            if (module != null)
            {
                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        entity = module.GetDefinition((TypeDefinitionHandle)handle) as ICSharpCode.Decompiler.TypeSystem.IEntity;
                        break;
                    case HandleKind.MethodDefinition:
                        entity = module.GetDefinition((MethodDefinitionHandle)handle) as ICSharpCode.Decompiler.TypeSystem.IEntity;
                        break;
                    case HandleKind.FieldDefinition:
                        entity = module.GetDefinition((FieldDefinitionHandle)handle) as ICSharpCode.Decompiler.TypeSystem.IEntity;
                        break;
                    case HandleKind.PropertyDefinition:
                        entity = module.GetDefinition((PropertyDefinitionHandle)handle) as ICSharpCode.Decompiler.TypeSystem.IEntity;
                        break;
                    case HandleKind.EventDefinition:
                        entity = module.GetDefinition((EventDefinitionHandle)handle) as ICSharpCode.Decompiler.TypeSystem.IEntity;
                        break;
                    default:
                        entity = null;
                        break;
                }
            }
        }
        catch
        {
            entity = null;
        }

        if (entity == null)
        {
            Serilog.Log.Debug("[IlSpyBackend] AnalyzeSymbolReferences: failed to resolve IEntity for handle={Handle}", handle);
            return Array.Empty<(EntityHandle, string, string)>();
        }
        Serilog.Log.Debug("[IlSpyBackend] AnalyzeSymbolReferences: resolved entity {Entity} (type={Type})", entity.Name ?? entity.MetadataToken.ToString(), entity.GetType().FullName);

        // Discover analyzer types
        var analyzerTypes = ICSharpCode.ILSpyX.Analyzers.ExportAnalyzerAttribute.GetAnnotatedAnalyzers()
            .Select(t => t.AnalyzerType)
            .ToList();
        Serilog.Log.Debug("[IlSpyBackend] AnalyzeSymbolReferences: discovered {Count} analyzer types", analyzerTypes.Count);

        // Build AnalyzerContext using the existing assemblyList and a simple language adapter
        var context = new ICSharpCode.ILSpyX.Analyzers.AnalyzerContext
        {
            AssemblyList = assemblyList,
            Language = new ProjectRover.Services.IlSpyX.BasicLanguage(),
            CancellationToken = CancellationToken.None
        };

        var results = new List<(EntityHandle, string, string)>();

        foreach (var analyzerType in analyzerTypes)
        {
            Serilog.Log.Debug("[IlSpyBackend] Trying analyzer type {AnalyzerType}", analyzerType.FullName);
            ICSharpCode.ILSpyX.Analyzers.IAnalyzer? analyzer = null;
            try
            {
                analyzer = Activator.CreateInstance(analyzerType) as ICSharpCode.ILSpyX.Analyzers.IAnalyzer;
            }
            catch
            {
                analyzer = null;
            }

            if (analyzer == null)
            {
                Serilog.Log.Debug("[IlSpyBackend] Skipping analyzer {AnalyzerType} - could not instantiate", analyzerType.FullName);
                continue;
            }

            try
            {
                if (!analyzer.Show(entity))
                {
                    Serilog.Log.Debug("[IlSpyBackend] Analyzer {AnalyzerType} .Show returned false for entity {Entity}", analyzerType.FullName, entity.Name ?? entity.MetadataToken.ToString());
                    continue;
                }
                Serilog.Log.Debug("[IlSpyBackend] Analyzer {AnalyzerType} will run for entity {Entity}", analyzerType.FullName, entity.Name ?? entity.MetadataToken.ToString());

                foreach (var sym in analyzer.Analyze(entity, context))
                {
                    if (sym is ICSharpCode.Decompiler.TypeSystem.IEntity ie && ie.ParentModule?.MetadataFile != null)
                    {
                        var mdFile = ie.ParentModule.MetadataFile;
                        var asmPath = mdFile.FileName ?? assembly.FilePath ?? string.Empty;
                        var displayName = ie.Name ?? ie.MetadataToken.ToString() ?? string.Empty;
                        results.Add((ie.MetadataToken, displayName, asmPath));
                        Serilog.Log.Debug("[IlSpyBackend] Analyzer {AnalyzerType} found symbol {Symbol} handle={Handle} in assembly={Asm}", analyzerType.FullName, displayName, ie.MetadataToken, asmPath);
                    }
                    else
                    {
                        Serilog.Log.Debug("[IlSpyBackend] Analyzer {AnalyzerType} returned non-entity or unresolved symbol", analyzerType.FullName);
                    }
                }
            }
            catch
            {
                // analyzer errors are non-fatal
            }
        }

        return results;
    }

    private static string DecompileCSharp(IlSpyAssembly assembly, EntityHandle handle, DecompilerSettings? settings)
    {
        var decompiler = new CSharpDecompiler(assembly.PeFile, assembly.Resolver, settings ?? new DecompilerSettings(LanguageVersion.Latest));
        return decompiler.DecompileAsString(handle);
    }

    private string DecompileIl(IlSpyAssembly assembly, EntityHandle handle, DecompilerSettings? settings)
    {
        var output = new PlainTextOutput
        {
            IndentationString = (settings ?? new DecompilerSettings(LanguageVersion.Latest)).CSharpFormattingOptions.IndentationString
        };
        var disassembler = new ReflectionDisassembler(output, CancellationToken.None)
        {
            DetectControlStructure = true,
            AssemblyResolver = assembly.Resolver
        };

        var peFile = assembly.PeFile;
        disassembler.DebugInfo = null;

        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                disassembler.DisassembleMethod(peFile, (MethodDefinitionHandle)handle);
                break;
            case HandleKind.FieldDefinition:
                disassembler.DisassembleField(peFile, (FieldDefinitionHandle)handle);
                break;
            case HandleKind.PropertyDefinition:
                var propertyHandle = (PropertyDefinitionHandle)handle;
                disassembler.DisassembleProperty(peFile, propertyHandle);
                var propertyDefinition = peFile.Metadata.GetPropertyDefinition(propertyHandle);
                var propertyAccessors = propertyDefinition.GetAccessors();
                if (!propertyAccessors.Getter.IsNil)
                {
                    output.WriteLine();
                    disassembler.DisassembleMethod(peFile, propertyAccessors.Getter);
                }
                if (!propertyAccessors.Setter.IsNil)
                {
                    output.WriteLine();
                    disassembler.DisassembleMethod(peFile, propertyAccessors.Setter);
                }
                break;
            case HandleKind.EventDefinition:
                var eventHandle = (EventDefinitionHandle)handle;
                disassembler.DisassembleEvent(peFile, eventHandle);
                var eventDefinition = peFile.Metadata.GetEventDefinition(eventHandle);
                var eventAccessors = eventDefinition.GetAccessors();
                if (!eventAccessors.Adder.IsNil)
                {
                    output.WriteLine();
                    disassembler.DisassembleMethod(peFile, eventAccessors.Adder);
                }
                if (!eventAccessors.Remover.IsNil)
                {
                    output.WriteLine();
                    disassembler.DisassembleMethod(peFile, eventAccessors.Remover);
                }
                if (!eventAccessors.Raiser.IsNil)
                {
                    output.WriteLine();
                    disassembler.DisassembleMethod(peFile, eventAccessors.Raiser);
                }
                break;
            case HandleKind.TypeDefinition:
                disassembler.DisassembleType(peFile, (TypeDefinitionHandle)handle);
                break;
            default:
                return $"// Unable to disassemble handle: {handle.Kind}";
        }

        return output.ToString();
    }

}

public record IlSpyAssembly(string FilePath, PEFile PeFile, IAssemblyResolver Resolver, DecompilerTypeSystem TypeSystem, LoadedAssembly LoadedAssembly);
