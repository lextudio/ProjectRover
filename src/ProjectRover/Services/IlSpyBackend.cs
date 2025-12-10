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
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.Settings;

namespace ProjectRover.Services;

public sealed class IlSpyBackend : IDisposable
{
    private readonly AssemblyList assemblyList;

    public IlSpyBackend()
    {
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

    }
}

public record IlSpyAssembly(string FilePath, PEFile PeFile, IAssemblyResolver Resolver, DecompilerTypeSystem TypeSystem, LoadedAssembly LoadedAssembly);
