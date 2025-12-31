using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ICSharpCode.ILSpyX.Settings;

namespace ProjectRover.Settings
{
    public sealed class ProjectRoverSettingsSection : ISettingsSection
    {
        private bool showAvaloniaMainMenuOnMac;
        private string? dockLayout;
        private string? preferredTerminalApp;
        private string? customTerminalPath;
        private string? preferredTerminalAppWindows;
        private string? preferredTerminalAppMac;
        private string? preferredTerminalAppLinux;
        private string? customTerminalPathWindows;
        private string? customTerminalPathMac;
        private string? customTerminalPathLinux;
        private bool useDefaultDockLayoutOnly = true;
        private bool showDecompilerLineNumbers = false;

        public XName SectionName => "ProjectRover";

        public bool ShowAvaloniaMainMenuOnMac
        {
            get => showAvaloniaMainMenuOnMac;
            set => SetProperty(ref showAvaloniaMainMenuOnMac, value);
        }

        public bool UseDefaultDockLayoutOnly
        {
            get => useDefaultDockLayoutOnly;
            set => SetProperty(ref useDefaultDockLayoutOnly, value);
        }

        public string? DockLayout
        {
            get => dockLayout;
            set => SetProperty(ref dockLayout, value);
        }

        public string? PreferredTerminalApp
        {
            get => preferredTerminalApp;
            set => SetProperty(ref preferredTerminalApp, value);
        }

        public string? CustomTerminalPath
        {
            get => customTerminalPath;
            set => SetProperty(ref customTerminalPath, value);
        }

        public string? PreferredTerminalAppWindows
        {
            get => preferredTerminalAppWindows;
            set => SetProperty(ref preferredTerminalAppWindows, value);
        }

        public string? PreferredTerminalAppMac
        {
            get => preferredTerminalAppMac;
            set => SetProperty(ref preferredTerminalAppMac, value);
        }

        public string? PreferredTerminalAppLinux
        {
            get => preferredTerminalAppLinux;
            set => SetProperty(ref preferredTerminalAppLinux, value);
        }

        public string? CustomTerminalPathWindows
        {
            get => customTerminalPathWindows;
            set => SetProperty(ref customTerminalPathWindows, value);
        }

        public string? CustomTerminalPathMac
        {
            get => customTerminalPathMac;
            set => SetProperty(ref customTerminalPathMac, value);
        }

        public string? CustomTerminalPathLinux
        {
            get => customTerminalPathLinux;
            set => SetProperty(ref customTerminalPathLinux, value);
        }

        /// <summary>
        /// Rover-specific toggle for decompiled text line numbers.
        /// </summary>
        public bool ShowDecompilerLineNumbers
        {
            get => showDecompilerLineNumbers;
            set => SetProperty(ref showDecompilerLineNumbers, value);
        }

        public void LoadFromXml(XElement section)
        {
            ShowAvaloniaMainMenuOnMac = (bool?)section.Attribute(nameof(ShowAvaloniaMainMenuOnMac)) ?? false;
            UseDefaultDockLayoutOnly = (bool?)section.Attribute(nameof(UseDefaultDockLayoutOnly)) ?? true;
            PreferredTerminalApp = (string?)section.Attribute(nameof(PreferredTerminalApp)) ?? string.Empty;
            CustomTerminalPath = (string?)section.Attribute(nameof(CustomTerminalPath)) ?? string.Empty;
            PreferredTerminalAppWindows = (string?)section.Attribute(nameof(PreferredTerminalAppWindows)) ?? PreferredTerminalApp;
            PreferredTerminalAppMac = (string?)section.Attribute(nameof(PreferredTerminalAppMac)) ?? PreferredTerminalApp;
            PreferredTerminalAppLinux = (string?)section.Attribute(nameof(PreferredTerminalAppLinux)) ?? PreferredTerminalApp;
            CustomTerminalPathWindows = (string?)section.Attribute(nameof(CustomTerminalPathWindows)) ?? CustomTerminalPath;
            CustomTerminalPathMac = (string?)section.Attribute(nameof(CustomTerminalPathMac)) ?? CustomTerminalPath;
            CustomTerminalPathLinux = (string?)section.Attribute(nameof(CustomTerminalPathLinux)) ?? CustomTerminalPath;
            ShowDecompilerLineNumbers = (bool?)section.Attribute(nameof(ShowDecompilerLineNumbers)) ?? false;
            var dockLayoutElement = section.Element(nameof(DockLayout));
            if (dockLayoutElement != null)
            {
                if (dockLayoutElement.HasElements)
                {
                    DockLayout = dockLayoutElement.Elements().FirstOrDefault()?.ToString();
                }
                else
                {
                    DockLayout = dockLayoutElement.Value;
                }
            }
        }

        public XElement SaveToXml()
        {
            var element = new XElement(SectionName);
            element.SetAttributeValue(nameof(ShowAvaloniaMainMenuOnMac), ShowAvaloniaMainMenuOnMac);
            element.SetAttributeValue(nameof(UseDefaultDockLayoutOnly), UseDefaultDockLayoutOnly);
            if (!string.IsNullOrWhiteSpace(DockLayout))
            {
                try
                {
                    element.Add(new XElement(nameof(DockLayout), XElement.Parse(DockLayout)));
                }
                catch
                {
                    element.Add(new XElement(nameof(DockLayout), DockLayout));
                }
            }
            element.SetAttributeValue(nameof(PreferredTerminalApp), PreferredTerminalApp);
            element.SetAttributeValue(nameof(CustomTerminalPath), CustomTerminalPath);
            element.SetAttributeValue(nameof(PreferredTerminalAppWindows), PreferredTerminalAppWindows);
            element.SetAttributeValue(nameof(PreferredTerminalAppMac), PreferredTerminalAppMac);
            element.SetAttributeValue(nameof(PreferredTerminalAppLinux), PreferredTerminalAppLinux);
            element.SetAttributeValue(nameof(CustomTerminalPathWindows), CustomTerminalPathWindows);
            element.SetAttributeValue(nameof(CustomTerminalPathMac), CustomTerminalPathMac);
            element.SetAttributeValue(nameof(CustomTerminalPathLinux), CustomTerminalPathLinux);
            element.SetAttributeValue(nameof(ShowDecompilerLineNumbers), ShowDecompilerLineNumbers);
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
