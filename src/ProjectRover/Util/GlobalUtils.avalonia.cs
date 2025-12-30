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

				ProjectRover.Settings.ProjectRoverSettingsSection? roverSettings = null;
				try
				{
					if (ProjectRover.App.ExportProvider is not null)
					{
						var settingsService = ProjectRover.App.ExportProvider.GetExportedValueOrDefault<ICSharpCode.ILSpy.Util.SettingsService>();
						if (settingsService != null)
						{
							roverSettings = settingsService.GetSettings<ProjectRover.Settings.ProjectRoverSettingsSection>();
						}
					}
				}
				catch { }

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					// Read user preference from ProjectRover settings and launch exactly that terminal (no fallbacks)
					var preferred = roverSettings?.PreferredTerminalAppWindows;
					if (string.IsNullOrWhiteSpace(preferred))
						preferred = roverSettings?.PreferredTerminalApp;
					var custom = roverSettings?.CustomTerminalPathWindows;
					if (string.IsNullOrWhiteSpace(custom))
						custom = roverSettings?.CustomTerminalPath;

					if (!string.IsNullOrEmpty(preferred))
					{
						try
						{
							switch (preferred)
							{
								case "Command Prompt":
								case "cmd":
									ExecuteCommand("cmd.exe", $"/k \"cd /d {path}\"");
									break;
								case "PowerShell":
								case "Windows PowerShell":
									Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoExit -Command Set-Location -LiteralPath \"{path}\"", UseShellExecute = false });
									break;
								case "PowerShell Core":
								case "pwsh":
									Process.Start(new ProcessStartInfo { FileName = "pwsh.exe", Arguments = $"-NoExit -Command Set-Location -LiteralPath \"{path}\"", UseShellExecute = false });
									break;
								case "Windows Terminal":
								case "wt":
									Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = $"-d \"{path}\"", UseShellExecute = false });
									break;
								case "Custom":
									{
										if (!string.IsNullOrEmpty(custom))
										{
											Process.Start(new ProcessStartInfo { FileName = custom, Arguments = path, UseShellExecute = false });
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
						ExecuteCommand("cmd.exe", $"/k \"cd /d {path}\"");
					}

					return;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					// Check for user preference (ProjectRover ProjectRoverSettingsSection.PreferredTerminalApp)
					var preferred = roverSettings?.PreferredTerminalAppMac;
					if (string.IsNullOrWhiteSpace(preferred))
						preferred = roverSettings?.PreferredTerminalApp;
					var custom = roverSettings?.CustomTerminalPathMac;
					if (string.IsNullOrWhiteSpace(custom))
						custom = roverSettings?.CustomTerminalPath;

					if (!string.IsNullOrEmpty(preferred))
					{
						try
						{
							switch (preferred)
							{
								case "System Default":
									// open with default terminal (use open) and don't attempt to cd inside the app
										{
											var openInfo = new ProcessStartInfo { FileName = "open", UseShellExecute = false };
											openInfo.ArgumentList.Add("-a");
											openInfo.ArgumentList.Add("Terminal");
											openInfo.ArgumentList.Add(path);
											Process.Start(openInfo);
											break;
										}
								case "Terminal.app":
									{
										RunAppleScript(TerminalScript, path);
										break;
									}
								case "iTerm2":
									{
										RunAppleScript(ItermScript, path);
										break;
									}
								case "Custom":
									{
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
						RunAppleScript(TerminalScript, path);
					}
					return;
				}

				var linuxPreferred = roverSettings?.PreferredTerminalAppLinux;
				if (string.IsNullOrWhiteSpace(linuxPreferred))
					linuxPreferred = roverSettings?.PreferredTerminalApp;
				var linuxCustom = roverSettings?.CustomTerminalPathLinux;
				if (string.IsNullOrWhiteSpace(linuxCustom))
					linuxCustom = roverSettings?.CustomTerminalPath;

				if (!string.IsNullOrWhiteSpace(linuxPreferred))
				{
					try
					{
						switch (linuxPreferred)
						{
							case "System Default":
								TryLaunchLinuxDefault(path);
								return;
							case "GNOME Terminal":
								Process.Start(new ProcessStartInfo { FileName = "gnome-terminal", Arguments = $"--working-directory={path}", UseShellExecute = false });
								return;
							case "Konsole":
								Process.Start(new ProcessStartInfo { FileName = "konsole", Arguments = $"--workdir {path}", UseShellExecute = false });
								return;
							case "Xfce Terminal":
								Process.Start(new ProcessStartInfo { FileName = "xfce4-terminal", Arguments = $"--working-directory={path}", UseShellExecute = false });
								return;
							case "XTerm":
								Process.Start(new ProcessStartInfo { FileName = "xterm", Arguments = $"-e bash -lc \"cd '{path}'; exec bash\"", UseShellExecute = false });
								return;
							case "Custom":
								if (!string.IsNullOrEmpty(linuxCustom))
								{
									Process.Start(new ProcessStartInfo { FileName = linuxCustom, Arguments = path, UseShellExecute = false });
								}
								else
								{
									System.Windows.MessageBox.Show("Custom terminal path is empty. Please set it in Preferences.", "ProjectRover");
								}
								return;
							default:
								Process.Start(new ProcessStartInfo { FileName = linuxPreferred, Arguments = path, UseShellExecute = false });
								return;
						}
					}
					catch (Exception ex)
					{
						try
						{
							System.Windows.MessageBox.Show($"Failed to start terminal '{linuxPreferred}': {ex.Message}\nPlease check the setting and try another option.", "ProjectRover");
						}
						catch { }
						return;
					}
				}

				TryLaunchLinuxDefault(path);
			}
			catch
			{
				// ignore failures
			}
		}

		static void TryLaunchLinuxDefault(string path)
		{
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

		static void RunAppleScript(string script, params string[] args)
		{
			var psi = new ProcessStartInfo { FileName = "osascript", UseShellExecute = false };
			psi.ArgumentList.Add("-e");
			psi.ArgumentList.Add(script);
			if (args.Length > 0)
			{
				psi.ArgumentList.Add("--");
				foreach (var arg in args)
				{
					psi.ArgumentList.Add(arg);
				}
			}
			Process.Start(psi);
		}

		const string TerminalScript = "on run argv\n"
			+ "set targetPath to item 1 of argv\n"
			+ "tell application \"Terminal\"\n"
			+ "do script \"cd \" & quoted form of targetPath & \"; clear\"\n"
			+ "activate\n"
			+ "end tell\n"
			+ "end run";

		const string ItermScript = "on run argv\n"
			+ "set targetPath to item 1 of argv\n"
			+ "tell application \"iTerm2\"\n"
			+ "create window with default profile\n"
			+ "tell current session of current window\n"
			+ "write text \"cd \" & quoted form of targetPath & \"; clear\"\n"
			+ "end tell\n"
			+ "end tell\n"
			+ "end run";
	}
}
