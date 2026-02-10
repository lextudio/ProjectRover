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
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");
		private readonly SettingsService settingsService;

		public SetLightThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetLightThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("Light");
			settingsService.SessionSettings.Theme = "Light";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "Dark", MenuCategory = "Theme", MenuOrder = 1)]
	[Shared]
	public class SetDarkThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");

		public SetDarkThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetDarkThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("Dark");
			settingsService.SessionSettings.Theme = "Dark";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "VS Code Light+", MenuCategory = "Theme", MenuOrder = 2)]
	[Shared]
	public class SetVSCodeLightThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");

		public SetVSCodeLightThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetVSCodeLightThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("VS Code Light+");
			settingsService.SessionSettings.Theme = "VS Code Light+";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "VS Code Dark+", MenuCategory = "Theme", MenuOrder = 3)]
	[Shared]
	public class SetVSCodeDarkThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");

		public SetVSCodeDarkThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetVSCodeDarkThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("VS Code Dark+");
			settingsService.SessionSettings.Theme = "VS Code Dark+";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "R# Light", MenuCategory = "Theme", MenuOrder = 4)]
	[Shared]
	public class SetRSharpLightThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");

		public SetRSharpLightThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetRSharpLightThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("R# Light");
			settingsService.SessionSettings.Theme = "R# Light";
		}
	}

	[ExportMainMenuCommand(ParentMenuID = "_Theme", Header = "R# Dark", MenuCategory = "Theme", MenuOrder = 5)]
	[Shared]
	public class SetRSharpDarkThemeMenuCommand : SimpleCommand
	{
		private readonly SettingsService settingsService;
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Theme");

		public SetRSharpDarkThemeMenuCommand(SettingsService settingsService)
		{
			this.settingsService = settingsService;
		}

		public override void Execute(object parameter)
		{
			log.Debug("SetRSharpDarkThemeMenuCommand executed");
			ThemeManager.Current.ApplyTheme("R# Dark");
			settingsService.SessionSettings.Theme = "R# Dark";
		}
	}
}
