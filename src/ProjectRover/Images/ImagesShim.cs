using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Concurrent;

namespace ICSharpCode.ILSpy
{
    internal static class Images
    {
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Images");
		// Simple in-memory cache for loaded images keyed by the resolved asset URI.
		// Key: absolute avares:// URI used to load the SvgSource
		// Value: cached IImage (SvgImage) instance
		private static readonly ConcurrentDictionary<string, IImage> imageCache = new ConcurrentDictionary<string, IImage>(StringComparer.OrdinalIgnoreCase);
		// Cache for composed images (base+overlay+static)
		private static readonly ConcurrentDictionary<string, IImage> composedCache = new ConcurrentDictionary<string, IImage>(StringComparer.OrdinalIgnoreCase);

		// Diagnostic helpers: track unresolved resource keys and failed candidate paths.
		private static readonly ConcurrentDictionary<string, int> unresolvedKeys = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private static readonly ConcurrentDictionary<string, int> failedCandidates = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private static readonly bool writeDiagnostics = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROJECTROVER_IMAGE_DIAG"));
		private static readonly Regex hexColorRegex = new Regex("#(?<hex>[0-9a-fA-F]{3}|[0-9a-fA-F]{6})", RegexOptions.Compiled);

		private static readonly string[] AccessSuffixes = new[]
		{
			"PrivateProtectedIcon",
			"ProtectedInternalIcon",
			"PrivateIcon",
			"ProtectedIcon",
			"InternalIcon",
			"PublicIcon"
		};

		private static readonly string[] AccessNames = new[]
		{
			"PrivateProtected",
			"ProtectedInternal",
			"Private",
			"Protected",
			"Internal",
			"Public"
		};

		private static void TryWriteDiagnostics()
		{
			if (!writeDiagnostics) return;
			try
			{
				var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
				var diagPath = Path.Combine(baseDir, "image-diagnostics.txt");
				using (var sw = File.CreateText(diagPath))
				{
					sw.WriteLine("Unresolved keys:");
					foreach (var kv in unresolvedKeys.OrderByDescending(k => k.Value))
					{
						sw.WriteLine($"{kv.Key}: {kv.Value}");
					}
					sw.WriteLine();
					sw.WriteLine("Failed candidate paths:");
					foreach (var kv in failedCandidates.OrderByDescending(k => k.Value))
					{
						sw.WriteLine($"{kv.Key}: {kv.Value}");
					}
				}
			}
			catch
			{
				// best-effort only
			}
		}

		private static bool IsDarkTheme()
		{
			return ICSharpCode.ILSpy.Themes.ThemeManager.Current.IsDarkTheme;
		}

		private static object? GetCachedThemeVariant()
		{
			return ICSharpCode.ILSpy.Themes.ThemeManager.Current.GetCachedThemeVariant();
		}

