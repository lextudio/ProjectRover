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
		static readonly string[] WindowsTerminals = ["Command Prompt", "PowerShell", "PowerShell Core", "Windows Terminal", "Custom"];
		static readonly string[] MacTerminals = ["System Default", "Terminal.app", "iTerm2", "Custom"];
		static readonly string[] LinuxTerminals = ["System Default", "GNOME Terminal", "Konsole", "Xfce Terminal", "XTerm", "Custom"];

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

		public string? PreferredTerminalAppWindows {
			get => settings.PreferredTerminalAppWindows;
			set {
				if (settings.PreferredTerminalAppWindows != value)
				{
					settings.PreferredTerminalAppWindows = value;
					settings.PreferredTerminalApp = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCustomSelectedWindows));
				}
			}
		}

		public string? PreferredTerminalAppMac {
			get => settings.PreferredTerminalAppMac;
			set {
				if (settings.PreferredTerminalAppMac != value)
				{
					settings.PreferredTerminalAppMac = value;
					settings.PreferredTerminalApp = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCustomSelectedMac));
				}
			}
		}

		public string? PreferredTerminalAppLinux {
			get => settings.PreferredTerminalAppLinux;
			set {
				if (settings.PreferredTerminalAppLinux != value)
				{
					settings.PreferredTerminalAppLinux = value;
					settings.PreferredTerminalApp = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCustomSelectedLinux));
				}
			}
		}

		public string? CustomTerminalPathWindows {
			get => settings.CustomTerminalPathWindows;
			set {
				if (settings.CustomTerminalPathWindows != value)
				{
					settings.CustomTerminalPathWindows = value;
					settings.CustomTerminalPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string? CustomTerminalPathMac {
			get => settings.CustomTerminalPathMac;
			set {
				if (settings.CustomTerminalPathMac != value)
				{
					settings.CustomTerminalPathMac = value;
					settings.CustomTerminalPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string? CustomTerminalPathLinux {
			get => settings.CustomTerminalPathLinux;
			set {
				if (settings.CustomTerminalPathLinux != value)
				{
					settings.CustomTerminalPathLinux = value;
					settings.CustomTerminalPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string[] AvailableTerminalsWindows => WindowsTerminals;

		public string[] AvailableTerminalsMac => MacTerminals;

		public string[] AvailableTerminalsLinux => LinuxTerminals;

		public bool IsCustomSelectedWindows => string.Equals(PreferredTerminalAppWindows, "Custom", StringComparison.OrdinalIgnoreCase);

		public bool IsCustomSelectedMac => string.Equals(PreferredTerminalAppMac, "Custom", StringComparison.OrdinalIgnoreCase);

		public bool IsCustomSelectedLinux => string.Equals(PreferredTerminalAppLinux, "Custom", StringComparison.OrdinalIgnoreCase);

		public void Load(SettingsSnapshot snapshot)
		{
			settings = snapshot.GetSettings<ProjectRoverSettingsSection>();
			OnPropertyChanged(nameof(ShowAvaloniaMainMenuOnMac));
			OnPropertyChanged(nameof(UseDefaultDockLayoutOnly));
			OnPropertyChanged(nameof(PreferredTerminalAppWindows));
			OnPropertyChanged(nameof(PreferredTerminalAppMac));
			OnPropertyChanged(nameof(PreferredTerminalAppLinux));
			OnPropertyChanged(nameof(CustomTerminalPathWindows));
			OnPropertyChanged(nameof(CustomTerminalPathMac));
			OnPropertyChanged(nameof(CustomTerminalPathLinux));
			OnPropertyChanged(nameof(IsCustomSelectedWindows));
			OnPropertyChanged(nameof(IsCustomSelectedMac));
			OnPropertyChanged(nameof(IsCustomSelectedLinux));
		}

		public void LoadDefaults()
		{
			var defaults = new ProjectRoverSettingsSection();
			defaults.LoadFromXml(new XElement(defaults.SectionName));

			settings.ShowAvaloniaMainMenuOnMac = defaults.ShowAvaloniaMainMenuOnMac;
			settings.UseDefaultDockLayoutOnly = defaults.UseDefaultDockLayoutOnly;
			settings.PreferredTerminalApp = defaults.PreferredTerminalApp;
			settings.CustomTerminalPath = defaults.CustomTerminalPath;
			settings.PreferredTerminalAppWindows = defaults.PreferredTerminalAppWindows;
			settings.PreferredTerminalAppMac = defaults.PreferredTerminalAppMac;
			settings.PreferredTerminalAppLinux = defaults.PreferredTerminalAppLinux;
			settings.CustomTerminalPathWindows = defaults.CustomTerminalPathWindows;
			settings.CustomTerminalPathMac = defaults.CustomTerminalPathMac;
			settings.CustomTerminalPathLinux = defaults.CustomTerminalPathLinux;
			settings.DockLayout = defaults.DockLayout;

			OnPropertyChanged(nameof(ShowAvaloniaMainMenuOnMac));
			OnPropertyChanged(nameof(UseDefaultDockLayoutOnly));
			OnPropertyChanged(nameof(PreferredTerminalAppWindows));
			OnPropertyChanged(nameof(PreferredTerminalAppMac));
			OnPropertyChanged(nameof(PreferredTerminalAppLinux));
			OnPropertyChanged(nameof(CustomTerminalPathWindows));
			OnPropertyChanged(nameof(CustomTerminalPathMac));
			OnPropertyChanged(nameof(CustomTerminalPathLinux));
			OnPropertyChanged(nameof(IsCustomSelectedWindows));
			OnPropertyChanged(nameof(IsCustomSelectedMac));
			OnPropertyChanged(nameof(IsCustomSelectedLinux));
		}
	}
}
