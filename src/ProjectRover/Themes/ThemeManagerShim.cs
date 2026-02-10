using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit.Highlighting;

using ICSharpCode.ILSpy.TextView;

namespace ICSharpCode.ILSpy.Themes
{
	// Avalonia implementation of ILSpy's ThemeManager: maps ILSpy theme names to Avalonia ThemeVariants
	// and drives the application theme variant.
	public class ThemeManager
	{
		private const string IsThemeAwareKey = "ILSpy.IsThemeAware";
		private const string SyntaxColorKeyPrefix = "SyntaxColor.";

		private static readonly string[] SupportedThemes = {
			"Light",
			"Dark",
			"VS Code Light+",
			"VS Code Dark+",
			"R# Light",
			"R# Dark"
		};

		private static readonly HashSet<string> DarkThemes = new(StringComparer.OrdinalIgnoreCase) {
			"Dark",
			"VS Code Dark+",
			"R# Dark"
		};

		private static readonly object SyntaxColorCacheLock = new();
		private static readonly Dictionary<string, IReadOnlyDictionary<string, SyntaxColorEntry>> SyntaxColorCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly IReadOnlyDictionary<string, SyntaxColorEntry> EmptySyntaxColors = new Dictionary<string, SyntaxColorEntry>();

		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("ThemeManager");

		private string currentTheme;
		private object? cachedThemeVariant;
		private readonly ResourceDictionary themeDictionaryContainer = new();
		private IReadOnlyDictionary<string, SyntaxColorEntry> syntaxColors = EmptySyntaxColors;

		public static ThemeManager Current { get; } = new ThemeManager();

		private ThemeManager()
		{
			currentTheme = string.Empty;
			var app = Application.Current;
			if (app != null)
			{
				app.Resources.MergedDictionaries.Add(themeDictionaryContainer);
				cachedThemeVariant = app.ActualThemeVariant;
			}
		}

		// Ensure we react to persisted settings changes (Options dialog commit) so theme is applied immediately.
		static ThemeManager()
		{
			try
			{
				MessageBus<SettingsChangedEventArgs>.Subscribers += (sender, _) => {
					if (sender is SessionSettings { Theme: { Length: > 0 } theme })
					{
						Current.ApplyTheme(theme);
					}
				};
			}
			catch
			{
				// Ignore if MessageBus isn't available.
			}
		}

		public IReadOnlyCollection<string> AllThemes { get; } = SupportedThemes;

		public bool IsDarkTheme { get; private set; }

		public string DefaultTheme => "Light";

		public string Theme {
			get => string.IsNullOrEmpty(currentTheme) ? DefaultTheme : currentTheme;
			set => ApplyTheme(value);
		}

		/// <summary>
		/// Get the cached theme variant. Can be safely called from any thread.
		/// Returns null if the cache has not been initialized.
		/// </summary>
		public object? GetCachedThemeVariant()
		{
			return cachedThemeVariant;
		}

		/// <summary>
		/// Resolve a resource from the currently loaded Theme.*.axaml dictionary.
		/// Falls back to application resources when the selected theme dictionary does not contain the key.
		/// </summary>
		public bool TryGetThemeResource(string key, out object? value)
		{
			value = null;
			if (string.IsNullOrWhiteSpace(key))
				return false;

			if (TryGetResourceFromProvider(themeDictionaryContainer, key, out value))
				return true;

			return Application.Current?.TryFindResource(key, out value) == true;
		}

