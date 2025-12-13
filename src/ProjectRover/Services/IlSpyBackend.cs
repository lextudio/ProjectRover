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
using System.Collections.Immutable;
using ICSharpCode.ILSpy;

namespace ProjectRover.Services;

public sealed class IlSpyBackend : IDisposable
{
    private readonly AssemblyList assemblyList;

    public IlSpyBackend()
    {
        if (ILSpySettings.SettingsFilePathProvider == null)
        {
            ILSpySettings.SettingsFilePathProvider = new ILSpySettingsFilePathProvider();
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

    /// <summary>
    /// Resolve candidate assembly file paths for a simple assembly name and optional MVID.
    /// The method consults the current <see cref="assemblyList"/>, persisted assemblies, and sibling directories
    /// for likely candidates (simpleName.dll/.exe) and, if an MVID is provided, will prefer candidates matching it.
    /// </summary>
    public IEnumerable<string> ResolveAssemblyCandidates(string simpleName, Guid? mvid = null)
    {
        if (string.IsNullOrWhiteSpace(simpleName))
            return Array.Empty<string>();

        var nameVariants = new[] { simpleName, simpleName + ".dll", simpleName + ".exe" };
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var a in assemblyList.GetAssemblies())
            {
                try
                {
                    var fn = a.FileName;
                    if (string.IsNullOrEmpty(fn))
                        continue;

                    var fileNameOnly = Path.GetFileName(fn);
                    if (nameVariants.Contains(fileNameOnly, StringComparer.OrdinalIgnoreCase) ||
                        Path.GetFileNameWithoutExtension(fn).Equals(simpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!candidates.Contains(fn))
                            candidates.Add(fn);
                    }
                }
                catch
                {
                    // ignore per-assembly probing errors
                }
            }

            foreach (var p in GetPersistedAssemblyFiles())
            {
                if (!candidates.Contains(p) && File.Exists(p))
                    candidates.Add(p);
            }

            // Probing sibling directories of known candidates for simpleName.dll/.exe
            var snapshot = candidates.ToList();
            foreach (var known in snapshot)
            {
                try
                {
                    var dir = Path.GetDirectoryName(known);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        continue;

                    foreach (var ext in new[] { ".dll", ".exe" })
                    {
                        var path = Path.Combine(dir, simpleName + ext);
                        if (File.Exists(path) && !candidates.Contains(path))
                            candidates.Add(path);
                    }
                }
                catch
                {
                    // ignore directory probe errors
                }
            }

            // If MVID provided, filter to matches (prefer exact matches)
            if (mvid.HasValue)
            {
                var matches = new List<string>();
                foreach (var file in candidates)
                {
                    try
                    {
                        using var fs = File.OpenRead(file);
                        using var pe = new PEReader(fs, PEStreamOptions.Default);
                        if (!pe.HasMetadata)
                            continue;
                        var reader = pe.GetMetadataReader();
                        var guid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
                        if (guid == mvid.Value)
                            matches.Add(file);
                    }
                    catch
                    {
                        // ignore unreadable files
                    }
                }

                if (matches.Count > 0)
                    return matches;
            }
        }
        catch
        {
            // top-level defensive catch - return what we have
        }

        return candidates;
    }

