using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.TreeView;
using ICSharpCode.ILSpyX.TreeView.PlatformAbstractions;
using ProjectRover.Nodes;

namespace ICSharpCode.ILSpy.TreeNodes
{
    /// <summary>
    /// Avalonia-adapted port of ILSpy's AssemblyTreeNode.
    /// This file replaces WPF UI bits with platform abstractions available in the workspace.
    /// It focuses on compiling and preserving non-visual behavior used by the rest of the codebase.
    /// </summary>
    public sealed class AssemblyTreeNode : ILSpyTreeNode
    {
        readonly Dictionary<string, NamespaceTreeNode> namespaces = new Dictionary<string, NamespaceTreeNode>();
        readonly Dictionary<System.Reflection.Metadata.TypeDefinitionHandle, TypeTreeNode> typeDict = new Dictionary<System.Reflection.Metadata.TypeDefinitionHandle, TypeTreeNode>();
        ICompilation typeSystem;

        public AssemblyTreeNode(LoadedAssembly assembly) : this(assembly, null)
        {
        }

        internal AssemblyTreeNode(LoadedAssembly assembly, PackageEntry packageEntry)
        {
            this.LoadedAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            this.LazyLoading = true;
            this.PackageEntry = packageEntry;
            Init();
        }

        public ILSpyX.AssemblyList AssemblyList {
            get { return LoadedAssembly.AssemblyList; }
        }

        public LoadedAssembly LoadedAssembly { get; }

        public PackageEntry PackageEntry { get; }

        public override bool IsAutoLoaded => LoadedAssembly.IsAutoLoaded;

        public override object Text => LoadedAssembly.Text;

        public override object Icon
        {
            get
            {
                // Simplified: use same images provider as upstream if available.
                if (LoadedAssembly.IsLoaded)
                {
                    if (LoadedAssembly.HasLoadError)
                        return Images.AssemblyWarning;
                    var loadResult = LoadedAssembly.GetLoadResultAsync().GetAwaiter().GetResult();
                    if (loadResult.MetadataFile != null)
                    {
                        switch (loadResult.MetadataFile.Kind)
                        {
                            case MetadataFile.MetadataFileKind.PortableExecutable:
                            case MetadataFile.MetadataFileKind.WebCIL:
                                return Images.Assembly;
                            default:
                                return Images.MetadataFile;
                        }
                    }
                    else
                    {
                        return Images.FindAssembly;
                    }
                }
                else
                {
                    return Images.FindAssembly;
                }
            }
        }

        object tooltip;

        public override object ToolTip
        {
            get
            {
                if (LoadedAssembly.HasLoadError)
                    return "Assembly could not be loaded. Click here for details.";

                if (tooltip == null && LoadedAssembly.IsLoaded)
                {
                    var module = LoadedAssembly.GetMetadataFileOrNull();
                    var metadata = module?.Metadata;
                    var lines = new List<string>();
                    if (metadata?.IsAssembly == true && metadata.TryGetFullAssemblyName(out var assemblyName))
                    {
                        lines.Add($"Name: {assemblyName}");
                    }
                    lines.Add($"Location: {LoadedAssembly.FileName}");
                    if (module != null)
                    {
                        if (module is PEFile peFile)
                        {
                            lines.Add($"Architecture: {Language.GetPlatformDisplayName(peFile)}");
                        }
                        string runtimeName = Language.GetRuntimeDisplayName(module);
                        if (runtimeName != null)
                        {
                            lines.Add($"Runtime: {runtimeName}");
                        }
                        var debugInfo = LoadedAssembly.GetDebugInfoOrNull();
                        lines.Add($"Debug info: {debugInfo?.Description ?? "none"}");
                    }
                    tooltip = string.Join("\n", lines);
                }
                return tooltip;
            }
        }

        public void UpdateToolTip()
        {
            tooltip = null;
            RaisePropertyChanged(nameof(ToolTip));
        }

        public override bool ShowExpander => !LoadedAssembly.HasLoadError;

        async void Init()
        {
            try
            {
                await this.LoadedAssembly.GetLoadResultAsync();
                RaisePropertyChanged(nameof(Text));
            }
            catch
            {
                RaisePropertyChanged(nameof(ShowExpander));
            }
            RaisePropertyChanged(nameof(Icon));
            RaisePropertyChanged(nameof(ExpandedIcon));
            RaisePropertyChanged(nameof(ToolTip));
        }

        protected override void LoadChildren()
        {
            LoadResult loadResult;
            try
            {
                loadResult = LoadedAssembly.GetLoadResultAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return;
            }
            try
            {
                if (loadResult.MetadataFile != null)
                {
                    switch (loadResult.MetadataFile.Kind)
                    {
                        case MetadataFile.MetadataFileKind.PortableExecutable:
                        case MetadataFile.MetadataFileKind.WebCIL:
                            LoadChildrenForExecutableFile(loadResult.MetadataFile);
                            break;
                        default:
                            var metadata = loadResult.MetadataFile;
                            this.Children.Add(new MetadataTablesTreeNode(metadata));
                            this.Children.Add(new StringHeapTreeNode(metadata));
                            this.Children.Add(new UserStringHeapTreeNode(metadata));
                            this.Children.Add(new GuidHeapTreeNode(metadata));
                            this.Children.Add(new BlobHeapTreeNode(metadata));
                            break;
                    }
                }
                else if (loadResult.Package != null)
                {
                    var package = loadResult.Package;
                    this.Children.AddRange(PackageFolderTreeNode.LoadChildrenForFolder(package.RootFolder));
                }
            }
            catch (Exception ex)
            {
                // TODO: App.UnhandledException(ex);
            }
        }

