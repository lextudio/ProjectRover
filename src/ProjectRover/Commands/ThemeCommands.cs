using System.Composition;

using ICSharpCode.ILSpy.Commands;
using ICSharpCode.ILSpy.Properties;
using ICSharpCode.ILSpy.Themes;
using ICSharpCode.ILSpy.Util;

namespace ICSharpCode.ILSpy.Commands
{
	[ExportMainMenuCommand(MenuID = "_Theme", Header = nameof(Resources.Theme), ParentMenuID = nameof(Resources._View), MenuCategory = "Theme", MenuOrder = 100)]
	[Shared]
	public class ThemeMenuRootCommand : SimpleCommand
	{
		public override void Execute(object parameter)
		{
			// Menu root, no direct action required.
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "Light", MenuCategory = "Theme", MenuOrder = 0)]
	[Shared]
	public class SetLightThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;

		public SetLightThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			System.Console.WriteLine("[ThemeCommands] SetLightThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("Light");
			settingsService.SessionSettings.Theme = "Light";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "Dark", MenuCategory = "Theme", MenuOrder = 1)]
	[Shared]
	public class SetDarkThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;

		public SetDarkThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			System.Console.WriteLine("[ThemeCommands] SetDarkThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("Dark");
			settingsService.SessionSettings.Theme = "Dark";
		}
	}
}