		public void ApplyTheme(string? themeName)
		{
			var normalizedThemeName = NormalizeTheme(themeName);
			var sameAsCurrent = string.Equals(normalizedThemeName, currentTheme, StringComparison.OrdinalIgnoreCase);
			log.Debug("ApplyTheme requested='{Requested}' normalized='{Normalized}' sameAsCurrent={Same}", themeName, normalizedThemeName, sameAsCurrent);
			if (sameAsCurrent)
				return;

			currentTheme = normalizedThemeName;
			IsDarkTheme = DarkThemes.Contains(normalizedThemeName);

			var themeFileName = GetThemeFileName(normalizedThemeName);
			ApplyThemeResourceDictionary(themeFileName);
			LoadSyntaxColors(themeFileName);

			if (Application.Current == null)
			{
				log.Warning("Application.Current is null; cannot set RequestedThemeVariant");
				return;
			}

			Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
			cachedThemeVariant = Application.Current.ActualThemeVariant;
			log.Debug("RequestedThemeVariant set to {Requested}, Actual={Actual}", Application.Current.RequestedThemeVariant, Application.Current.ActualThemeVariant);

			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
			{
				foreach (var window in desktopLifetime.Windows)
				{
					window.RequestedThemeVariant = Application.Current.RequestedThemeVariant;
				}
			}

			try
			{
				MessageBus.Send(this, new ThemeChangedEventArgs(normalizedThemeName));
			}
			catch (Exception ex)
			{
				log.Error(ex, "Failed to send ThemeChanged message");
			}

			try
			{
				DecompilerTextView.RefreshHighlightingForAllOpenEditors();
			}
			catch (Exception ex)
			{
				log.Error(ex, "Failed to refresh highlighting after theme change");
			}

			try
			{
				if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
				{
					foreach (var window in desktop.Windows)
					{
						var editorCount = 0;
						foreach (var descendant in window.GetVisualDescendants())
						{
							if (descendant is AvaloniaEdit.TextEditor)
							{
								editorCount++;
							}
						}

						log.Debug("Window '{Title}': TextEditors={Count}", window.Title, editorCount);
					}
				}
			}
			catch (Exception ex)
			{
				log.Error(ex, "Diagnostics failed");
			}
		}

		private static string NormalizeTheme(string? themeName)
		{
			if (string.IsNullOrWhiteSpace(themeName))
				return Current.DefaultTheme;

			foreach (var supportedThemeName in SupportedThemes)
			{
				if (supportedThemeName.Equals(themeName, StringComparison.OrdinalIgnoreCase))
					return supportedThemeName;
			}

			return Current.DefaultTheme;
		}

		private static bool TryGetResourceFromProvider(IResourceProvider provider, string key, out object? value)
		{
			value = null;
			if (provider.TryGetResource(key, ThemeVariant.Default, out value))
				return true;

			if (provider is ResourceDictionary dictionary)
			{
				foreach (var merged in dictionary.MergedDictionaries.Reverse())
				{
					if (TryGetResourceFromProvider(merged, key, out value))
						return true;
				}
			}

			return false;
		}

		private static string GetThemeFileName(string themeName)
		{
			return themeName
				.Replace("+", "Plus", StringComparison.Ordinal)
				.Replace("#", "Sharp", StringComparison.Ordinal)
				.Replace(" ", string.Empty, StringComparison.Ordinal);
		}

		private void ApplyThemeResourceDictionary(string themeFileName)
		{
			if (Application.Current == null)
				return;

			themeDictionaryContainer.MergedDictionaries.Clear();
			var themeUri = new Uri($"avares://ProjectRover/Themes/Theme.{themeFileName}.axaml");

			try
			{
				var loaded = AvaloniaXamlLoader.Load(themeUri);
				if (loaded is IResourceProvider resourceProvider)
				{
					themeDictionaryContainer.MergedDictionaries.Add(resourceProvider);
					log.Debug("Loaded theme resource dictionary {ThemeUri}", themeUri);
				}
				else
				{
					log.Warning("Loaded theme resource is not an IResourceProvider: {ThemeUri}", themeUri);
				}
			}
			catch (Exception ex)
			{
				log.Warning(ex, "Failed to load theme resource dictionary {ThemeUri}", themeUri);
			}
		}

