using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ICSharpCode.ILSpy.Util
{
	partial class GlobalUtils
	{
		public static void OpenTerminalAt(string path)
		{
			try
			{
				if (string.IsNullOrEmpty(path))
					return;
				path = Path.GetFullPath(path);

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					ExecuteCommand("cmd.exe", $"/k \"cd /d {path}\"");
					return;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					// Check for user preference (ProjectRover ProjectRoverSettingsSection.PreferredTerminalApp)
					string? preferred = null;
					try
					{
						if (ProjectRover.App.ExportProvider is not null)
						{
							var settingsService = ProjectRover.App.ExportProvider.GetExportedValueOrDefault<ICSharpCode.ILSpy.Util.SettingsService>();
							if (settingsService != null)
							{
								var roverSettings = settingsService.GetSettings<ProjectRover.Settings.ProjectRoverSettingsSection>();
								preferred = roverSettings.PreferredTerminalApp;
							}
						}
					}
					catch { }

					if (!string.IsNullOrEmpty(preferred))
					{
						try
						{
							switch (preferred)
							{
								case "System Default":
									// open with default terminal (use open) and don't attempt to cd inside the app
									Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"-a Terminal {path}", UseShellExecute = false });
									break;
								case "Terminal.app":
									{
										var script = $"tell application \"Terminal\" to do script \"cd '{path}'; clear\"";
										Process.Start(new ProcessStartInfo
										{
											FileName = "osascript",
											Arguments = $"-e \"{script}\"",
											UseShellExecute = false
										});
										break;
									}
								case "iTerm2":
									{
										var args = $"-e \"tell application \\\"iTerm2\\\"\" -e \"create window with default profile\" -e \"tell current session of current window\" -e \"write text \\\"cd '{path}'; clear\\\"\" -e \"end tell\" -e \"end tell\"";
										Process.Start(new ProcessStartInfo
										{
											FileName = "osascript",
											Arguments = args,
											UseShellExecute = false
										});
										break;
									}
								case "Custom":
									{
										var settingsService = ProjectRover.App.ExportProvider?.GetExportedValueOrDefault<ICSharpCode.ILSpy.Util.SettingsService>();
										var roverSettings = settingsService?.GetSettings<ProjectRover.Settings.ProjectRoverSettingsSection>();
										var custom = roverSettings?.CustomTerminalPath;
										if (!string.IsNullOrEmpty(custom))
										{
											Process.Start(new ProcessStartInfo { FileName = custom, Arguments = $"-c \"cd '{path}'; exec $SHELL\"", UseShellExecute = false });
										}
										else
										{
											System.Windows.MessageBox.Show("Custom terminal path is empty. Please set it in Preferences.", "ProjectRover");
										}
										break;
									}
								default:
									// Treat as executable name/path
									Process.Start(new ProcessStartInfo { FileName = preferred, Arguments = path, UseShellExecute = false });
									break;
							}
						}
						catch (Exception ex)
						{
							try
							{
								System.Windows.MessageBox.Show($"Failed to start terminal '{preferred}': {ex.Message}\nPlease check the setting and try another option.", "ProjectRover");
							}
							catch { }
						}
					}
					else
					{
						// Fallback: open Terminal.app
						var script = $"tell application \\\"Terminal\\\" to do script \\\"cd '{path}'; clear\\\"";
						Process.Start(new ProcessStartInfo
						{
							FileName = "osascript",
							Arguments = $"-e \"{script}\"",
							UseShellExecute = false
						});
					}
					return;
				}

				// Assume Linux / Unix-like: try common terminal emulators
				var candidates = new[]
				{
					new[]{ "gnome-terminal", $"--working-directory={path}" },
					new[]{ "konsole", $"--workdir {path}" },
					new[]{ "xfce4-terminal", $"--working-directory={path}" },
					new[]{ "x-terminal-emulator", $"-e bash -lc \"cd '{path}'; exec bash\"" },
					new[]{ "xterm", $"-e bash -lc \"cd '{path}'; exec bash\"" }
				};

				foreach (var c in candidates)
				{
					try
					{
						Process.Start(new ProcessStartInfo
						{
							FileName = c[0],
							Arguments = c[1],
							UseShellExecute = false
						});
						return;
					}
					catch { }
				}
			}
			catch
			{
				// ignore failures
			}
		}
    }
}
