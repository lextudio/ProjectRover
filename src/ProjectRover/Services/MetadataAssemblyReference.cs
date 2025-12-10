using System;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;

namespace ProjectRover.Services;

internal class MetadataAssemblyReference : IAssemblyReference
{
    public MetadataAssemblyReference(System.Reflection.Metadata.AssemblyReference reference, MetadataReader reader)
    {
        Name = reader.GetString(reference.Name);
        Culture = reference.Culture.IsNil ? null : reader.GetString(reference.Culture);
        Version = reference.Version;
        PublicKeyToken = reference.PublicKeyOrToken.IsNil ? null : reader.GetBlobBytes(reference.PublicKeyOrToken);
        var flags = (int)reference.Flags;
        IsWindowsRuntime = (flags & 0x200) != 0; // AssemblyFlags.WindowsRuntime
        IsRetargetable = (flags & 0x100) != 0; // AssemblyFlags.Retargetable
    }

    public string Name { get; }
    public string FullName => Name;
    public Version? Version { get; }
    public string? Culture { get; }
    public byte[]? PublicKeyToken { get; }
    public bool IsWindowsRuntime { get; }
    public bool IsRetargetable { get; }
}