		private static bool ShouldPreferGrayInvert()
		{
			var env = Environment.GetEnvironmentVariable("PROJECTROVER_ICON_GRAY_INVERT");
			if (string.IsNullOrWhiteSpace(env)) return true;
			return env.Equals("1", StringComparison.OrdinalIgnoreCase) || env.Equals("true", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsLightAssetPath(string path)
		{
			return !path.Contains("/Assets/Dark/", StringComparison.OrdinalIgnoreCase)
				&& !path.Contains("/ProjectRover/Assets/Dark/", StringComparison.OrdinalIgnoreCase);
		}

		private static string ToLightAssetPath(string path)
		{
			var replaced = path.Replace("/Assets/Dark/", "/Assets/");
			replaced = replaced.Replace("/ProjectRover/Assets/Dark/", "/ProjectRover/Assets/");
			return replaced;
		}

		private static bool TryReadSvg(string path, out string svg)
		{
			svg = string.Empty;
			try
			{
				using var stream = AssetLoader.Open(new Uri(path));
				using var reader = new StreamReader(stream);
				svg = reader.ReadToEnd();
				return true;
			}
			catch (Exception ex)
			{
				log.Warning(ex, "Images.LoadImage: failed to read svg {Path}", path);
				return false;
			}
		}

		private static bool TryParseHexColor(string hex, out byte r, out byte g, out byte b)
		{
			r = g = b = 0;
			if (hex.Length == 3)
			{
				if (byte.TryParse(hex.Substring(0, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rNib)
					&& byte.TryParse(hex.Substring(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gNib)
					&& byte.TryParse(hex.Substring(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bNib))
				{
					r = (byte)(rNib * 17);
					g = (byte)(gNib * 17);
					b = (byte)(bNib * 17);
					return true;
				}
				return false;
			}
			if (hex.Length == 6)
			{
				if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
					&& byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
					&& byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
				{
					return true;
				}
				return false;
			}
			return false;
		}

		private static string InvertGraySvg(string svg)
		{
			if (string.IsNullOrEmpty(svg)) return svg;
			return hexColorRegex.Replace(svg, match =>
			{
				var hex = match.Groups["hex"].Value;
				if (!TryParseHexColor(hex, out var r, out var g, out var b))
					return match.Value;
				if (r != g || g != b)
					return match.Value;
				var inv = (byte)(255 - r);
				var invHex = inv.ToString("X2", CultureInfo.InvariantCulture);
				return "#" + invHex + invHex + invHex;
			});
		}

		// Helper type to represent a composite icon request
		private sealed class CompositeIcon
		{
			public object Base { get; }
			public object? Overlay { get; }
			public bool IsStatic { get; }

			public CompositeIcon(object @base, object? overlay, bool isStatic)
			{
				Base = @base;
				Overlay = overlay;
				IsStatic = isStatic;
			}
		}

		private readonly struct SvgCandidate
		{
			public string Path { get; }
			public bool InvertGray { get; }

			public SvgCandidate(string path, bool invertGray)
			{
				Path = path;
				InvertGray = invertGray;
			}
		}

		private sealed class CompositeImage : IImage
		{
			private readonly IImage? baseImage;
			private readonly IImage? overlayImage;
			private readonly IImage? staticImage;

			public CompositeImage(IImage? baseImage, IImage? overlayImage, IImage? staticImage)
			{
				this.baseImage = baseImage;
				this.overlayImage = overlayImage;
				this.staticImage = staticImage;

				var width = 0.0;
				var height = 0.0;
				if (baseImage != null)
				{
					width = Math.Max(width, baseImage.Size.Width);
					height = Math.Max(height, baseImage.Size.Height);
				}
				if (overlayImage != null)
				{
					width = Math.Max(width, overlayImage.Size.Width);
					height = Math.Max(height, overlayImage.Size.Height);
				}
				if (staticImage != null)
				{
					width = Math.Max(width, staticImage.Size.Width);
					height = Math.Max(height, staticImage.Size.Height);
				}
				if (width <= 0 || height <= 0)
				{
					width = height = 16;
				}
				Size = new Size(width, height);
			}

			public Size Size { get; }

			public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
			{
				if (baseImage == null && overlayImage == null && staticImage == null)
					return;

				var crop = NormalizeSourceRect(sourceRect, Size);
				if (baseImage != null)
				{
					// WPF ILSpy parity: scale the base image to 80% and center it in the dest rect,
					// then draw the overlay at full size on top of that.
					var baseDest = destRect;
					var baseWidth = Math.Max(1.0, Math.Round(destRect.Width * 0.8));
					var baseHeight = Math.Max(1.0, Math.Round(destRect.Height * 0.8));
					var baseX = destRect.X + Math.Round((destRect.Width - baseWidth) / 2.0);
					var baseY = destRect.Y + Math.Round((destRect.Height - baseHeight) / 2.0);
					baseDest = new Rect(baseX, baseY, baseWidth, baseHeight);
					baseImage.Draw(context, MapSourceRect(baseImage.Size, crop), baseDest);
				}

				if (overlayImage != null)
				{
					overlayImage.Draw(context, MapSourceRect(overlayImage.Size, crop), destRect);
				}

				if (staticImage != null)
				{
					staticImage.Draw(context, MapSourceRect(staticImage.Size, crop), destRect);
				}
			}

			private static Rect NormalizeSourceRect(Rect sourceRect, Size size)
			{
				if (size.Width <= 0 || size.Height <= 0)
					return new Rect(0, 0, 1, 1);

				var x = sourceRect.X / size.Width;
				var y = sourceRect.Y / size.Height;
				var w = sourceRect.Width / size.Width;
				var h = sourceRect.Height / size.Height;

				return new Rect(Clamp01(x), Clamp01(y), Clamp01(w), Clamp01(h));
			}

			private static Rect MapSourceRect(Size imageSize, Rect crop)
			{
				return new Rect(
					imageSize.Width * crop.X,
					imageSize.Height * crop.Y,
					imageSize.Width * crop.Width,
					imageSize.Height * crop.Height);
			}

			private static double Clamp01(double value)
			{
				if (value < 0) return 0;
				if (value > 1) return 1;
				return value;
			}
		}

        private static string GetUri(string path) => $"avares://ProjectRover/Assets/{path}";

        public static object OK => GetUri("StatusOKOutline.svg");

		private static bool TryGetResourceSafe(string key, out object? result)
		{
			var app = App.Current;
			var variant = GetCachedThemeVariant();
			result = null;
			if (app == null || variant == null)
				return false;
			try
			{
				return app.TryGetResource(key, (Avalonia.Styling.ThemeVariant?)variant, out result);
			}
			catch
			{
				return false;
			}
		}

		private static bool HasIconResource(string key)
		{
			if (App.Current == null)
				return true; // don't block on startup; let resolve happen later
			return TryGetResourceSafe(key, out var res) && res is string;
		}

		private static string NormalizeBaseKey(string key)
		{
			if (HasIconResource(key))
				return key;

			// Strip access suffix and retry: MethodIconInternal -> MethodIcon
			foreach (var suffix in AccessSuffixes)
			{
				if (key.EndsWith(suffix, StringComparison.Ordinal))
				{
					var fallback = key.Substring(0, key.Length - suffix.Length) + "Icon";
					if (HasIconResource(fallback))
						return fallback;
				}
			}

			// Handle member-style keys: MethodIconPrivate -> MethodIcon
			foreach (var access in AccessNames)
			{
				if (key.EndsWith(access, StringComparison.Ordinal))
				{
					var fallback = key.Substring(0, key.Length - access.Length);
					if (fallback.EndsWith("Icon", StringComparison.Ordinal) && HasIconResource(fallback))
						return fallback;
				}
			}

			// As a last resort, if the key didn't end with Icon, append it.
			if (!key.EndsWith("Icon", StringComparison.Ordinal))
			{
				var appended = key + "Icon";
				if (HasIconResource(appended))
					return appended;
			}

			return key; // fall back to original; caller will log if unresolved
		}
        
        public static object Load(object owner, string path)
        {
             if (string.IsNullOrEmpty(path)) return null;
             if (path.EndsWith(".svg")) return GetUri(path);
             return path;
        }

        public static string? ResolveIcon(object? icon)
        {
            if (icon is string s)
            {
                if (s.StartsWith("avares://") || s.StartsWith("/")) return s;
				// Support legacy toolbar metadata like "Images/Open"
				if (s.StartsWith("Images/"))
				{
					var name = Path.GetFileName(s);
					return $"/Assets/{name}.svg";
				}
				if (TryGetResourceSafe(s, out var res) && res is string p)
				{
					log.Debug("Images.ResolveIcon: resolved resource key {Key} -> {Path}", s, p);
					return p;
				}
				// Fallback: try an Icon-suffixed resource key if the plain key is missing
				if (!s.EndsWith("Icon", StringComparison.Ordinal) &&
					TryGetResourceSafe(s + "Icon", out res) && res is string p2)
				{
					log.Debug("Images.ResolveIcon: resolved fallback key {Key} -> {Path}", s + "Icon", p2);
					return p2;
				}
				var normalized = NormalizeBaseKey(s);
				if (!string.Equals(normalized, s, StringComparison.Ordinal) &&
					TryGetResourceSafe(normalized, out res) && res is string p3)
				{
					log.Debug("Images.ResolveIcon: resolved normalized key {Key} -> {Path}", normalized, p3);
					return p3;
				}
            }
            return null;
        }

		public static IImage? LoadImage(object? icon)
		{
			// Avoid noisy logs for callers that intentionally pass null (many UI elements don't have icons).
			// Only emit the standard request log when a non-null icon is provided; when a non-null
			// key fails to resolve we'll log that below to help diagnose missing resource keys.
			if (icon != null)
			{
				log.Debug("Images.LoadImage: requested icon: {Icon}", icon);
			}

			// Handle composite icon requests (base + overlay + optional static badge)
			if (icon is CompositeIcon comp)
			{
				log.Debug("Images.LoadImage: composite request base={Base} overlay={Overlay} isStatic={IsStatic}", comp.Base, comp.Overlay, comp.IsStatic);
				// Try to build cache key from resolved paths when possible
				string baseKey = ResolveIcon(comp.Base) ?? comp.Base?.ToString() ?? "";
				string overlayKey = comp.Overlay != null ? ResolveIcon(comp.Overlay) ?? comp.Overlay.ToString() ?? "" : "";
				string staticKey = comp.IsStatic ? ResolveIcon("OverlayStaticIcon") ?? ResolveIcon("OverlayStatic") ?? "/Assets/OverlayStatic.svg" : "";
				log.Debug("Images.LoadImage: composite resolved keys base={BaseKey} overlay={OverlayKey} static={StaticKey}", baseKey, overlayKey, staticKey);
				var cacheKey = baseKey + "|" + overlayKey + "|" + staticKey;
				if (composedCache.TryGetValue(cacheKey, out var cachedComposed))
					return cachedComposed;

				var baseImg = LoadImage(comp.Base);
				var overlayImg = comp.Overlay != null ? LoadImage(comp.Overlay) : null;
				var staticImg = comp.IsStatic ? LoadImage("OverlayStaticIcon") ?? LoadImage("OverlayStatic") : null;

				var composed = ComposeImages(baseImg, overlayImg, staticImg);
				if (composed != null)
				{
					composedCache.TryAdd(cacheKey, composed);
				}
				return composed;
			}

			string? path = ResolveIcon(icon);
			// If the caller passed a non-null key but resolution returned null, log that — it
			// usually indicates a missing resource key in App.axaml or a misspelt lookup.
			if (icon != null && path == null)
			{
				// Track unresolved resource keys so we can report which logical keys are missing
				// from App.axaml resource dictionaries.
				unresolvedKeys.AddOrUpdate(icon.ToString() ?? "(null)", 1, (_, v) => v + 1);
				log.Information("Images.LoadImage: ResolveIcon returned null for key {Icon}", icon);
				if (writeDiagnostics)
				{
					TryWriteDiagnostics();
				}
				return null;
			}
			if (path == null) return null;

			if (path.StartsWith("/"))
			{
				path = $"avares://ProjectRover{path}";
			}

			if (path.EndsWith(".svg"))
			{
				var isDark = IsDarkTheme();
				var preferGrayInvert = isDark && ShouldPreferGrayInvert();
				var candidates = new System.Collections.Generic.List<SvgCandidate>();
				var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

				void AddCandidate(string? candidatePath, bool invertGray)
				{
					if (string.IsNullOrWhiteSpace(candidatePath)) return;
					var key = invertGray ? candidatePath + "|invertgray" : candidatePath;
					if (seen.Add(key))
					{
						candidates.Add(new SvgCandidate(candidatePath, invertGray));
					}
				}

				if (isDark && preferGrayInvert)
				{
					AddCandidate(ToLightAssetPath(path), true);
				}

				var invertOriginal = preferGrayInvert && isDark && IsLightAssetPath(path);
				AddCandidate(path, invertOriginal);

				foreach (var candidate in candidates)
				{
					try
					{
						var p = candidate.Path;
						if (!p.Contains("://"))
						{
							p = $"avares://ProjectRover/Assets/{p}";
						}
						var cacheKey = candidate.InvertGray ? p + "|invertgray" : p;
						if (imageCache.TryGetValue(cacheKey, out var cached))
						{
							log.Debug("Images.LoadImage: cache hit for {Path}", cacheKey);
							return cached;
						}

						SvgSource? svg = null;
						if (candidate.InvertGray)
						{
							if (TryReadSvg(p, out var svgText))
							{
								var inverted = InvertGraySvg(svgText);
								svg = SvgSource.LoadFromSvg(inverted);
							}
						}
						else
						{
							svg = SvgSource.Load(p, null);
						}

						if (svg != null)
						{
							var svgImage = new SvgImage { Source = svg };
							log.Debug("Images.LoadImage: loaded svg for {Path}", cacheKey);
							return imageCache.GetOrAdd(cacheKey, svgImage);
						}

						failedCandidates.AddOrUpdate(cacheKey, 1, (_, v) => v + 1);
						log.Warning("Images.LoadImage: SvgSource.Load returned null for candidate {Candidate} icon={Icon}", cacheKey, icon);
					}
					catch (Exception ex)
					{
						var cacheKey = candidate.InvertGray ? candidate.Path + "|invertgray" : candidate.Path;
						failedCandidates.AddOrUpdate(cacheKey, 1, (_, v) => v + 1);
						log.Warning(ex, "Images.LoadImage: failed to load candidate {Candidate} for icon {Icon}", cacheKey, icon);
					}
				}
				return null;
			}
			return null;
		}

		private static IImage? ComposeImages(IImage? baseImg, IImage? overlayImg, IImage? staticImg)
		{
			if (baseImg == null) return overlayImg ?? staticImg;

			try
			{
				return new CompositeImage(baseImg, overlayImg, staticImg);
			}
			catch
			{
				log.Error("Images.ComposeImages: composition failed, returning base image");
				return baseImg;
			}
		}

		internal static object GetIcon(object icon, object overlay, bool isStatic)
		{
			string name = null;
			string access = "Public";

			if (overlay is AccessOverlayIcon accessOverlay)
			{
				switch (accessOverlay)
				{
					case AccessOverlayIcon.Public: access = "Public"; break;
					case AccessOverlayIcon.Internal: access = "Internal"; break;
					case AccessOverlayIcon.Protected: access = "Protected"; break;
					case AccessOverlayIcon.Private: access = "Private"; break;
					case AccessOverlayIcon.ProtectedInternal: access = "Protected"; break;
					case AccessOverlayIcon.PrivateProtected: access = "Private"; break;
					case AccessOverlayIcon.CompilerControlled: access = "Private"; break;
				}
			}

			if (icon is TypeIcon typeIcon)
			{
				switch (typeIcon)
				{
					case TypeIcon.Class: name = "Class"; break;
					case TypeIcon.Struct: name = "Struct"; break;
					case TypeIcon.Interface: name = "Interface"; break;
					case TypeIcon.Delegate: name = "Delegate"; break;
					case TypeIcon.Enum: name = "Enum"; break;
					default: name = "Class"; break;
				}
			}
			else if (icon is MemberIcon memberIcon)
			{
				switch (memberIcon)
				{
					case MemberIcon.Literal: name = "Constant"; break;
					case MemberIcon.FieldReadOnly: name = "Field"; break;
					case MemberIcon.Field: name = "Field"; break;
					case MemberIcon.Property: name = "Property"; break;
					case MemberIcon.Method: name = "Method"; break;
					case MemberIcon.Event: name = "Event"; break;
					case MemberIcon.EnumValue: name = "Enum"; break;
					case MemberIcon.Constructor: name = "Constructor"; break;
					case MemberIcon.VirtualMethod: name = "Method"; break;
					case MemberIcon.Operator: name = "Method"; break;
					case MemberIcon.ExtensionMethod: name = "Method"; break;
					case MemberIcon.PInvokeMethod: name = "Method"; break;
					case MemberIcon.Indexer: name = "Property"; break;
					default: name = "Method"; break;
				}
			}

			if (name != null)
			{
				string baseKey;
				if (name == "Class" || name == "Struct" || name == "Interface" || name == "Enum" || name == "Delegate")
				{
					baseKey = access == "Public" ? $"{name}Icon" : $"{name}{access}Icon";
				}
				else
				{
					baseKey = $"{name}Icon{access}";
				}
				baseKey = NormalizeBaseKey(baseKey);

				// Determine overlay resource name (if any)
				string? overlayName = null;
				if (overlay is AccessOverlayIcon ao)
				{
					switch (ao)
					{
						case AccessOverlayIcon.Public: overlayName = null; break;
						case AccessOverlayIcon.Internal: overlayName = "OverlayInternalIcon"; break;
						case AccessOverlayIcon.Protected: overlayName = "OverlayProtectedIcon"; break;
						case AccessOverlayIcon.Private: overlayName = "OverlayPrivateIcon"; break;
						case AccessOverlayIcon.ProtectedInternal: overlayName = "OverlayProtectedInternalIcon"; break;
						case AccessOverlayIcon.PrivateProtected: overlayName = "OverlayPrivateProtectedIcon"; break;
						case AccessOverlayIcon.CompilerControlled: overlayName = "OverlayCompilerControlledIcon"; break;
						default: overlayName = null; break;
					}
				}
				else if (overlay is string sOverlay)
				{
					overlayName = sOverlay;
				}

				if (!string.IsNullOrEmpty(overlayName))
				{
					return new CompositeIcon(baseKey, overlayName, isStatic);
				}
				return baseKey;
			}

			return "ClassIcon";
		}

		internal static object GetOverlayIcon(Accessibility accessibility)
		{
			switch (accessibility)
			{
				case Accessibility.Public: return AccessOverlayIcon.Public;
				case Accessibility.Internal: return AccessOverlayIcon.Internal;
				case Accessibility.Protected: return AccessOverlayIcon.Protected;
				case Accessibility.Private: return AccessOverlayIcon.Private;
				case Accessibility.ProtectedOrInternal: return AccessOverlayIcon.ProtectedInternal;
				case Accessibility.ProtectedAndInternal: return AccessOverlayIcon.PrivateProtected;
				case Accessibility.None: return AccessOverlayIcon.CompilerControlled;
				default: return AccessOverlayIcon.Public;
			}
		}

		public static object SubTypes => "SubTypesIcon"; // Missing in App.axaml?

		// Back-compat mappings for icons referenced by the original ILSpy code
		public static object NuGet => "AssemblyListIcon";
		public static object ProgramDebugDatabase => "ProgramDebugDatabaseIcon";
		public static object WebAssemblyFile => "WebAssemblyIcon";

		public static object ListFolder => "FolderClosedIcon";
		public static object ListFolderOpen => "FolderOpenIcon";

		// DirectoryTable mapping for Data Directory / Debug Directory nodes
		public static object DirectoryTable => "MetadataTableIcon";

		public static object Header => "HeaderIcon";
		public static object MetadataTableGroup => "MetadataTableGroupIcon";
		public static object Library => "AssemblyIcon";
		public static object Namespace => "NamespaceIcon";
		public static object FolderClosed => "FolderClosedIcon";
		public static object FolderOpen => "FolderOpenIcon";
		public static object MetadataTable => "MetadataTableIcon";
		public static object ExportedType => "ClassIcon";
		public static object TypeReference => "ClassIcon";
		public static object MethodReference => "MethodIcon";
		public static object FieldReference => "FieldIcon";
		public static object Interface => "InterfaceIcon";
		public static object Class => "ClassIcon";
		public static object Field => "FieldIcon";
		public static object Method => "MethodIcon";
		public static object Property => "PropertyIcon";
		public static object Event => "EventIcon";
		public static object Literal => "LiteralIcon";
		public static object Save => "SaveIcon";
		public static object Assembly => "AssemblyIcon";
		public static object ViewCode => "ViewCodeIcon";
		public static object AssemblyWarning => "AssemblyWarningIcon";
		public static object AssemblyLoading => "AssemblyLoadingIcon";
		public static object MetadataFile => "MetadataIcon";
		public static object FindAssembly => "OpenIcon";
		public static object SuperTypes => "SuperTypesIcon"; // Missing?
		public static object ReferenceFolder => "ReferenceFolderIcon";
		public static object ResourceImage => "ResourceImageIcon";
		public static object Resource => "ResourceIcon";
		public static object ResourceResourcesFile => "ResourceResourcesFileIcon";
		public static object ResourceXml => "ResourceXmlIcon";
		public static object ResourceXsd => "ResourceXsdIcon";
		public static object ResourceXslt => "ResourceXsltIcon";
		public static object Heap => "HeapIcon";
		public static object Metadata => "MetadataIcon";

		public static object Search => "SearchIcon";
	}
}
