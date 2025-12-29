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
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using ICSharpCode.ILSpy.Views;
using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpyX.TreeView;
using ICSharpCode.ILSpy.Themes;
using TomsToolbox.Wpf.Composition;

namespace ProjectRover;

public partial class App : Application
{
    private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("App");
    public new static App Current => (App)Application.Current!;

    public IServiceProvider Services { get; private set; } = null!;
    public object? CompositionHost { get; private set; }
    public static IExportProvider? ExportProvider { get; private set; }

    public static CommandLineArguments CommandLineArguments { get; private set; } = CommandLineArguments.Create(Array.Empty<string>()); // TODO:
    internal static readonly IList<ExceptionData> StartupExceptions = new List<ExceptionData>(); // TODO:

    public override void Initialize()
    {
        Name = "Project Rover";
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataGridLog = ICSharpCode.ILSpy.Util.LogCategory.For("DataGrid");
            Avalonia.Controls.DataGrid.ScrollDiagnosticsLog = message => dataGridLog.Debug("{Message}", message);
            ProjectRover.Settings.RectTypeConverterRegistration.Ensure();
            var services = CreateServiceCollection();

            // Initialize SettingsService
            var settingsService = new ICSharpCode.ILSpy.Util.SettingsService();
            var desiredTheme = settingsService.SessionSettings.Theme;
            services.AddSingleton(settingsService);

            // Apply persisted UI culture early so resource managers probe satellite assemblies correctly
            try
            {
                var cultureName = settingsService.SessionSettings.CurrentCulture;
                if (!string.IsNullOrWhiteSpace(cultureName))
                {
                    var ci = new System.Globalization.CultureInfo(cultureName);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ci;
                    // Update generated strongly-typed resources culture so Properties.Resources picks the satellite
                    ICSharpCode.ILSpy.Properties.Resources.Culture = ci;
                    log.Information("Applied persisted UI culture: {Culture}", cultureName);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to apply persisted UI culture");
            }

            // Watch for runtime changes to the selected UI culture and apply them
            try
            {
                settingsService.SessionSettings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ICSharpCode.ILSpy.SessionSettings.CurrentCulture))
                    {
                        try
                        {
                            var name = settingsService.SessionSettings.CurrentCulture;
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                // Use system default
                                ICSharpCode.ILSpy.Properties.Resources.Culture = null;
                                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InstalledUICulture;
                            }
                            else
                            {
                                var ci2 = new System.Globalization.CultureInfo(name);
                                ICSharpCode.ILSpy.Properties.Resources.Culture = ci2;
                                System.Threading.Thread.CurrentThread.CurrentUICulture = ci2;
                                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ci2;
                            }
                            log.Information("UI culture changed to: {Culture}", settingsService.SessionSettings.CurrentCulture);
                        }
                        catch (Exception ex2)
                        {
                            log.Warning(ex2, "Failed to apply UI culture change");
                        }
                    }
                };
            }
            catch { }

            // ProjectRover-only sanitization: some persisted settings may contain
            // empty/whitespace assembly list names (causes an empty combobox entry).
            // We sanitize here in the port layer (without modifying ILSpy files).
            try
            {
                var asmLists = settingsService.AssemblyListManager.AssemblyLists;
                var empties = asmLists.Where(s => string.IsNullOrWhiteSpace(s)).ToList();
                if (empties.Count > 0)
                {
                    foreach (var e in empties)
                        asmLists.Remove(e);
                    log.Warning("Removed {Count} empty assembly list name(s) from settings", empties.Count);
                }

                var active = settingsService.SessionSettings.ActiveAssemblyList;
                if (string.IsNullOrWhiteSpace(active) || !asmLists.Contains(active))
                {
                    settingsService.SessionSettings.ActiveAssemblyList = ICSharpCode.ILSpyX.AssemblyListManager.DefaultListName;
                    log.Information("Reset SessionSettings.ActiveAssemblyList to default: {Default}", ICSharpCode.ILSpyX.AssemblyListManager.DefaultListName);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to sanitize assembly lists");
            }

            // Bind exports from assemblies
            // ILSpyX
                log.Information("Binding exports from ILSpyX...");
            services.BindExports(typeof(IAnalyzer).Assembly);
            // ILSpy (Original)
            // NOTE: Do not bind the original ILSpy assembly here. Many ILSpy source files
            // are linked into the shim (executing) assembly; binding both the original
            // ILSpy assembly and the shim causes duplicate MEF exports and duplicate
            // menu entries. The shim's executing assembly is bound below.
                log.Information("Skipping binding of the original ILSpy assembly to avoid duplicate exports.");
            // ILSpy.Shims (Rover)
                log.Information("Binding exports from ILSpy.Shims...");
            services.BindExports(Assembly.GetExecutingAssembly());

            // Add the export provider (circular dependency resolution via factory)
            services.AddSingleton<IExportProvider>(sp => ExportProvider!);

                log.Information("Building ServiceProvider...");
            var serviceProvider = services.BuildServiceProvider();
            Services = serviceProvider;

            // Create the adapter
                log.Information("Creating ExportProviderAdapter...");
            ExportProvider = new ExportProviderAdapter(serviceProvider);

            // Register the export provider as a global fallback and make it
            // available via the attached property on the visual tree.
            try
            {
                if (ExportProvider != null)
                {
                    ExportProviderLocator.Register(ExportProvider);
                        log.Information("ExportProviderLocator registered.");
                }
            }
            catch (Exception ex)
            {
                    log.Error(ex, "Failed to register ExportProviderLocator");
            }

                log.Information("ExportProvider initialized: {HasProvider}", ExportProvider != null);

                log.Information("Creating MainWindow...");
            desktop.MainWindow = Services.GetRequiredService<ICSharpCode.ILSpy.MainWindow>();
                log.Information("MainWindow created.");

            // Attach the export provider to the MainWindow so that inheritable
            // attached property lookup works for all visual children.
            try
            {
                ExportProviderLocator.SetExportProvider(desktop.MainWindow, ExportProvider);
                    log.Information("ExportProvider attached to MainWindow.");
            }
            catch (Exception ex)
            {
                    log.Error(ex, "Failed to attach ExportProvider to MainWindow");
            }

            desktop.MainWindow.Opened += async (_, _) =>
            {
                ThemeManager.Current.ApplyTheme(desiredTheme);
                try
                {
                    await ShowWarningIfILSpyRunning(desktop.MainWindow);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Warning dialog failed");
                }
            };

            // Register command bindings
            ICSharpCode.ILSpy.CommandWrapper.RegisterBindings(desktop.MainWindow);
            ICSharpCode.ILSpy.Commands.CommandManagerExtensions.RegisterNavigationRequery();

            // Diagnostic: attach to AssemblyTreeModel export when available and watch Root.Children
            try
            {
                _ = AttachAssemblyTreeDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Startup] Failed to start assembly diagnostics: " + ex);
            }

            // Theme application and ILSpy-warning will run in the Opened handler above.
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

    // Show a modal warning dialog if ILSpy (WPF) is detected running on Windows
    private static async System.Threading.Tasks.Task ShowWarningIfILSpyRunning(Window owner)
    {
        await System.Threading.Tasks.Task.Yield(); // ensure we yield back to dispatcher so window is shown

        try
        {
            if (!OperatingSystem.IsWindows())
                return;

            var processes = System.Diagnostics.Process.GetProcessesByName("ILSpy");
            if (processes == null || processes.Length == 0)
                return;

            var warning = new Window
            {
                Title = "Warning",
                Width = 520,
                Height = 180,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            };

            var panel = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(12)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "ILSpy (WPF) appears to be running. ProjectRover reads and writes the same ILSpy settings file; running both at the same time may cause settings conflicts.\n\nIt is recommended to close the other ILSpy instance or proceed with caution.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var okButton = new Avalonia.Controls.Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 12, 0, 0)
            };
            okButton.Click += (_, _) => warning.Close();
            panel.Children.Add(okButton);

            warning.Content = panel;

            await warning.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            log.Error(ex, "ShowWarningIfILSpyRunning failed");
        }
    }

    private static void AttachModelDiagnostics(ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel model)
    {
        try
        {
            model.PropertyChanged += (s, e) =>
            {
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
                incc.CollectionChanged += (s, e) =>
                {
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