    /// <summary>
    /// Probe a candidate assembly file to see if it contains the specified metadata <paramref name="handle"/>.
    /// Returns true when the handle resolves in the metadata without throwing.
    /// </summary>
    public bool ProbeAssemblyForHandle(string filePath, EntityHandle handle)
    {
        if (handle.IsNil)
            return false;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var fs = File.OpenRead(filePath);
            using var pe = new PEReader(fs, PEStreamOptions.Default);
            if (!pe.HasMetadata)
                return false;
            var reader = pe.GetMetadataReader();

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return true;
                case HandleKind.MethodDefinition:
                    reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    return true;
                case HandleKind.FieldDefinition:
                    reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    return true;
                case HandleKind.PropertyDefinition:
                    reader.GetPropertyDefinition((PropertyDefinitionHandle)handle);
                    return true;
                case HandleKind.EventDefinition:
                    reader.GetEventDefinition((EventDefinitionHandle)handle);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public async System.Threading.Tasks.Task<bool> ProbeAssemblyForHandleAsync(string filePath, EntityHandle handle, IProgress<string>? progress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        return await System.Threading.Tasks.Task.Run(() => ProbeAssemblyForHandle(filePath, handle), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempt to locate the best assembly file that contains the specified metadata handle or symbol name.
    /// Heuristics used (in order):
    /// - If a simple assembly name is provided, prefer candidates from <see cref="ResolveAssemblyCandidates"/> and MVID matches.
    /// - Probe candidates by directly checking for the handle via <see cref="ProbeAssemblyForHandle"/> for definition handles.
    /// - If a symbolic name is provided, probe by looking up type/member names and exported type forwards.
    /// - As a last resort, scan all known/persisted assemblies and return the first positive probe.
    /// Returns the file path of the matching assembly or null if none found.
    /// </summary>
    public string? ResolveAssemblyForHandle(EntityHandle handle, string? simpleAssemblyName = null, string? symbolName = null, Guid? mvid = null)
    {
        if (handle.IsNil && string.IsNullOrEmpty(symbolName))
            return null;

        // Build candidate set
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(simpleAssemblyName))
        {
            candidates.AddRange(ResolveAssemblyCandidates(simpleAssemblyName!, mvid));
        }

        // Add persisted assemblies and currently loaded ones if not already present
        foreach (var p in GetPersistedAssemblyFiles())
        {
            if (!candidates.Contains(p) && File.Exists(p))
                candidates.Add(p);
        }

        foreach (var a in assemblyList.GetAssemblies())
        {
            try
            {
                var fn = a.FileName;
                if (!string.IsNullOrEmpty(fn) && File.Exists(fn) && !candidates.Contains(fn))
                    candidates.Add(fn);
            }
            catch
            {
                // ignore
            }
        }

        // Prefer MVID exact matches first
        if (mvid.HasValue && candidates.Count > 0)
        {
            var mvidMatches = new List<string>();
            foreach (var file in candidates)
            {
                try
                {
                    using var fs = File.OpenRead(file);
                    using var pe = new PEReader(fs, PEStreamOptions.Default);
                    if (!pe.HasMetadata)
                        continue;
                    var reader = pe.GetMetadataReader();
                    var guid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
                    if (guid == mvid.Value)
                        mvidMatches.Add(file);
                }
                catch
                {
                }
            }

            if (mvidMatches.Count > 0)
            {
                // Probe exact MVID matches for the handle/symbol
                foreach (var f in mvidMatches)
                {
                    try
                    {
                        if (!handle.IsNil && ProbeAssemblyForHandle(f, handle))
                            return f;

                        if (!string.IsNullOrEmpty(symbolName) && ProbeAssemblyForSymbolName(f, symbolName))
                            return f;
                    }
                    catch { }
                }
            }
        }

        // Probe candidate list in order
        foreach (var f in candidates)
        {
            try
            {
                if (!handle.IsNil && ProbeAssemblyForHandle(f, handle))
                    return f;

                if (!string.IsNullOrEmpty(symbolName) && ProbeAssemblyForSymbolName(f, symbolName))
                    return f;
            }
            catch
            {
                // ignore per-file errors
            }
        }

        // As a final attempt, do a broader scan across sibling directories of known candidates for common file names
        try
        {
            var extra = new List<string>();
            foreach (var known in candidates.ToArray())
            {
                try
                {
                    var dir = Path.GetDirectoryName(known);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        continue;

                    foreach (var f in Directory.EnumerateFiles(dir, "*.dll"))
                    {
                        if (!extra.Contains(f) && !candidates.Contains(f))
                            extra.Add(f);
                    }
                    foreach (var f in Directory.EnumerateFiles(dir, "*.exe"))
                    {
                        if (!extra.Contains(f) && !candidates.Contains(f))
                            extra.Add(f);
                    }
                }
                catch { }
            }

            foreach (var f in extra)
            {
                try
                {
                    if (!handle.IsNil && ProbeAssemblyForHandle(f, handle))
                        return f;

                    if (!string.IsNullOrEmpty(symbolName) && ProbeAssemblyForSymbolName(f, symbolName))
                        return f;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    public async System.Threading.Tasks.Task<string?> ResolveAssemblyForHandleAsync(EntityHandle handle, string? simpleAssemblyName = null, string? symbolName = null, Guid? mvid = null, IProgress<string>? progress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        if (handle.IsNil && string.IsNullOrEmpty(symbolName))
            return null;

        progress?.Report("Collecting candidates...");

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(simpleAssemblyName))
        {
            candidates.AddRange(ResolveAssemblyCandidates(simpleAssemblyName!, mvid));
        }

        foreach (var p in GetPersistedAssemblyFiles())
        {
            if (!candidates.Contains(p) && File.Exists(p))
                candidates.Add(p);
        }

        foreach (var a in assemblyList.GetAssemblies())
        {
            try
            {
                var fn = a.FileName;
                if (!string.IsNullOrEmpty(fn) && File.Exists(fn) && !candidates.Contains(fn))
                    candidates.Add(fn);
            }
            catch { }
        }

        // Prefer MVID exact matches
        if (mvid.HasValue && candidates.Count > 0)
        {
            var mvidMatches = new List<string>();
            foreach (var file in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var fs = File.OpenRead(file);
                    using var pe = new PEReader(fs, PEStreamOptions.Default);
                    if (!pe.HasMetadata)
                        continue;
                    var reader = pe.GetMetadataReader();
                    var guid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
                    if (guid == mvid.Value)
                        mvidMatches.Add(file);
                }
                catch { }
            }

            if (mvidMatches.Count > 0)
            {
                foreach (var f in mvidMatches)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"Probing {f} (MVID match)...");
                    try
                    {
                        if (!handle.IsNil && await ProbeAssemblyForHandleAsync(f, handle, progress, cancellationToken).ConfigureAwait(false))
                            return f;

                        if (!string.IsNullOrEmpty(symbolName) && await ProbeAssemblyForSymbolNameAsync(f, symbolName, progress, cancellationToken).ConfigureAwait(false))
                            return f;
                    }
                    catch { }
                }
            }
        }

        // Probe candidate list
        foreach (var f in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Probing {f}...");
            try
            {
                if (!handle.IsNil && await ProbeAssemblyForHandleAsync(f, handle, progress, cancellationToken).ConfigureAwait(false))
                    return f;

                if (!string.IsNullOrEmpty(symbolName) && await ProbeAssemblyForSymbolNameAsync(f, symbolName, progress, cancellationToken).ConfigureAwait(false))
                    return f;
            }
            catch { }
        }

        // broad scan in sibling directories
        try
        {
            var extra = new List<string>();
            foreach (var known in candidates.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var dir = Path.GetDirectoryName(known);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        continue;

                    foreach (var ff in Directory.EnumerateFiles(dir, "*.dll"))
                    {
                        if (!extra.Contains(ff) && !candidates.Contains(ff))
                            extra.Add(ff);
                    }
                    foreach (var ff in Directory.EnumerateFiles(dir, "*.exe"))
                    {
                        if (!extra.Contains(ff) && !candidates.Contains(ff))
                            extra.Add(ff);
                    }
                }
                catch { }
            }

            foreach (var ff in extra)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Probing {ff} (broad scan)...");
                try
                {
                    if (!handle.IsNil && await ProbeAssemblyForHandleAsync(ff, handle, progress, cancellationToken).ConfigureAwait(false))
                        return ff;

                    if (!string.IsNullOrEmpty(symbolName) && await ProbeAssemblyForSymbolNameAsync(ff, symbolName, progress, cancellationToken).ConfigureAwait(false))
                        return ff;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    public async System.Threading.Tasks.Task<bool> ProbeAssemblyForSymbolNameAsync(string filePath, string symbolName, IProgress<string>? progress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        return await System.Threading.Tasks.Task.Run(() => ProbeAssemblyForSymbolName(filePath, symbolName), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Probe a file for a symbol by textual name. The probe checks type definitions, exported type forwards,
    /// and simple member name matches (methods/fields/properties/events).
    /// This is a best-effort heuristic and may produce false positives for generic or overloaded members.
    /// </summary>
    private bool ProbeAssemblyForSymbolName(string filePath, string symbolName)
    {
        if (string.IsNullOrEmpty(symbolName) || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var fs = File.OpenRead(filePath);
            using var pe = new PEReader(fs, PEStreamOptions.Default);
            if (!pe.HasMetadata)
                return false;
            var reader = pe.GetMetadataReader();

            // Normalize symbolName: allow both full name (Namespace.Type) and short name
            var shortName = symbolName.Contains('.') ? symbolName.Substring(symbolName.LastIndexOf('.') + 1) : symbolName;

            // Check TypeDefinitions (type name match)
            foreach (var tdHandle in reader.TypeDefinitions)
            {
                var td = reader.GetTypeDefinition(tdHandle);
                var name = reader.GetString(td.Name);
                if (string.Equals(name, shortName, StringComparison.Ordinal))
                    return true;
                var ns = reader.GetString(td.Namespace);
                if (!string.IsNullOrEmpty(ns) && string.Equals(ns + "." + name, symbolName, StringComparison.Ordinal))
                    return true;
            }

            // Check exported types (type forwards)
            foreach (var etHandle in reader.ExportedTypes)
            {
                var et = reader.GetExportedType(etHandle);
                var name = reader.GetString(et.Name);
                var ns = reader.GetString(et.Namespace);
                if (string.Equals(name, shortName, StringComparison.Ordinal))
                    return true;
                if (!string.IsNullOrEmpty(ns) && string.Equals(ns + "." + name, symbolName, StringComparison.Ordinal))
                    return true;
            }

            // Member-level probes with improved signature checks
            // For methods, compare name and parameter count when a signature hint is present like "TypeName.MethodName(paramCount)"
            int? hintedParamCount = null;
            string hintedTypePrefix = null;
            // Try parse hints like "Namespace.Type.Method" or "Type.Method(2)"
            var paramStart = symbolName.IndexOf('(');
            if (paramStart >= 0)
            {
                var paramEnd = symbolName.IndexOf(')', paramStart + 1);
                if (paramEnd > paramStart)
                {
                    var between = symbolName.Substring(paramStart + 1, paramEnd - paramStart - 1);
                    if (int.TryParse(between.Trim(), out var pc))
                        hintedParamCount = pc;
                }
            }

            // Optionally extract type prefix (e.g., "Namespace.Type.") to prioritize methods on that type
            var lastDot = symbolName.LastIndexOf('.');
            if (lastDot > 0)
            {
                hintedTypePrefix = symbolName.Substring(0, lastDot);
            }

            foreach (var tdHandle in reader.TypeDefinitions)
            {
                var td = reader.GetTypeDefinition(tdHandle);
                var typeName = reader.GetString(td.Name);
                var typeNs = reader.GetString(td.Namespace);
                var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : typeNs + "." + typeName;

                // If hint provided, skip types that don't match
                if (!string.IsNullOrEmpty(hintedTypePrefix) && !string.Equals(fullTypeName, hintedTypePrefix, StringComparison.Ordinal) && !fullTypeName.StartsWith(hintedTypePrefix + "+", StringComparison.Ordinal))
                    continue;

                // Methods
                foreach (var mh in td.GetMethods())
                {
                    var md = reader.GetMethodDefinition(mh);
                    var mname = reader.GetString(md.Name);
                    if (!string.Equals(mname, shortName, StringComparison.Ordinal) && !string.Equals(mname, symbolName, StringComparison.Ordinal))
                        continue;

                    if (hintedParamCount.HasValue)
                    {
                        try
                        {
                            // Use parameter declarations table to estimate parameter count
                            var pcount = md.GetParameters().Count;
                            if (pcount == hintedParamCount.Value)
                                return true;
                        }
                        catch
                        {
                            // fallback to name-only match on error
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }

                // Fields
                foreach (var fh in td.GetFields())
                {
                    var fd = reader.GetFieldDefinition(fh);
                    var fname = reader.GetString(fd.Name);
                    if (string.Equals(fname, symbolName, StringComparison.Ordinal) || string.Equals(fname, shortName, StringComparison.Ordinal))
                        return true;
                }

                // Properties
                foreach (var ph in td.GetProperties())
                {
                    var pd = reader.GetPropertyDefinition(ph);
                    var pname = reader.GetString(pd.Name);
                    if (string.Equals(pname, symbolName, StringComparison.Ordinal) || string.Equals(pname, shortName, StringComparison.Ordinal))
                        return true;
                }

                // Events
                foreach (var eh in td.GetEvents())
                {
                    var ed = reader.GetEventDefinition(eh);
                    var ename = reader.GetString(ed.Name);
                    if (string.Equals(ename, symbolName, StringComparison.Ordinal) || string.Equals(ename, shortName, StringComparison.Ordinal))
                        return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
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
