using System;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ICSharpCode.ILSpy.Util
{
    public static class RoverLog
    {
        private static ILogger? _logger;

        public static ILogger Log => _logger ??= CreateLogger();

        private static ILogger CreateLogger()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                    .Build();

                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(config)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .CreateLogger();

                logger.Information("Rover logging initialized");
                return logger;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to initialize Rover logging: " + ex);
                return new LoggerConfiguration().WriteTo.Console().CreateLogger();
            }
        }
    }
}
