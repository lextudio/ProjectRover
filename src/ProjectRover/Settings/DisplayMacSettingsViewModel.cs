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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ICSharpCode.ILSpy.Util;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectRover.Settings
{
    public class DisplayMacSettingsViewModel : INotifyPropertyChanged
    {
        private readonly ProjectRoverSettingsSection _settings;
        private readonly string[] availableTerminals = new[] { "System Default", "Terminal.app", "iTerm2", "Custom" };

        public DisplayMacSettingsViewModel()
        {
            var services = ProjectRover.App.Current?.Services;
            var settingsService = services?.GetService<SettingsService>() ?? new SettingsService();
            _settings = settingsService.GetSettings<ProjectRoverSettingsSection>();
            _settings.PropertyChanged += Settings_PropertyChanged;
        }

        public bool ShowAvaloniaMainMenuOnMac
        {
            get => _settings.ShowAvaloniaMainMenuOnMac;
            set
            {
                if (_settings.ShowAvaloniaMainMenuOnMac != value)
                {
                    _settings.ShowAvaloniaMainMenuOnMac = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? PreferredTerminalApp
        {
            get => _settings.PreferredTerminalAppMac;
            set
            {
                if (_settings.PreferredTerminalAppMac != value)
                {
                    _settings.PreferredTerminalAppMac = value;
                    OnPropertyChanged();
                }
            }
        }

        public string[] AvailableTerminals => availableTerminals;

        public string? CustomTerminalPath
        {
            get => _settings.CustomTerminalPathMac;
            set
            {
                if (_settings.CustomTerminalPathMac != value)
                {
                    _settings.CustomTerminalPathMac = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCustomSelected => string.Equals(PreferredTerminalApp, "Custom", StringComparison.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectRoverSettingsSection.ShowAvaloniaMainMenuOnMac))
            {
                OnPropertyChanged(nameof(ShowAvaloniaMainMenuOnMac));
            }
            if (e.PropertyName == nameof(ProjectRoverSettingsSection.PreferredTerminalAppMac))
            {
                OnPropertyChanged(nameof(PreferredTerminalApp));
                OnPropertyChanged(nameof(IsCustomSelected));
            }
            if (e.PropertyName == nameof(ProjectRoverSettingsSection.CustomTerminalPathMac))
            {
                OnPropertyChanged(nameof(CustomTerminalPath));
            }
        }

    }
}