		private void LoadSyntaxColors(string themeFileName)
		{
			lock (SyntaxColorCacheLock)
			{
				if (!SyntaxColorCache.TryGetValue(themeFileName, out var cached))
				{
					cached = LoadSyntaxColorsFromCurrentThemeResource();
					if (cached.Count == 0)
					{
						cached = LoadSyntaxColorsFromThemeAxaml(themeFileName);
					}

					// Avoid caching transient failures (empty result), so later attempts can recover.
					if (cached.Count > 0)
					{
						SyntaxColorCache[themeFileName] = cached;
					}
				}

				syntaxColors = cached;
			}

			log.Debug("Loaded {Count} syntax color mappings for theme {ThemeFileName}", syntaxColors.Count, themeFileName);
		}

		private static IReadOnlyDictionary<string, SyntaxColorEntry> LoadSyntaxColorsFromThemeAxaml(string themeFileName)
		{
			var themeUri = new Uri($"avares://ProjectRover/Themes/Theme.{themeFileName}.axaml");

			try
			{
				var loaded = AvaloniaXamlLoader.Load(themeUri);
				if (loaded is not IResourceProvider resourceProvider)
				{
					log.Warning(
						"Loaded theme resource dictionary is not an IResourceProvider: {ThemeUri}; Type={Type}",
						themeUri,
						loaded?.GetType().FullName ?? "<null>");
					return EmptySyntaxColors;
				}

				var result = new Dictionary<string, SyntaxColorEntry>(StringComparer.Ordinal);
				CollectSyntaxColors(resourceProvider, result);

				return result;
			}
			catch (Exception ex)
			{
				log.Warning(ex, "Failed to parse syntax colors from theme resource {ThemeUri}", themeUri);
				return EmptySyntaxColors;
			}
		}

		private IReadOnlyDictionary<string, SyntaxColorEntry> LoadSyntaxColorsFromCurrentThemeResource()
		{
			if (themeDictionaryContainer.MergedDictionaries.Count == 0)
			{
				return EmptySyntaxColors;
			}

			var result = new Dictionary<string, SyntaxColorEntry>(StringComparer.Ordinal);
			foreach (var mergedDictionary in themeDictionaryContainer.MergedDictionaries)
			{
				CollectSyntaxColors(mergedDictionary, result);
			}

			return result;
		}

		private static void CollectSyntaxColors(IResourceProvider resourceProvider, Dictionary<string, SyntaxColorEntry> result)
		{
			var syntaxKeysSeen = 0;
			var syntaxKeysResolved = 0;

			if (resourceProvider is IEnumerable<KeyValuePair<object, object?>> entries)
			{
				foreach (var entry in entries)
				{
					if (entry.Key is not string key || !key.StartsWith(SyntaxColorKeyPrefix, StringComparison.Ordinal))
						continue;

					syntaxKeysSeen++;
					// Prefer TryGetResource over raw dictionary entry values because Avalonia may defer materialization.
					// Raw entry values can appear as placeholder instances with unset CLR properties.
					SyntaxColorResource? syntaxResource;
					if (!TryResolveSyntaxColorResource(resourceProvider, key, out syntaxResource))
					{
						syntaxResource = entry.Value as SyntaxColorResource;
					}

					if (syntaxResource == null)
						continue;

					result[key] = new SyntaxColorEntry(
						foreground: ParseColor(syntaxResource.Foreground),
						background: ParseColor(syntaxResource.Background),
						weight: ParseFontWeight(syntaxResource.FontWeight),
						style: ParseFontStyle(syntaxResource.FontStyle));
					syntaxKeysResolved++;
				}
			}

			if (resourceProvider is ResourceDictionary resourceDictionary)
			{
				foreach (var mergedDictionary in resourceDictionary.MergedDictionaries)
				{
					CollectSyntaxColors(mergedDictionary, result);
				}
			}

			if (syntaxKeysSeen > 0 && syntaxKeysResolved == 0)
			{
				log.Warning(
					"Saw {Seen} syntax color keys but resolved none from provider type {ProviderType}",
					syntaxKeysSeen,
					resourceProvider.GetType().FullName);
			}
		}

