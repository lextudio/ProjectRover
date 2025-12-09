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
using System.Threading;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace ProjectRover.Services;

public sealed class IlSpyBackend : IDisposable
{
    private readonly DefaultAssemblyResolver cecilResolver = new();
    private readonly HashSet<string> searchDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IlSpyAssembly> loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    public IlSpyAssembly? LoadAssembly(string filePath)
    {
        if (loadedAssemblies.TryGetValue(filePath, out var existing))
            return existing;

        if (!File.Exists(filePath))
            return null;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            AddSearchDirectory(directory);
        }

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = cecilResolver,
            InMemory = true,
            ReadSymbols = true,
            ReadingMode = ReadingMode.Deferred,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false)
        };

        CecilAssemblyDefinition assemblyDefinition;
        try
        {
            using var assemblyStream = File.OpenRead(filePath);
            assemblyDefinition = CecilAssemblyDefinition.ReadAssembly(assemblyStream, readerParameters);
        }
        catch (SymbolsNotFoundException)
        {
            var fallbackParameters = new ReaderParameters
            {
                AssemblyResolver = cecilResolver,
                InMemory = true,
                ReadSymbols = false,
                ReadingMode = ReadingMode.Deferred
            };

            using var assemblyStream = File.OpenRead(filePath);
            assemblyDefinition = CecilAssemblyDefinition.ReadAssembly(assemblyStream, fallbackParameters);
        }

        PEFile peFile;
        using (var peStream = File.OpenRead(filePath))
        {
            peFile = new PEFile(filePath, peStream, PEStreamOptions.PrefetchEntireImage);
        }

        var targetFramework = peFile.Metadata.DetectTargetFrameworkId(filePath);
        var runtimePack = peFile.DetectRuntimePack();
        var resolver = new UniversalAssemblyResolver(
            filePath,
            throwOnError: false,
            targetFramework,
            runtimePack,
            PEStreamOptions.PrefetchEntireImage);

        // Make sure every resolver knows all the search paths we've collected so far.
        foreach (var dir in searchDirectories)
        {
            resolver.AddSearchDirectory(dir);
        }

        var ilSpyAssembly = new IlSpyAssembly(filePath, assemblyDefinition, peFile, resolver);
        loadedAssemblies[filePath] = ilSpyAssembly;
        return ilSpyAssembly;
    }

    public string DecompileMember(IlSpyAssembly assembly, IMemberDefinition memberDefinition, DecompilationLanguage language, DecompilerSettings? settings = null)
    {
        var handle = ToHandle(memberDefinition);
        if (handle.IsNil)
        {
            return $"// Unable to map member token: {memberDefinition.FullName}";
        }

        return language switch
        {
            DecompilationLanguage.IL => DecompileIl(assembly, handle, settings),
            _ => DecompileCSharp(assembly, handle, settings)
        };
    }

    public void Clear()
    {
        foreach (var asm in loadedAssemblies.Values)
        {
            asm.AssemblyDefinition.Dispose();
        }

        loadedAssemblies.Clear();
        searchDirectories.Clear();
    }

    public void Dispose()
    {
        Clear();
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

    private static EntityHandle ToHandle(IMemberDefinition memberDefinition)
    {
        var token = memberDefinition.MetadataToken;
        return token.TokenType switch
        {
            TokenType.TypeDef => MetadataTokens.TypeDefinitionHandle((int)token.RID),
            TokenType.Field => MetadataTokens.FieldDefinitionHandle((int)token.RID),
            TokenType.Method => MetadataTokens.MethodDefinitionHandle((int)token.RID),
            TokenType.Property => MetadataTokens.PropertyDefinitionHandle((int)token.RID),
            TokenType.Event => MetadataTokens.EventDefinitionHandle((int)token.RID),
            _ => default
        };
    }

    private void AddSearchDirectory(string directory)
    {
        if (searchDirectories.Add(directory))
        {
            cecilResolver.AddSearchDirectory(directory);
            foreach (var asm in loadedAssemblies.Values)
            {
                asm.Resolver.AddSearchDirectory(directory);
            }
        }
    }
}

public record IlSpyAssembly(string FilePath, CecilAssemblyDefinition AssemblyDefinition, PEFile PeFile, UniversalAssemblyResolver Resolver);
