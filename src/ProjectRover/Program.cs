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

using Avalonia;
using System;
using Serilog;
using Microsoft.Extensions.Logging;

namespace ProjectRover;

sealed class Program
{
    public static event EventHandler<Exception>? UnhandledException;
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("[ProjectRover] Main started. Console output is working.");

        // Configure Serilog (console + file) - settings can be controlled via configuration later.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("projectrover.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Do NOT forward ILSpyX diagnostics into Serilog here so ilspy logs remain console-only.
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            if (UnhandledException != null)
                UnhandledException.Invoke(null, e);
            else
                Console.WriteLine(e);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}