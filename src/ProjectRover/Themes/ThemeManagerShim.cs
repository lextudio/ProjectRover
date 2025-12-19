using System;
using System.Collections.Generic;
using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;

namespace ICSharpCode.ILSpy.Themes
{
	// Avalonia implementation of ILSpy's ThemeManager: maps ILSpy theme names to Avalonia ThemeVariants
	// and drives the application theme variant.
	public class ThemeManager
	{
		private string currentTheme;

		public static ThemeManager Current { get; } = new ThemeManager();

		private ThemeManager()
		{
			currentTheme = DefaultTheme;
		}

		public IReadOnlyCollection<string> AllThemes { get; } = new[] { "Light", "Dark" };

		public bool IsDarkTheme { get; private set; }

		public string DefaultTheme => "Light";

		public string Theme {
			get => currentTheme;
			set => ApplyTheme(value);
		}

		public void ApplyTheme(string? themeName)
		{
			var normalized = NormalizeTheme(themeName);
			var same = string.Equals(normalized, currentTheme, StringComparison.OrdinalIgnoreCase);
			Console.WriteLine($"[ThemeManager] ApplyTheme requested='{themeName}' normalized='{normalized}' sameAsCurrent={same}");
			if (same)
				return;

			currentTheme = normalized;
			IsDarkTheme = string.Equals(normalized, "Dark", StringComparison.OrdinalIgnoreCase);

			if (Application.Current != null)
			{
				Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
				Console.WriteLine($"[ThemeManager] RequestedThemeVariant set to {Application.Current.RequestedThemeVariant}, Actual={Application.Current.ActualThemeVariant}");

				if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
				{
					foreach (var window in desktopLifetime.Windows)
					{
						window.RequestedThemeVariant = Application.Current.RequestedThemeVariant;
						Console.WriteLine($"[ThemeManager] Window '{window.Title}' RequestedThemeVariant => {window.RequestedThemeVariant}");
					}
				}
			}
			else
			{
				Console.WriteLine("[ThemeManager] Application.Current is null; cannot set RequestedThemeVariant");
			}
		}

		private static string NormalizeTheme(string? themeName)
		{
			if (string.IsNullOrWhiteSpace(themeName))
				return "Light";

			return themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
		}

		public static HighlightingColor GetColorForDarkTheme(HighlightingColor hc)
		{
			// For now, return the incoming color; AvaloniaEdit has its own theme handling.
			return hc;
		}

		public bool IsThemeAware(IHighlightingDefinition highlightingDefinition)
		{
			// TODO: mark definitions as theme-aware when ApplyHighlightingColors is expanded.
			return false;
		}

		public Button CreateButton()
		{
			return new Button();
		}

		internal void ApplyHighlightingColors(IHighlightingDefinition highlightingDefinition)
		{
			// TODO: propagate palette colors into AvaloniaEdit highlighting definitions.
		}
	}
}
