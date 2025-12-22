using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ICSharpCode.ILSpyX.Settings;

namespace ProjectRover.Settings
{
    public sealed class ProjectRoverSettingsSection : ISettingsSection
    {
        private bool showAvaloniaMainMenuOnMac;

        public XName SectionName => "ProjectRover";

        public bool ShowAvaloniaMainMenuOnMac
        {
            get => showAvaloniaMainMenuOnMac;
            set => SetProperty(ref showAvaloniaMainMenuOnMac, value);
        }

        public void LoadFromXml(XElement section)
        {
            ShowAvaloniaMainMenuOnMac = (bool?)section.Attribute(nameof(ShowAvaloniaMainMenuOnMac)) ?? false;
        }

        public XElement SaveToXml()
        {
            var element = new XElement(SectionName);
            element.SetAttributeValue(nameof(ShowAvaloniaMainMenuOnMac), ShowAvaloniaMainMenuOnMac);
            return element;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