        void LoadChildrenForExecutableFile(MetadataFile module)
        {
            typeSystem = LoadedAssembly.GetTypeSystemOrNull();
            var assembly = (MetadataModule)typeSystem.MainModule;
            this.Children.Add(new MetadataTreeNode(module, Resources.Metadata));
            Decompiler.DebugInfo.IDebugInfoProvider debugInfo = LoadedAssembly.GetDebugInfoOrNull();
            if (debugInfo is Decompiler.DebugInfo.PortableDebugInfoProvider ppdb
                && ppdb.GetMetadataReader() is System.Reflection.Metadata.MetadataReader reader)
            {
                this.Children.Add(new MetadataTreeNode(ppdb.ToMetadataFile(), $"Debug Metadata ({(ppdb.IsEmbedded ? "Embedded" : "From portable PDB")})"));
            }
            this.Children.Add(new ReferenceFolderTreeNode(module, this));
            if (module.Resources.Any())
                this.Children.Add(new ResourceListTreeNode(module));
            foreach (NamespaceTreeNode ns in namespaces.Values)
            {
                ns.Children.Clear();
            }
            namespaces.Clear();
            bool useNestedStructure = SettingsService.DisplaySettings.UseNestedNamespaceNodes;
            foreach (var type in assembly.TopLevelTypeDefinitions.OrderBy(t => t.ReflectionName, NaturalStringComparer.Instance))
            {
                var ns = GetOrCreateNamespaceTreeNode(type.Namespace);
                TypeTreeNode node = new TypeTreeNode(type, this);
                typeDict[(System.Reflection.Metadata.TypeDefinitionHandle)type.MetadataToken] = node;
                ns.Children.Add(node);
            }
            foreach (NamespaceTreeNode ns in namespaces.Values
                .Where(ns => ns.Children.Count > 0 && ns.Parent == null)
                .OrderBy(n => n.Name, NaturalStringComparer.Instance))
            {
                this.Children.Add(ns);
                SetPublicAPI(ns);
            }

            NamespaceTreeNode GetOrCreateNamespaceTreeNode(string @namespace)
            {
                if (!namespaces.TryGetValue(@namespace, out NamespaceTreeNode ns))
                {
                    if (useNestedStructure)
                    {
                        int decimalIndex = @namespace.LastIndexOf('.');
                        if (decimalIndex < 0)
                        {
                            var escapedNamespace = Language.EscapeName(@namespace);
                            ns = new NamespaceTreeNode(escapedNamespace);
                        }
                        else
                        {
                            var parentNamespaceTreeNode = GetOrCreateNamespaceTreeNode(@namespace.Substring(0, decimalIndex));
                            var escapedInnerNamespace = Language.EscapeName(@namespace.Substring(decimalIndex + 1));
                            ns = new NamespaceTreeNode(escapedInnerNamespace);
                            parentNamespaceTreeNode.Children.Add(ns);
                        }
                    }
                    else
                    {
                        var escapedNamespace = Language.EscapeName(@namespace);
                        ns = new NamespaceTreeNode(escapedNamespace);
                    }
                    namespaces.Add(@namespace, ns);
                }
                return ns;
            }
        }

        private static void SetPublicAPI(NamespaceTreeNode ns)
        {
            foreach (NamespaceTreeNode innerNamespace in ns.Children.OfType<NamespaceTreeNode>())
            {
                SetPublicAPI(innerNamespace);
            }
            ns.SetPublicAPI(ns.Children.OfType<ILSpyTreeNode>().Any(n => n.IsPublicAPI));
        }

        public TypeTreeNode FindTypeNode(ITypeDefinition type)
        {
            if (type == null)
                return null;
            EnsureLazyChildren();
            TypeTreeNode node;
            if (typeDict.TryGetValue((System.Reflection.Metadata.TypeDefinitionHandle)type.MetadataToken, out node))
                return node;
            else
                return null;
        }

        public NamespaceTreeNode FindNamespaceNode(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return null;
            EnsureLazyChildren();
            NamespaceTreeNode node;
            if (namespaces.TryGetValue(namespaceName, out node))
                return node;
            else
                return null;
        }

        public override bool CanDrag(SharpTreeNode[] nodes)
        {
            return nodes.All(n => n is AssemblyTreeNode { PackageEntry: null });
        }

        public override void StartDrag(object dragSource, SharpTreeNode[] nodes, IPlatformDragDrop dragdropManager)
        {
            dragdropManager.DoDragDrop(dragSource, Copy(nodes), XPlatDragDropEffects.All);
        }