		private static bool TryResolveSyntaxColorResource(IResourceProvider resourceProvider, string key, out SyntaxColorResource? syntaxResource)
		{
			syntaxResource = null;

			if (resourceProvider.TryGetResource(key, ThemeVariant.Default, out var defaultThemeValue)
				&& defaultThemeValue is SyntaxColorResource resolvedDefault)
			{
				syntaxResource = resolvedDefault;
				return true;
			}

			if (resourceProvider.TryGetResource(key, theme: null, out var unthemedValue)
				&& unthemedValue is SyntaxColorResource resolvedUnthemed)
			{
				syntaxResource = resolvedUnthemed;
				return true;
			}

			return false;
		}

		private static Color? ParseColor(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			return Color.TryParse(value, out var parsedColor) ? parsedColor : null;
		}

		private static FontWeight? ParseFontWeight(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			return Enum.TryParse(value, ignoreCase: true, out FontWeight parsedWeight) ? parsedWeight : null;
		}

		private static FontStyle? ParseFontStyle(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			return Enum.TryParse(value, ignoreCase: true, out FontStyle parsedStyle) ? parsedStyle : null;
		}

		public static HighlightingColor GetColorForDarkTheme(HighlightingColor color)
		{
			if (color.Foreground is null && color.Background is null)
				return color;

			var darkColor = color.Clone();
			darkColor.Foreground = AdjustForDarkTheme(darkColor.Foreground);
			darkColor.Background = AdjustForDarkTheme(darkColor.Background);
			return darkColor;
		}

		private static HighlightingBrush? AdjustForDarkTheme(HighlightingBrush? lightBrush)
		{
			if (lightBrush is not SimpleHighlightingBrush simpleBrush)
				return lightBrush;

			if (simpleBrush.GetBrush(null) is not SolidColorBrush brush)
				return lightBrush;

			var color = brush.Color;
			var (h, s, l) = RgbToHsl(color.R, color.G, color.B);

			// Invert the lightness, but also increase it a bit.
			l = 1f - MathF.Pow(l, 1.2f);

			// Desaturate very saturated colors.
			if (s > 0.75f && l < 0.75f)
			{
				s *= 0.75f;
				l *= 1.2f;
			}

			var (r, g, b) = HslToRgb(h, s, l);
			var newColor = Color.FromArgb(color.A, r, g, b);
			return new SimpleHighlightingBrush(newColor);
		}

		private static (float h, float s, float l) RgbToHsl(byte rB, byte gB, byte bB)
		{
			var r = rB / 255f;
			var g = gB / 255f;
			var b = bB / 255f;

			var max = MathF.Max(r, MathF.Max(g, b));
			var min = MathF.Min(r, MathF.Min(g, b));
			var l = (max + min) / 2f;
			if (MathF.Abs(max - min) < 1e-6f)
			{
				return (0f, 0f, l);
			}

			var d = max - min;
			var s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
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
			if (h < 0)
				h += 360f;

			var c = (1f - MathF.Abs(2f * l - 1f)) * s;
			var hp = h / 60f;
			var x = c * (1f - MathF.Abs(hp % 2f - 1f));
			float r1, g1, b1;
			switch ((int)MathF.Floor(hp))
			{
				case 0:
					r1 = c;
					g1 = x;
					b1 = 0;
					break;
				case 1:
					r1 = x;
					g1 = c;
					b1 = 0;
					break;
				case 2:
					r1 = 0;
					g1 = c;
					b1 = x;
					break;
				case 3:
					r1 = 0;
					g1 = x;
					b1 = c;
					break;
				case 4:
					r1 = x;
					g1 = 0;
					b1 = c;
					break;
				default:
					r1 = c;
					g1 = 0;
					b1 = x;
					break;
			}

			var m = l - c / 2f;
			var rf = (r1 + m) * 255f;
			var gf = (g1 + m) * 255f;
			var bf = (b1 + m) * 255f;

			return (
				(byte)Math.Clamp(rf, 0f, 255f),
				(byte)Math.Clamp(gf, 0f, 255f),
				(byte)Math.Clamp(bf, 0f, 255f));
		}

