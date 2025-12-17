using System;
using System.Composition;
using System.Composition.Convention;
using ProjectRover.Services;
using ICSharpCode.ILSpy;

namespace ProjectRover.Services
{
    // Export the service provider so MEF imports can obtain services via IServiceProvider
    [Export(typeof(IServiceProvider))]
    public class ExportedServiceProvider : IServiceProvider
    {
        private readonly IServiceProvider _provider;

        [ImportingConstructor]
        public ExportedServiceProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public object? GetService(Type serviceType) => _provider.GetService(serviceType);
    }

    // Export IlSpyBackend explicitly so MEF can resolve it
    // [Export(typeof(IlSpyBackend))]
    // public class ExportedIlSpyBackend
    // {
    //     public IlSpyBackend Backend { get; }

    //     [ImportingConstructor]
    //     public ExportedIlSpyBackend(IlSpyBackend backend)
    //     {
    //         Backend = backend;
    //     }
    // }

    // Export a few ViewModels so ILSpy parts expecting them over MEF can import them
    // [Shared]
    // public class ExportedMainWindowViewModel
    // {
    //     [ImportingConstructor]
    //     public ExportedMainWindowViewModel(MainWindowViewModel vm)
    //     {
    //         ViewModel = vm;
    //     }

    //     [Export]
    //     public MainWindowViewModel ViewModel { get; }
    // }

    // public class ExportedTabPageModel
    // {
    //     [ImportingConstructor]
    //     public ExportedTabPageModel(ICSharpCode.ILSpy.ViewModels.TabPageModel model)
    //     {
    //         Model = model;
    //     }

    //     [Export]
    //     public ICSharpCode.ILSpy.ViewModels.TabPageModel Model { get; }
    // }

    // [Export(typeof(ICSharpCode.ILSpy.Languages.LanguageService))]
    // public class ExportedLanguageService
    // {
    //     [ImportingConstructor]
    //     public ExportedLanguageService(ICSharpCode.ILSpy.Languages.LanguageService svc)
    //     {
    //         Service = svc;
    //     }

    //     [Export]
    //     public ICSharpCode.ILSpy.Languages.LanguageService Service { get; }
    // }

    // [Shared]
    // public class ExportedSettingsService
    // {
    //     public ExportedSettingsService()
    //     {
    //         Service = new ICSharpCode.ILSpy.Util.SettingsService();
    //     }

    //     [Export]
    //     public ICSharpCode.ILSpy.Util.SettingsService Service { get; }
    // }
}
