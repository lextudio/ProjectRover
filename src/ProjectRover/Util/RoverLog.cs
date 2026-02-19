using System;
using System.IO;
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
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                    .Build();

                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(config)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("App", "ProjectRover")
                    .CreateLogger();

                logger.Information("Rover logging initialized");

                try
                {
                    // Log configured overrides for diagnosis
                    var overrides = config.GetSection("Serilog:MinimumLevel:Override").GetChildren();
                    foreach (var o in overrides)
                    {
                        logger.Debug("Serilog override: {Name} = {Level}", o.Key, o.Value);
                    }
                }
                catch
                {
                    // ignore
                }
                return logger;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to initialize Rover logging: " + ex);
                // Fallback: basic console logger if configuration-based initialization fails.
                return new LoggerConfiguration()
                    .Enrich.WithProperty("App", "ProjectRover")
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .CreateLogger();
            }
        }
    }
}
