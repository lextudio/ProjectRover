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
using ICSharpCode.ILSpy;
using ProjectRover.ViewModels;
using ProjectRover.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using AssemblyTreeModel = ProjectRover.ViewModels.AssemblyTreeModel;

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
            .AddSingleton<MainWindow>()
            .AddTransient<AboutDialog>();

    public static IServiceCollection AddViewModels(this IServiceCollection services) =>
        services
            .AddSingleton<AssemblyTreeModel>()
            .AddSingleton<MainWindowViewModel>()
            .AddTransient<IAboutWindowViewModel, AboutWindowViewModel>()
            .AddSingleton<IUpdatePanelViewModel, UpdatePanelViewModel>();

    public static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddSingleton<IlSpyBackend>()
            .AddSingleton<ICSharpCode.ILSpy.Docking.IDockWorkspace, AvaloniaDockWorkspace>()
            .AddSingleton<IPlatformService>(sp => new AvaloniaPlatformService(sp.GetRequiredService<ICSharpCode.ILSpy.Docking.IDockWorkspace>()))
            .AddSingleton<INotificationService, NotificationService>()
            .AddTransient<IProjectGenerationService, ProjectGenerationService>()
            .AddTransient<IAutoUpdateService, AutoUpdateService>()
            .AddTransient<IAnalyticsService, NullAnalyticsService>()
            .AddTransient<IDialogService, DialogService>()
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<ICommandCatalog, CommandCatalog>()
            .AddSingleton<ProjectRover.Services.Navigation.INavigationService, ProjectRover.Services.Navigation.NavigationService>()
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
            .AddHttpClient<IAutoUpdateService, AutoUpdateService>(httpClient =>
            {
                httpClient.BaseAddress = new Uri("https://api.github.com");

                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                // As per https://docs.github.com/en/rest/using-the-rest-api/troubleshooting-the-rest-api?apiVersion=2022-11-28#user-agent-required
                httpClient.DefaultRequestHeaders.Add("User-Agent", "ProjectRover");
            })
            .AddPolicyHandler(policy);

        if (EnvironmentProvider.Environment == Environment.Production)
        {
            services
                .AddHttpClient<IAnalyticsService, MatomoAnalyticsService>((services, httpClient) =>
                {
                    var options = services.GetRequiredService<IOptions<MatomoAnalyticsOptions>>();

                    httpClient.BaseAddress = new Uri(options.Value.ServerUrl);
                })
                .AddPolicyHandler(policy);
        }

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

    private static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services, Environment environment)
        where TService : class
        where TImplementation : class, TService
    {
        if (environment == EnvironmentProvider.Environment)
            services.AddTransient<TService, TImplementation>();

        return services;
    }
}
