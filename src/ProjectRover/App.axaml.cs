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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProjectRover.Extensions;
using ProjectRover.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Composition.Convention;
using TomsToolbox.Composition;
using TomsToolbox.Composition.MicrosoftExtensions;
using ICSharpCode.ILSpyX.Analyzers;
using TomsToolbox.Composition;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using ICSharpCode.ILSpy.Views;
using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpyX.TreeView;

namespace ProjectRover;

public partial class App : Application
{
    public new static App Current => (App)Application.Current!;

    public IServiceProvider Services { get; private set; } = null!;
    public object? CompositionHost { get; private set; }
    public static IExportProvider? ExportProvider { get; private set; }

    public static CommandLineArguments CommandLineArguments { get; private set; } = CommandLineArguments.Create(Array.Empty<string>()); // TODO:
    internal static readonly IList<ExceptionData> StartupExceptions = new List<ExceptionData>(); // TODO:

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = CreateServiceCollection();

            // Initialize SettingsService
            var settingsService = new ICSharpCode.ILSpy.Util.SettingsService();
            services.AddSingleton(settingsService);

            // Bind exports from assemblies
            // ILSpyX
            Console.WriteLine("Binding exports from ILSpyX...");
            services.BindExports(typeof(IAnalyzer).Assembly);
            // ILSpy (Original)
            // NOTE: Do not bind the original ILSpy assembly here. Many ILSpy source files
            // are linked into the shim (executing) assembly; binding both the original
            // ILSpy assembly and the shim causes duplicate MEF exports and duplicate
            // menu entries. The shim's executing assembly is bound below.
            Console.WriteLine("Skipping binding of the original ILSpy assembly to avoid duplicate exports.");
            // ILSpy.Shims (Rover)
            Console.WriteLine("Binding exports from ILSpy.Shims...");
            services.BindExports(Assembly.GetExecutingAssembly());

            // Add the export provider (circular dependency resolution via factory)
            services.AddSingleton<IExportProvider>(sp => ExportProvider!);

            Console.WriteLine("Building ServiceProvider...");
            var serviceProvider = services.BuildServiceProvider();
            Services = serviceProvider;

            // Create the adapter
            Console.WriteLine("Creating ExportProviderAdapter...");
            ExportProvider = new ExportProviderAdapter(serviceProvider);

            Console.WriteLine($"ExportProvider initialized: {ExportProvider != null}");

            Console.WriteLine("Creating MainWindow...");
            desktop.MainWindow = Services.GetRequiredService<ICSharpCode.ILSpy.MainWindow>();
            Console.WriteLine("MainWindow created.");

            // Register command bindings
            ICSharpCode.ILSpy.CommandWrapper.RegisterBindings(desktop.MainWindow);

