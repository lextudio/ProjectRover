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
using ProjectRover.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Collections.Generic;
using System.Composition.Hosting;
using TomsToolbox.Composition;

namespace ProjectRover;

public partial class App : Application
{
    public new static App Current => (App)Application.Current!;
    
    public IServiceProvider Services { get; } = ConfigureServices();
    public object? CompositionHost { get; private set; }
    public IExportProvider? ExportProvider { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();

            // Initialize CompositionHost for MEF-style exports (compose ILSpy parts) using ContainerConfiguration
            try
            {
                var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
                try { assemblies.Add(Assembly.Load("ICSharpCode.ILSpyX")); } catch { }

                var config = new System.Composition.Hosting.ContainerConfiguration()
                    .WithAssemblies(assemblies);

                // Explicitly register our wrapper parts so they are discoverable
                config = config.WithParts(
                    typeof(ProjectRover.Services.ExportedIlSpyBackend),
                    typeof(ProjectRover.Services.ExportedServiceProvider),
                    typeof(ProjectRover.Services.ExportedMainWindowViewModel),
                    typeof(ProjectRover.Services.ExportedTabPageModel)
                );
                // Register language and settings shims
                config = config.WithParts(
                    typeof(ICSharpCode.ILSpy.LanguageService),
                    typeof(ICSharpCode.ILSpy.Util.SettingsService)
                );

                var container = config.CreateContainer();
                CompositionHost = container;
                ExportProvider = new CompositionHostExportProvider(container, Services);
            }
            catch
            {
                CompositionHost = null;
                ExportProvider = null;
            }

            // Exercise docking workspace once at startup (diagnostic)
            try
            {
                var dockWorkspace = Services.GetService<ICSharpCode.ILSpy.Docking.IDockWorkspace>();
                if (dockWorkspace != null)
                {
                    // Add a diagnostic tab and show some text
                    var doc = dockWorkspace.AddTabPage(null);
                    dockWorkspace.ShowText("ProjectRover: diagnostic tab created at startup.");
                }
            }
            catch
            {
                // swallow diagnostic errors
            }

                // Runtime MEF diagnostics: try to resolve the exported IlSpy backend wrapper
                try
                {
                    if (ExportProvider != null)
                    {
                        try
                        {
                            var exportedBackend = ExportProvider.GetExportedValue<ProjectRover.Services.ExportedIlSpyBackend>();
                            if (exportedBackend != null && exportedBackend.Backend != null)
                            {
                                Console.WriteLine("MEF: Resolved ExportedIlSpyBackend via ExportProvider.");
                                try
                                {
                                    // Call a safe method to verify the backend is callable
                                    exportedBackend.Backend.Clear();
                                    Console.WriteLine("IlSpyBackend.Clear() invoked successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("IlSpyBackend.Clear failed: " + ex.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("MEF: ExportedIlSpyBackend not resolved via ExportProvider: " + ex.Message);
                        }
                    }
                }
                catch
                {
                    // swallow diagnostics
                }

            desktop.ShutdownRequested += (_, _) =>
            {
                var analyticsService = Services.GetRequiredService<IAnalyticsService>();
                try
                {
                    analyticsService.TrackEvent(AnalyticsEvents.Shutdown);
                }
                catch { }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private static IServiceProvider ConfigureServices() =>
        new ServiceCollection()
            .ConfigureOptions()
            .ConfigureLogging()
            .AddViews()
            .AddViewModels()
            .AddServices()
            .AddProviders()
            .AddHttpClients()
            .BuildServiceProvider();

    private void About_OnClick(object? sender, EventArgs e)
    {
        _ = Services.GetRequiredService<IAnalyticsService>().TrackEventAsync(AnalyticsEvents.About);
        Services.GetRequiredService<IDialogService>().ShowDialog<AboutDialog>();
    }

    // Small adapter so existing ILSpy code can call GetExportedValue<T>() against a provider.
    class CompositionHostExportProvider : IExportProvider
    {
        private readonly object _host;
        private readonly IServiceProvider _services;

        public CompositionHostExportProvider(object host, IServiceProvider services)
        {
            _host = host;
            _services = services;
        }

        public T GetExportedValue<T>()
        {
            try
            {
                // Prefer composition host via reflection
                var hgType = _host.GetType();
                var getExport = hgType.GetMethod("GetExport", BindingFlags.Instance | BindingFlags.Public);
                if (getExport != null && getExport.IsGenericMethodDefinition)
                {
                    var generic = getExport.MakeGenericMethod(typeof(T));
                    var result = generic.Invoke(_host, null);
                    if (result is T t)
                        return t;
                }
            }
            catch { }

            // Fallback to service provider
            var svc = (T?)_services.GetService(typeof(T));
            if (svc != null)
                return svc;
            throw new InvalidOperationException($"Export not found: {typeof(T).FullName}");
        }

        public T[] GetExportedValues<T>()
        {
            try
            {
                // Prefer composition host via reflection
                var hgType = _host.GetType();
                var getExports = hgType.GetMethod("GetExports", BindingFlags.Instance | BindingFlags.Public);
                if (getExports != null && getExports.IsGenericMethodDefinition)
                {
                    var generic = getExports.MakeGenericMethod(typeof(T));
                    var result = generic.Invoke(_host, null);
                    if (result is IEnumerable<T> enumerable)
                        return System.Linq.Enumerable.ToArray(enumerable);
                }
            }
            catch { }

            // Fallback to service provider
            var svcs = (IEnumerable<T>?)_services.GetService(typeof(IEnumerable<T>));
            if (svcs != null)
                return System.Linq.Enumerable.ToArray(svcs);
            return Array.Empty<T>();
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
