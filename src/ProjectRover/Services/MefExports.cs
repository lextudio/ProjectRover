using System;
using System.Composition;
using ProjectRover.Services;

namespace ProjectRover.Services
{
    // Export the service provider so MEF imports can obtain services via IServiceProvider
    [Export]
    public class ExportedServiceProvider
    {
        private readonly IServiceProvider _provider;

        [ImportingConstructor]
        public ExportedServiceProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public object GetService(Type serviceType) => _provider.GetService(serviceType)!;
    }

    // Export IlSpyBackend explicitly so MEF can resolve it
    [Export]
    public class ExportedIlSpyBackend
    {
        public IlSpyBackend Backend { get; }

        [ImportingConstructor]
        public ExportedIlSpyBackend(IlSpyBackend backend)
        {
            Backend = backend;
        }
    }
}
