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
