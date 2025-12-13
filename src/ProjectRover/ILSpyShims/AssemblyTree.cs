using System;
using System.Collections.Generic;
using ICSharpCode.ILSpyX;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    // Minimal placeholder types to satisfy references from linked ILSpy code.
    //public record LoadedAssembly(string FileName);

    public class AssemblyList
    {
        public static string DefaultListName => "Default";
    }

    public class LoadedAssemblyCollection : List<LoadedAssembly> { }
}
