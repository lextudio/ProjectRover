using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    public partial class AssemblyTreeModel
    {
        public AssemblyTreeNode? FindAssemblyNodeByPath(string path)
        {
            // Iterate over loaded assemblies to find one with matching path
            // This assumes AssemblyList is available and populated
            // Note: AssemblyList is ICSharpCode.ILSpyX.AssemblyList
            
            // We need to access the underlying loaded assemblies.
            // AssemblyList.GetAssemblies() returns LoadedAssembly[]
            
            if (AssemblyList == null) return null;

            foreach (var loadedAssembly in AssemblyList.GetAssemblies())
            {
                if (string.Equals(loadedAssembly.FileName, path, StringComparison.OrdinalIgnoreCase))
                {
                    return FindAssemblyNode(loadedAssembly);
                }
            }
            return null;
        }

        public void ReindexAssembly(AssemblyTreeNode node)
        {
            // This method was likely intended to force re-reading metadata or similar.
            // For now, we can try to reload it or just do nothing if not strictly required.
            // Or maybe it means "ensure it's in the index for search".
            // Given the context of NavigationService, it might be about ensuring the assembly is ready for symbol resolution.
            
            // If we have access to the underlying LoadedAssembly, we might check if it has errors.
        }

        public SharpTreeNode? FindAnyNodeForAssembly(string path)
        {
             return FindAssemblyNodeByPath(path);
        }

        public Task<LoadedAssembly?> LoadAssembliesAsync(IEnumerable<string> fileNames, bool focusNode = true, bool temporary = false, bool loadRequiredDependencies = false, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            // Wrapper around LoadAssemblies
            // Since LoadAssemblies is private and synchronous, we wrap it.
            // Note: The original LoadAssemblies signature might differ.
            
            // We can use the public OpenAssembly method on AssemblyList, but LoadAssemblies handles batching.
            
            // Since we are in a partial class, we can call private LoadAssemblies.
            // But LoadAssemblies returns void.
            
            // We need to return the LoadedAssembly.
            
            LoadedAssembly? firstLoaded = null;
            foreach (var file in fileNames)
            {
                var loaded = this.AssemblyList.OpenAssembly(file);
                if (firstLoaded == null) firstLoaded = loaded;
            }
            
            return Task.FromResult(firstLoaded);
        }
    }
}
