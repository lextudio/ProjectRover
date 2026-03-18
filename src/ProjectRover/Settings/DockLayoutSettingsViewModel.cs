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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ICSharpCode.ILSpy.Util;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectRover.Settings
{
    public class DockLayoutSettingsViewModel : INotifyPropertyChanged
    {
        private readonly ProjectRoverSettingsSection _settings;

        public DockLayoutSettingsViewModel()
        {
            var services = ProjectRover.App.Current?.Services;
            var settingsService = services?.GetService<SettingsService>() ?? new SettingsService();
            _settings = settingsService.GetSettings<ProjectRoverSettingsSection>();
            _settings.PropertyChanged += Settings_PropertyChanged;
        }

        public bool UseDefaultDockLayoutOnly
        {
            get => _settings.UseDefaultDockLayoutOnly;
            set
            {
                if (_settings.UseDefaultDockLayoutOnly != value)
                {
                    _settings.UseDefaultDockLayoutOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectRoverSettingsSection.UseDefaultDockLayoutOnly))
            {
                OnPropertyChanged(nameof(UseDefaultDockLayoutOnly));
            }
        }
    }
}
