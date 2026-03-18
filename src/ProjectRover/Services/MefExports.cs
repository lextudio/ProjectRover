// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