            // Diagnostic: attach to AssemblyTreeModel export when available and watch Root.Children
            try
            {
                _ = AttachAssemblyTreeDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Startup] Failed to start assembly diagnostics: " + ex);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    // Diagnostic helper: wait for AssemblyTreeModel export and attach to Root.Children changes
    private static async System.Threading.Tasks.Task AttachAssemblyTreeDiagnosticsAsync()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                var model = ExportProvider?.GetExportedValueOrDefault<ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel>();
                if (model != null)
                {
                    System.Diagnostics.Debug.WriteLine("[Startup] AssemblyTreeModel export found.");
                    AttachModelDiagnostics(model);
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Startup] Error retrieving AssemblyTreeModel: " + ex);
            }
            await System.Threading.Tasks.Task.Delay(200);
        }
        System.Diagnostics.Debug.WriteLine("[Startup] AssemblyTreeModel export not found after polling.");
    }

    private static void AttachModelDiagnostics(ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel model)
    {
        try
        {
            model.PropertyChanged += (s, e) => {
                if (e.PropertyName == "Root")
                {
                    System.Diagnostics.Debug.WriteLine("[Startup] AssemblyTreeModel.Root changed.");
                    SubscribeToRoot(model.Root);
                }
            };

            if (model.Root != null)
            {
                System.Diagnostics.Debug.WriteLine("[Startup] AssemblyTreeModel.Root already set.");
                SubscribeToRoot(model.Root);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[Startup] AttachModelDiagnostics failed: " + ex);
        }
    }

    private static void SubscribeToRoot(SharpTreeNode? root)
    {
        try
        {
            if (root == null)
                return;
            System.Diagnostics.Debug.WriteLine($"[Startup] Root has {root.Children.Count} children.");
            if (root.Children is System.Collections.Specialized.INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) => {
                    System.Diagnostics.Debug.WriteLine($"[Startup] Root.Children changed: action={e.Action}, newCount={root.Children.Count}");
                    if (e.NewItems != null)
                    {
                        foreach (var ni in e.NewItems)
                        {
                            if (ni is ICSharpCode.ILSpy.TreeNodes.AssemblyTreeNode atn)
                            {
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Startup] New assembly node: {atn.LoadedAssembly?.ShortName}");
                                    atn.EnsureLazyChildren();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine("[Startup] EnsureLazyChildren failed in CollectionChanged: " + ex);
                                }
                            }
                        }
                    }
                };
            }

            foreach (var node in root.Children.OfType<ICSharpCode.ILSpy.TreeNodes.AssemblyTreeNode>())
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Startup] Forcing EnsureLazyChildren for existing node: {node.LoadedAssembly?.ShortName}");
                    node.EnsureLazyChildren();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[Startup] EnsureLazyChildren failed for existing node: " + ex);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[Startup] SubscribeToRoot failed: " + ex);
        }
    }

    private static IServiceCollection CreateServiceCollection() =>
        new ServiceCollection()
            .ConfigureOptions()
            .ConfigureLogging()
            .AddViews()
            .AddViewModels()
            .AddServices();

    // Small adapter so existing ILSpy code can call GetExportedValue<T>() against a provider.
    class CompositionHostExportProvider : IExportProvider
    {
        private readonly CompositionHost _host;
        private readonly IServiceProvider _services;

        public CompositionHostExportProvider(CompositionHost host, IServiceProvider services)
        {
            _host = host;
            _services = services;
        }

        public event EventHandler<EventArgs>? ExportsChanged;

        public T GetExportedValue<T>()
        {
            if (_host.TryGetExport<T>(out var export))
                return export;

            // Fallback to service provider
            var svc = (T?)_services.GetService(typeof(T));
            if (svc != null)
                return svc;
            throw new InvalidOperationException($"Export not found: {typeof(T).FullName}");
        }

        public T GetExportedValue<T>(string? contractName = null) where T : class
        {
            if (_host.TryGetExport<T>(contractName, out var export))
                return export;

            if (contractName == null)
            {
                var svc = (T?)_services.GetService(typeof(T));
                if (svc != null)
                    return svc;
            }
            throw new InvalidOperationException($"Export not found: {typeof(T).FullName} (contract: {contractName})");
        }

        public T? GetExportedValueOrDefault<T>(string? contractName = null) where T : class
        {
            if (_host.TryGetExport<T>(contractName, out var export))
                return export;

            if (contractName == null)
            {
                var svc = (T?)_services.GetService(typeof(T));
                if (svc != null)
                    return svc;
            }
            return default;
        }

        public T[] GetExportedValues<T>()
        {
            return System.Linq.Enumerable.ToArray(_host.GetExports<T>());
        }

        public IEnumerable<T> GetExportedValues<T>(string? contractName = null) where T : class
        {
            return _host.GetExports<T>(contractName);
        }

        public IEnumerable<object> GetExportedValues(Type contractType, string? contractName = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IExport<object>> GetExports(Type contractType, string? contractName = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IExport<T>> GetExports<T>(string? contractName = null) where T : class
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IExport<T, TMetadataView>> GetExports<T, TMetadataView>(string? contractName = null)
            where T : class
            where TMetadataView : class
        {
            throw new NotImplementedException();
        }

        public bool TryGetExportedValue<T>(string? contractName, [NotNullWhen(true)] out T? value) where T : class
        {
            throw new NotImplementedException();
        }
    }

    // Small holder type that can be discovered by MEF consumers if they import IServiceProvider
    // We keep this type internal to avoid adding new public API surface.
    class ProjectRoverExportedServiceProvider
    {
        private readonly IServiceProvider _provider;

        public ProjectRoverExportedServiceProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public object GetService(Type serviceType) => _provider.GetService(serviceType)!;
    }
}
