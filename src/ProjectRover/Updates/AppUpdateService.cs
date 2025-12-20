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
using NuGet.Versioning;

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

		public static readonly NuGetVersion CurrentSemanticVersion = ResolveCurrentSemanticVersion();

		public static readonly Version CurrentVersion = CurrentSemanticVersion.Version;

		static NuGetVersion ResolveCurrentSemanticVersion()
		{
			var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (!string.IsNullOrWhiteSpace(info))
			{
				if (NuGetVersion.TryParse(info, out var semanticVersion))
					return semanticVersion;

				var plusIndex = info.IndexOf('+');
				if (plusIndex > 0 && NuGetVersion.TryParse(info.Substring(0, plusIndex), out semanticVersion))
					return semanticVersion;
			}

			var version = assembly.GetName().Version ?? new Version(0, 0, 0, 0);
			return new NuGetVersion(version);
		}
	}
}
