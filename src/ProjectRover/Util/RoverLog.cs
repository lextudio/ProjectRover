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
