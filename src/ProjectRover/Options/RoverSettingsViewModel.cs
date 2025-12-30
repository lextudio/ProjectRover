using System;
using System.Composition;
using System.Xml.Linq;
using ICSharpCode.ILSpyX.Settings;
using ProjectRover.Settings;
using TomsToolbox.Wpf;

namespace ICSharpCode.ILSpy.Options
{
	[ExportOptionPage(Order = 99)]
	[NonShared]
	public class RoverSettingsViewModel : ObservableObject, IOptionPage
	{
		static readonly string[] DefaultTerminals = ["System Default", "Terminal.app", "iTerm2", "Custom"];

		ProjectRoverSettingsSection settings = new();

		public string Title => "Rover";

		public bool ShowAvaloniaMainMenuOnMac {
			get => settings.ShowAvaloniaMainMenuOnMac;
			set {
				if (settings.ShowAvaloniaMainMenuOnMac != value)
				{
					settings.ShowAvaloniaMainMenuOnMac = value;
					OnPropertyChanged();
				}
			}
		}

		public bool UseDefaultDockLayoutOnly {
			get => settings.UseDefaultDockLayoutOnly;
			set {
				if (settings.UseDefaultDockLayoutOnly != value)
				{
					settings.UseDefaultDockLayoutOnly = value;
					OnPropertyChanged();
				}
			}
		}

		public string? PreferredTerminalApp {
			get => settings.PreferredTerminalApp;
			set {
				if (settings.PreferredTerminalApp != value)
				{
					settings.PreferredTerminalApp = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCustomSelected));
				}
			}
		}

		public string? CustomTerminalPath {
			get => settings.CustomTerminalPath;
			set {
				if (settings.CustomTerminalPath != value)
				{
					settings.CustomTerminalPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string[] AvailableTerminals => DefaultTerminals;

		public bool IsCustomSelected => string.Equals(PreferredTerminalApp, "Custom", StringComparison.OrdinalIgnoreCase);

		public void Load(SettingsSnapshot snapshot)
		{
			settings = snapshot.GetSettings<ProjectRoverSettingsSection>();
			OnPropertyChanged(nameof(ShowAvaloniaMainMenuOnMac));
			OnPropertyChanged(nameof(UseDefaultDockLayoutOnly));
			OnPropertyChanged(nameof(PreferredTerminalApp));
			OnPropertyChanged(nameof(CustomTerminalPath));
			OnPropertyChanged(nameof(IsCustomSelected));
		}

		public void LoadDefaults()
		{
			var defaults = new ProjectRoverSettingsSection();
			defaults.LoadFromXml(new XElement(defaults.SectionName));

			settings.ShowAvaloniaMainMenuOnMac = defaults.ShowAvaloniaMainMenuOnMac;
			settings.UseDefaultDockLayoutOnly = defaults.UseDefaultDockLayoutOnly;
			settings.PreferredTerminalApp = defaults.PreferredTerminalApp;
			settings.CustomTerminalPath = defaults.CustomTerminalPath;
			settings.DockLayout = defaults.DockLayout;

			OnPropertyChanged(nameof(ShowAvaloniaMainMenuOnMac));
			OnPropertyChanged(nameof(UseDefaultDockLayoutOnly));
			OnPropertyChanged(nameof(PreferredTerminalApp));
			OnPropertyChanged(nameof(CustomTerminalPath));
			OnPropertyChanged(nameof(IsCustomSelected));
		}
	}
}
