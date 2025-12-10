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
using System.Threading.Tasks;
using Avalonia.Controls;
using ProjectRover.Notifications;
using ProjectRover.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace ProjectRover.ViewModels.Design;

public class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel()
        : base(new DesignLogger(), new DesignNotificationService(), new DesignAnalyticsService(), new DesignDialogService(), new DesignStartupOptions())
    {
    }
}

file class DesignNotificationService : INotificationService
{
    public void RegisterHandler(INotificationHandler handler)
    {
    }

    public void ShowNotification(Notification notification)
    {
    }

    public void ReplaceNotification(Notification notificationToBeReplaced, Notification replacementNotification)
    {
    }
}

file class DesignAnalyticsService : IAnalyticsService
{
    public void TrackEvent(AnalyticsEvent @event)
    {
    }

    public Task TrackEventAsync(AnalyticsEvent @event)
    {
        return Task.CompletedTask;
    }
}

file class DesignDialogService : IDialogService
{
    public void ShowDialog<TWindow>()
        where TWindow : Window
    {
    }
}

file class DesignStartupOptions : IOptions<Options.StartupOptions>
{
    public Options.StartupOptions Value { get; } = new();
}

file class DesignLogger : ILogger<MainWindowViewModel>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

    private class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
