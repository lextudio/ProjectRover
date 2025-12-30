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
            get => _settings.PreferredTerminalApp;
            set
            {
                if (_settings.PreferredTerminalApp != value)
                {
                    _settings.PreferredTerminalApp = value;
                    OnPropertyChanged();
                }
            }
        }

        public string[] AvailableTerminals => availableTerminals;

        public string? CustomTerminalPath
        {
            get => _settings.CustomTerminalPath;
            set
            {
                if (_settings.CustomTerminalPath != value)
                {
                    _settings.CustomTerminalPath = value;
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
            if (e.PropertyName == nameof(ProjectRoverSettingsSection.PreferredTerminalApp))
            {
                OnPropertyChanged(nameof(PreferredTerminalApp));
            }
        }

    }
}