        public override bool CanDelete()
        {
            return PackageEntry == null;
        }

        public override void Delete()
        {
            DeleteCore();
        }

        public override void DeleteCore()
        {
            LoadedAssembly.AssemblyList.Unload(LoadedAssembly);
        }

        internal const string DataFormat = "ILSpyAssemblies";

        public override IPlatformDataObject Copy(SharpTreeNode[] nodes)
        {
            //TODO: var dataObject = new WpfWindowsDataObject(new System.Windows.IDataObjectStub());
            //dataObject.SetData(DataFormat, nodes.OfType<AssemblyTreeNode>().Select(n => n.LoadedAssembly.FileName).ToArray());
            return null; //dataObject;
        }

        public override FilterResult Filter(LanguageSettings settings)
        {
            if (settings.SearchTermMatches(LoadedAssembly.ShortName))
                return FilterResult.Match;
            else
                return FilterResult.Recurse;
        }

        public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
        {
            void HandleException(Exception ex, string message)
            {
                language.WriteCommentLine(output, message);

                output.WriteLine();
                output.MarkFoldStart("Exception details", true);
                output.Write(ex.ToString());
                output.MarkFoldEnd();
            }

            try
            {
                var loadResult = LoadedAssembly.GetLoadResultAsync().GetAwaiter().GetResult();
                if (loadResult.MetadataFile != null)
                {
                    switch (loadResult.MetadataFile.Kind)
                    {
                        case MetadataFile.MetadataFileKind.ProgramDebugDatabase:
                        case MetadataFile.MetadataFileKind.Metadata:
                            output.WriteLine("// " + LoadedAssembly.FileName);
                            break;
                        default:
                            language.DecompileAssembly(LoadedAssembly, output, options);
                            break;
                    }
                }
                else if (loadResult.Package != null)
                {
                    output.WriteLine("// " + LoadedAssembly.FileName);
                    DecompilePackage(loadResult.Package, output);
                }
                else if (loadResult.FileLoadException != null)
                {
                    HandleException(loadResult.FileLoadException, loadResult.FileLoadException.Message);
                }
            }
            catch (BadImageFormatException badImage)
            {
                HandleException(badImage, "This file does not contain a managed assembly.");
            }
            catch (FileNotFoundException fileNotFound) when (options.SaveAsProjectDirectory == null)
            {
                HandleException(fileNotFound, "The file was not found.");
            }
            catch (DirectoryNotFoundException dirNotFound) when (options.SaveAsProjectDirectory == null)
            {
                HandleException(dirNotFound, "The directory was not found.");
            }
            catch (MetadataFileNotSupportedException notSupported)
            {
                HandleException(notSupported, notSupported.Message);
            }
        }

        private void DecompilePackage(LoadedPackage package, ITextOutput output)
        {
            switch (package.Kind)
            {
                case LoadedPackage.PackageKind.Zip:
                    output.WriteLine("// File format: .zip file");
                    break;
                case LoadedPackage.PackageKind.Bundle:
                    var header = package.BundleHeader;
                    output.WriteLine($"// File format: .NET bundle {header.MajorVersion}.{header.MinorVersion}");
                    break;
            }
            output.WriteLine();
            output.WriteLine("Entries:");
            foreach (var entry in package.Entries)
            {
                output.WriteLine($" {entry.Name} ({entry.TryGetLength()} bytes)");
            }
        }

        public override bool Save(TabPageModel tabPage)
        {
            if (!LoadedAssembly.IsLoadedAsValidAssembly)
                return false;
            Language language = this.Language;
            if (string.IsNullOrEmpty(language.ProjectFileExtension))
                return false;
            // TODO:
            /*
            var dlg = DialogProvider.CreateSaveFileDialog();
            dlg.FileName = WholeProjectDecompiler.CleanUpFileName(LoadedAssembly.ShortName, language.ProjectFileExtension);
            dlg.Filter = language.Name + " project|*" + language.ProjectFileExtension + "|" + language.Name + " single file|*" + language.FileExtension + "|All files|*.*";
            if (dlg.ShowDialog() == true)
            {
                var options = DockWorkspace.ActiveTabPage.CreateDecompilationOptions();
                options.FullDecompilation = true;
                if (dlg.FilterIndex == 1)
                {
                    options.SaveAsProjectDirectory = Path.GetDirectoryName(dlg.FileName);
                    foreach (string entry in Directory.GetFileSystemEntries(options.SaveAsProjectDirectory))
                    {
                        if (!string.Equals(entry, dlg.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            // TODO:
                            // var result = DialogProvider.ShowMessageBox(Resources.AssemblySaveCodeDirectoryNotEmpty, Resources.AssemblySaveCodeDirectoryNotEmptyTitle);
                            // if (result == MessageBoxResult.No)
                            //     return true;
                            break;
                        }
                    }
                }
                tabPage.ShowTextView(textView => textView.SaveToDisk(language, new[] { this }, options, dlg.FileName));
            }
            */
            return true;
        }

        public override string ToString()
        {
            return LoadedAssembly.FileName;
        }
    }
}
