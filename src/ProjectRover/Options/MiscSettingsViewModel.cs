// Copyright (c) 2026 LeXtudio Inc.
// Licensed under the MIT License.

using System;
using System.Composition;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpyX.Settings;

using Microsoft.Win32;

using TomsToolbox.Wpf;

namespace ICSharpCode.ILSpy.Options
{
	[ExportOptionPage(Order = 30)]
	[NonShared]
	public class MiscSettingsViewModel : ObservableObject, IOptionPage
	{
		private MiscSettings settings;
		public MiscSettings Settings {
			get => settings;
			set => SetProperty(ref settings, value);
		}

		public ICommand AddRemoveShellIntegrationCommand => new DelegateCommand(() => AppEnvironment.IsWindows, AddRemoveShellIntegration);

		const string rootPath = @"Software\Classes\{0}\shell";
		const string fullPath = @"Software\Classes\{0}\shell\Open with Project Rover\command";

		private void AddRemoveShellIntegration()
		{
			string commandLine = CommandLineTools.ArgumentArrayToCommandLine(Path.ChangeExtension(Assembly.GetEntryAssembly()?.Location, ".exe")) + " \"%L\"";
			if (RegistryEntriesExist())
			{
				if (MessageBox.Show(string.Format(Properties.Resources.RemoveShellIntegrationMessage, commandLine), "Project Rover", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
#pragma warning disable CA1416 // Validate platform compatibility
					Registry.CurrentUser
						.CreateSubKey(string.Format(rootPath, "dllfile"))?
						.DeleteSubKeyTree("Open with Project Rover");
					Registry.CurrentUser
						.CreateSubKey(string.Format(rootPath, "exefile"))?
						.DeleteSubKeyTree("Open with Project Rover");
#pragma warning restore CA1416 // Validate platform compatibility
				}
			}
			else
			{
				if (MessageBox.Show(string.Format(Properties.Resources.AddShellIntegrationMessage, commandLine), "Project Rover", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
#pragma warning disable CA1416 // Validate platform compatibility
					Registry.CurrentUser
						.CreateSubKey(string.Format(fullPath, "dllfile"))?
						.SetValue("", commandLine);
					Registry.CurrentUser
						.CreateSubKey(string.Format(fullPath, "exefile"))?
						.SetValue("", commandLine);
#pragma warning restore CA1416 // Validate platform compatibility
				}
			}
			OnPropertyChanged(nameof(AddRemoveShellIntegrationText));
		}

		private static bool RegistryEntriesExist()
		{
			if (!AppEnvironment.IsWindows)
				return false;

#pragma warning disable CA1416 // Validate platform compatibility
			return Registry.CurrentUser.OpenSubKey(string.Format(fullPath, "dllfile")) != null
				&& Registry.CurrentUser.OpenSubKey(string.Format(fullPath, "exefile")) != null;
#pragma warning restore CA1416 // Validate platform compatibility
		}

		public string AddRemoveShellIntegrationText {
			get {
				return RegistryEntriesExist() ? Properties.Resources.RemoveShellIntegration : Properties.Resources.AddShellIntegration;
			}
		}

		public string Title => Properties.Resources.Misc;

		public void Load(SettingsSnapshot settings)
		{
			Settings = settings.GetSettings<MiscSettings>();
		}

		public void LoadDefaults()
		{
			Settings.LoadFromXml(new XElement("dummy"));
		}
	}
}
