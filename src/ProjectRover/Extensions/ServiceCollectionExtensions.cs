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

using ProjectRover.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpyX;

namespace ProjectRover.Extensions;

public static class ServiceCollectionExtensions
{
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
            .AddSingleton<AssemblyList>(sp => sp.GetRequiredService<ICSharpCode.ILSpy.AssemblyTree.AssemblyTreeModel>().AssemblyList);

    private static IConfigurationRoot GetConfiguration() =>
        new ConfigurationBuilder()
            .Build();
}
