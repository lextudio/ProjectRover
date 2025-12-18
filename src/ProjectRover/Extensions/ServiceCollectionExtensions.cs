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
using ProjectRover.Options;
using ProjectRover.Providers;
using ProjectRover.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using ICSharpCode.ILSpy.ViewModels;

namespace ProjectRover.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureOptions(this IServiceCollection services)
    {
        var configuration = GetConfiguration();

        return services
            .Configure<MatomoAnalyticsOptions>(configuration.GetSection(MatomoAnalyticsOptions.Key))
            .Configure<AppInformationProviderOptions>(configuration.GetSection(AppInformationProviderOptions.Key));
    }

    public static IServiceCollection ConfigureLogging(this IServiceCollection services)
    {
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        });

        return services;
    }

    public static IServiceCollection AddViews(this IServiceCollection services) =>
        services
            .AddSingleton<ICSharpCode.ILSpy.MainWindow>();

    public static IServiceCollection AddViewModels(this IServiceCollection services) =>
        services
            .AddSingleton<ICSharpCode.ILSpy.MainWindowViewModel>()
            .AddSingleton<UpdatePanelViewModel>();

    public static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddSingleton<ICSharpCode.ILSpy.Util.SettingsService>()
            .AddSingleton<ICSharpCode.ILSpy.LanguageService>(sp => new ICSharpCode.ILSpy.LanguageService(
                new ICSharpCode.ILSpy.Language[] { new ICSharpCode.ILSpy.CSharpLanguage(), new ICSharpCode.ILSpy.ILLanguage(sp.GetRequiredService<ICSharpCode.ILSpy.Docking.DockWorkspace>()) },
                sp.GetRequiredService<ICSharpCode.ILSpy.Util.SettingsService>(),
                sp.GetRequiredService<ICSharpCode.ILSpy.Docking.DockWorkspace>()
            ))
            .AddSingleton<ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel>(sp => new ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel(
                sp.GetRequiredService<ICSharpCode.ILSpy.Util.SettingsService>(),
                sp.GetRequiredService<ICSharpCode.ILSpy.LanguageService>(),
                ProjectRover.App.ExportProvider
            ))
            .AddSingleton<IDockLayoutDescriptorProvider, DefaultDockLayoutDescriptorProvider>();

    public static IServiceCollection AddProviders(this IServiceCollection services) =>
        services
            .AddSingleton<ISystemInformationProvider, SystemInformationProvider>()
            .AddSingleton<IDeviceIdentifierProvider, DeviceIdentifierProvider>()
            .AddSingleton<IAppInformationProvider, AppInformationProvider>();

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: 5));

        services
            .AddHttpClient(nameof(AppInformationProvider), (services, httpClient) =>
            {
                var options = services.GetRequiredService<IOptions<AppInformationProviderOptions>>();
            
                httpClient.BaseAddress = new Uri(options.Value.ServerUrl);
            })
            .AddPolicyHandler(policy);

        return services;
    }

    private static IConfigurationRoot GetConfiguration() =>
        new ConfigurationBuilder()
            .AddEmbeddedResource("appsettings.json")
            .AddEmbeddedResource($"appsettings.{EnvironmentProvider.Environment}.json")
            .Build();
}
