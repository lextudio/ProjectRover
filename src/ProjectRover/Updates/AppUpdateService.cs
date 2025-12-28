// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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

using System;
using System.Reflection;

namespace ICSharpCode.ILSpy.Updates
{
	internal enum UpdateStrategy
	{
		NotifyOfUpdates,
		// AutoUpdate
	}

	internal static class AppUpdateService
	{
		public static readonly UpdateStrategy updateStrategy = UpdateStrategy.NotifyOfUpdates;

		public static readonly Version CurrentVersion = ResolveCurrentVersion();

		static Version ResolveCurrentVersion()
		{
			var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			// First prefer AssemblyInformationalVersion (may include prerelease/build metadata)
			var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
					ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("[AppUpdateService] Assembly InformationalVersion attribute: {Info}", info);
			if (TryParseVersionString(info, out var semanticVersion))
				return semanticVersion;

			// If InformationalVersion is not set, prefer AssemblyFileVersionAttribute
			var fileVer = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
					ICSharpCode.ILSpy.Util.RoverLog.Log.Debug("[AppUpdateService] Assembly FileVersion attribute: {FileVer}", fileVer);
			if (TryParseVersionString(fileVer, out var fileVersion))
				return fileVersion;

			var version = assembly.GetName().Version ?? new Version(0, 0, 0, 0);
			return NormalizeToMajorMinorPatch(version);
		}

		public static bool IsUnset(Version? version)
		{
			return version == null
				|| (version.Major == 0 && version.Minor == 0 && version.Build == 0);
		}

		public static bool IsNewerThanCurrent(Version? candidate)
		{
			return candidate != null && candidate > CurrentVersion;
		}

		public static bool TryParseVersionString(string? raw, out Version version)
		{
			version = null;
			if (string.IsNullOrWhiteSpace(raw))
				return false;

			var trimmed = raw.Trim();
			// Strip leading 'v' if present (e.g., v1.2.3)
			if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed.Substring(1);

			return Version.TryParse(trimmed, out version);
		}

		static Version NormalizeToMajorMinorPatch(Version v)
		{
			if (v == null) return new Version(0, 0, 0);
			return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
		}
	}
}