		public bool IsThemeAware(IHighlightingDefinition highlightingDefinition)
		{
			return highlightingDefinition?.Properties.TryGetValue(IsThemeAwareKey, out var value) == true
				&& value == bool.TrueString;
		}

		public Button CreateButton()
		{
			return new Button();
		}

		internal void ApplyHighlightingColors(IHighlightingDefinition highlightingDefinition)
		{
			if (highlightingDefinition == null)
				return;

			if (highlightingDefinition.NamedHighlightingColors != null)
			{
				foreach (var color in highlightingDefinition.NamedHighlightingColors)
				{
					if (color == null)
						continue;

					try
					{
						SyntaxColorEntry.ResetColor(color);
					}
					catch (InvalidOperationException)
					{
						// Named color may be frozen; skip replacing it.
					}
				}
			}

			var prefix = $"SyntaxColor.{highlightingDefinition.Name}.";
			var appliedMappings = 0;
			foreach (var (key, syntaxColor) in syntaxColors)
			{
				if (!key.StartsWith(prefix, StringComparison.Ordinal))
					continue;

				var colorName = key[prefix.Length..];
				var targetColor = highlightingDefinition.GetNamedColor(colorName);
				if (targetColor is null)
					continue;

				try
				{
					syntaxColor.ApplyTo(targetColor);
					appliedMappings++;
				}
				catch (InvalidOperationException)
				{
					// Named color may be frozen; skip replacing it.
				}
			}

			if (appliedMappings > 0)
			{
				highlightingDefinition.Properties[IsThemeAwareKey] = bool.TrueString;
				return;
			}

			highlightingDefinition.Properties.Remove(IsThemeAwareKey);
			if (IsDarkTheme)
			{
				ApplyDarkThemeFallback(highlightingDefinition);
			}
		}

		private static void ApplyDarkThemeFallback(IHighlightingDefinition highlightingDefinition)
		{
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

			if (highlightingDefinition.MainRuleSet == null)
				return;

			foreach (var rule in highlightingDefinition.MainRuleSet.Rules)
			{
				if (rule is not HighlightingRule { Color: { } color })
					continue;

				try
				{
					rule.Color = GetColorForDarkTheme(color);
				}
				catch (InvalidOperationException)
				{
					// Color may be frozen; skip replacing it.
				}
			}
		}

		private sealed class SyntaxColorEntry
		{
			private readonly Color? foreground;
			private readonly Color? background;
			private readonly FontWeight? weight;
			private readonly FontStyle? style;

			public SyntaxColorEntry(Color? foreground, Color? background, FontWeight? weight, FontStyle? style)
			{
				this.foreground = foreground;
				this.background = background;
				this.weight = weight;
				this.style = style;
			}

			public void ApplyTo(HighlightingColor color)
			{
				color.Foreground = foreground is { } foregroundColor ? new SimpleHighlightingBrush(foregroundColor) : null;
				color.Background = background is { } backgroundColor ? new SimpleHighlightingBrush(backgroundColor) : null;
				color.FontWeight = weight ?? FontWeight.Normal;
				color.FontStyle = style ?? FontStyle.Normal;
			}

			public static void ResetColor(HighlightingColor color)
			{
				color.Foreground = null;
				color.Background = null;
				color.FontWeight = null;
				color.FontStyle = null;
			}
		}
	}

	public class ThemeChangedEventArgs(string? themeName) : EventArgs
	{
		public string? ThemeName { get; } = themeName;
	}

	// Serializable resource shape used by Theme.*.axaml to store migrated SyntaxColor entries.
	public sealed class SyntaxColorResource
	{
		public string? Foreground { get; set; }

		public string? Background { get; set; }

		public string? FontWeight { get; set; }

		public string? FontStyle { get; set; }
	}
}
