using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    // Minimal shim of AssemblyTreeModel used by ProjectRover services.
    public class AssemblyTreeModel
    {
        public AssemblyTreeModel() { }

        public AssemblyList AssemblyList { get; private set; } = new AssemblyList();

        // Expose a static accessor like the real ILSpy code uses via App.ExportProvider
        // In the shim we provide a simple static instance for consumers that call
        // `App.ExportProvider.GetExportedValue<AssemblyTreeModel>()` or `AssemblyTreeModel`.
        public static AssemblyTreeModel Instance { get; } = new AssemblyTreeModel();

        // Language and version used by the decompilers. Keep nullable to match ILSpy.
        public ICSharpCode.ILSpy.Language? CurrentLanguage { get; set; }

        public ICSharpCode.ILSpyX.LanguageVersion? CurrentLanguageVersion { get; set; }

        // Selection helpers used by decompilation entry points.
        public IEnumerable<ICSharpCode.ILSpy.TreeNodes.ILSpyTreeNode> SelectedNodes
            => Array.Empty<ICSharpCode.ILSpy.TreeNodes.ILSpyTreeNode>();

        public void SelectNode(object node, bool inNewTabPage = false) { }

        public void RefreshDecompiledView() { }

        public void ReindexAssembly(object node) { }

        public object[] FindAnyNodeForAssembly(string path) => Array.Empty<object>();

        public IEnumerable<string> ResolveAssemblyCandidates(string simpleName, object? context) => Array.Empty<string>();

        public object LoadAssemblies(IEnumerable<string> paths, bool focusNode, bool somethingElse) => null!;

        public Task<object> LoadAssembliesAsync(IEnumerable<string> paths, bool a, bool b, bool c, object progress, System.Threading.CancellationToken cancellationToken) => Task.FromResult<object>(Array.Empty<object>());
    }
}
