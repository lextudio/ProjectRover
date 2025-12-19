using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.TextMate;
using TextMateSharp.Themes;
using TextMateSharp.Registry;

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

				// Notify other components (e.g., owners of TextMate installations) that theme changed
				try
				{
					MessageBus.Send(this, new ThemeChangedEventArgs(normalized));
				}
				catch (Exception ex)
				{
					Console.WriteLine("[ThemeManager] Failed to send ThemeChanged message: " + ex.Message);
				}

				// Diagnostic: inspect windows for TextEditor and presence of TextMate transformer
				try
				{
					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
					{
						foreach (var w in desktop.Windows)
						{
							int editorCount = 0;
							int installedCount = 0;
							foreach (var desc in w.GetVisualDescendants())
							{
								if (desc is AvaloniaEdit.TextEditor te)
								{
									editorCount++;
									var hasTransformer = te.TextArea.TextView.LineTransformers.OfType<AvaloniaEdit.TextMate.TextMateColoringTransformer>().Any();
									if (hasTransformer) installedCount++;
								}
							}
							Console.WriteLine($"[ThemeManager] Window '{w.Title}': TextEditors={editorCount}, TextMateInstalled={installedCount}");
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("[ThemeManager] Diagnostics failed: " + ex.Message);
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
			if (hc.Foreground is null && hc.Background is null)
			{
				return hc;
			}

			var dark = hc.Clone();
			dark.Foreground = AdjustForDarkTheme(dark.Foreground);
			dark.Background = AdjustForDarkTheme(dark.Background);
			return dark;
		}

		private static HighlightingBrush? AdjustForDarkTheme(HighlightingBrush? lightBrush)
		{
			if (lightBrush is AvaloniaEdit.Highlighting.SimpleHighlightingBrush simpleBrush)
			{
				var brush = simpleBrush.GetBrush(null) as Avalonia.Media.SolidColorBrush;
				if (brush != null)
				{
					var col = brush.Color;
					var (h, s, l) = RgbToHsl(col.R, col.G, col.B);

					// Invert the lightness, but also increase it a bit
					l = 1f - MathF.Pow(l, 1.2f);

					// Desaturate very saturated colors
					if (s > 0.75f && l < 0.75f)
					{
						s *= 0.75f;
						l *= 1.2f;
					}

					var (r, g, b) = HslToRgb(h, s, l);
					var newColor = Avalonia.Media.Color.FromArgb(col.A, r, g, b);
					return new AvaloniaEdit.Highlighting.SimpleHighlightingBrush(newColor);
				}
			}
			return lightBrush;
		}

		private static (float h, float s, float l) RgbToHsl(byte rB, byte gB, byte bB)
		{
			float r = rB / 255f;
			float g = gB / 255f;
			float b = bB / 255f;

			float max = MathF.Max(r, MathF.Max(g, b));
			float min = MathF.Min(r, MathF.Min(g, b));
			float l = (max + min) / 2f;
			if (MathF.Abs(max - min) < 1e-6f)
			{
				return (0f, 0f, l);
			}

			float d = max - min;
			float s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
			float h;
			if (max == r)
				h = (g - b) / d + (g < b ? 6f : 0f);
			else if (max == g)
				h = (b - r) / d + 2f;
			else
				h = (r - g) / d + 4f;
			h *= 60f;
			return (h, s, l);
		}

		private static (byte r, byte g, byte b) HslToRgb(float h, float s, float l)
		{
			// h in degrees [0..360)
			h = h % 360f;
			if (h < 0) h += 360f;
			float c = (1f - MathF.Abs(2f * l - 1f)) * s;
			float hp = h / 60f;
			float x = c * (1f - MathF.Abs(hp % 2f - 1f));
			float r1 = 0f, g1 = 0f, b1 = 0f;
			switch ((int)MathF.Floor(hp))
			{
				case 0: r1 = c; g1 = x; b1 = 0; break;
				case 1: r1 = x; g1 = c; b1 = 0; break;
				case 2: r1 = 0; g1 = c; b1 = x; break;
				case 3: r1 = 0; g1 = x; b1 = c; break;
				case 4: r1 = x; g1 = 0; b1 = c; break;
				default: r1 = c; g1 = 0; b1 = x; break;
			}
			float m = l - c / 2f;
			float rf = (r1 + m) * 255f;
			float gf = (g1 + m) * 255f;
			float bf = (b1 + m) * 255f;
			if (rf < 0f) rf = 0f; else if (rf > 255f) rf = 255f;
			if (gf < 0f) gf = 0f; else if (gf > 255f) gf = 255f;
			if (bf < 0f) bf = 0f; else if (bf > 255f) bf = 255f;
			byte r = (byte)rf;
			byte g = (byte)gf;
			byte b = (byte)bf;
			return (r, g, b);
		}

		public bool IsThemeAware(IHighlightingDefinition highlightingDefinition)
		{
			if (highlightingDefinition == null)
				return false;
			// Determine if the definition contains any colors that we can map for dark theme.
			if (highlightingDefinition.MainRuleSet != null)
			{
				foreach (var rule in highlightingDefinition.MainRuleSet.Rules)
				{
					if (rule is HighlightingRule hr)
					{
						if (hr.Color != null && (hr.Color.Foreground != null || hr.Color.Background != null))
							return true;
					}
				}
			}
			// Fallback: if there are any named highlighting colors, consider it theme-aware.
			if (highlightingDefinition.NamedHighlightingColors != null)
			{
				using (var e = highlightingDefinition.NamedHighlightingColors.GetEnumerator())
				{
					if (e.MoveNext())
						return true;
				}
			}
			return false;
		}

		public Button CreateButton()
		{
			return new Button();
		}

		internal void ApplyHighlightingColors(IHighlightingDefinition highlightingDefinition)
		{
			if (highlightingDefinition == null)
				return;

			// Convert named highlighting colors.
			if (highlightingDefinition.NamedHighlightingColors != null)
			{
				foreach (var named in highlightingDefinition.NamedHighlightingColors)
				{
					if (named == null)
						continue;
					try
					{
						var dark = GetColorForDarkTheme(named);
						named.Foreground = dark.Foreground;
						named.Background = dark.Background;
					}
					catch (InvalidOperationException)
					{
						// Named color may be frozen; skip replacing it.
					}
				}
			}

			// Walk main rules and convert inline rule colors.
			if (highlightingDefinition.MainRuleSet != null)
			{
				foreach (var rule in highlightingDefinition.MainRuleSet.Rules)
				{
					if (rule is HighlightingRule hr && hr.Color != null)
					{
						try
						{
							hr.Color = GetColorForDarkTheme(hr.Color);
						}
						catch (InvalidOperationException)
						{
							// If the color is frozen, skip modification.
						}
					}
				}
			}
		}
	}

	public class ThemeChangedEventArgs(string? themeName) : EventArgs
	{
		public string? ThemeName { get; } = themeName;
	}
}
