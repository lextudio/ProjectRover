using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ProjectRover.Settings;

namespace ProjectRover.Settings
{
    public class DisplayMacSettingsViewModel : INotifyPropertyChanged
    {
        private readonly ProjectRoverSettingsService _service;
        private ProjectRoverSettings _settings;

        public DisplayMacSettingsViewModel()
        {
            _service = new ProjectRoverSettingsService();
            _settings = _service.Load();
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
                    // Save off the UI thread to avoid blocking UI during file I/O and MessageBus delivery
                    Task.Run(() => _service.Save(_settings));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
