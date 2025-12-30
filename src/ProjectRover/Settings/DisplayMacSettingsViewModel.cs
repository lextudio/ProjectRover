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
