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
using System.Threading.Tasks;
using Octokit;
using NuGet.Versioning;

namespace ICSharpCode.ILSpy.Updates
{
	internal static class UpdateService
	{
		const string RepoOwner = "LeXtudio";
		const string RepoName = "ProjectRover";
		const bool IncludePrereleases = true;

		static readonly ProductHeaderValue ProductHeader = new ProductHeaderValue("ProjectRover");

		public static AvailableVersionInfo LatestAvailableVersion { get; private set; }
		public static NuGetVersion? LatestAvailableSemanticVersion { get; private set; }

		public static async Task<AvailableVersionInfo> GetLatestVersionAsync()
		{
			var client = new GitHubClient(ProductHeader);
			var releaseInfo = await GetLatestReleaseAsync(client).ConfigureAwait(false);
			if (releaseInfo.Release == null || releaseInfo.Version == null)
			{
				LatestAvailableSemanticVersion = AppUpdateService.CurrentSemanticVersion;
				LatestAvailableVersion = new AvailableVersionInfo {
					Version = AppUpdateService.CurrentVersion,
					DownloadUrl = null
				};
				return LatestAvailableVersion;
			}

			var url = releaseInfo.Release.HtmlUrl?.ToString();
			if (!IsHttpUrl(url))
				url = null;

			LatestAvailableSemanticVersion = releaseInfo.Version;
			LatestAvailableVersion = new AvailableVersionInfo {
				Version = releaseInfo.Version.Version,
				DownloadUrl = url
			};
			return LatestAvailableVersion;
		}

		static async Task<(Release? Release, NuGetVersion? Version)> GetLatestReleaseAsync(GitHubClient client)
		{
			var releases = await client.Repository.Release.GetAll(RepoOwner, RepoName).ConfigureAwait(false);
			Release? bestRelease = null;
			NuGetVersion? bestVersion = null;
			var hasBest = false;

			foreach (var release in releases)
			{
				if (release.Draft)
					continue;
				if (!IncludePrereleases && release.Prerelease)
					continue;

				if (!TryParseReleaseVersion(release, out var version))
					continue;

				if (!hasBest || (bestVersion != null && version.CompareTo(bestVersion) > 0))
				{
					bestRelease = release;
					bestVersion = version;
					hasBest = true;
				}
			}

			if (!hasBest || bestRelease == null || bestVersion == null)
				return (null, null);

			return (bestRelease, bestVersion);
		}

		static bool TryParseReleaseVersion(Release release, out NuGetVersion version)
		{
			return TryParseNuGetVersion(release.TagName, out version)
				|| TryParseNuGetVersion(release.Name, out version);
		}

		static bool TryParseNuGetVersion(string? value, out NuGetVersion version)
		{
			version = null;
			if (string.IsNullOrWhiteSpace(value))
				return false;

			var trimmed = value.Trim();
			if (trimmed.Length > 1 && (trimmed[0] == 'v' || trimmed[0] == 'V') && char.IsDigit(trimmed[1]))
				trimmed = trimmed.Substring(1);

			return NuGetVersion.TryParse(trimmed, out version);
		}

		static bool IsHttpUrl(string? url)
		{
			return !string.IsNullOrWhiteSpace(url)
				&& (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
					|| url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// If automatic update checking is enabled, checks if there are any updates available.
		/// Returns the download URL if an update is available.
		/// Returns null if no update is available, or if no check was performed.
		/// </summary>
		public static async Task<string> CheckForUpdatesIfEnabledAsync(UpdateSettings settings)
		{
			if (!settings.AutomaticUpdateCheckEnabled)
				return null;

			// perform update check if we never did one before;
			// or if the last check wasn't in the past 7 days
			if (settings.LastSuccessfulUpdateCheck == null
				|| settings.LastSuccessfulUpdateCheck < DateTime.UtcNow.AddDays(-7)
				|| settings.LastSuccessfulUpdateCheck > DateTime.UtcNow)
			{
				return await CheckForUpdateInternal(settings).ConfigureAwait(false);
			}

			return null;
		}

		public static Task<string> CheckForUpdatesAsync(UpdateSettings settings)
		{
			return CheckForUpdateInternal(settings);
		}

		static async Task<string> CheckForUpdateInternal(UpdateSettings settings)
		{
			try
			{
				var v = await GetLatestVersionAsync().ConfigureAwait(false);
				settings.LastSuccessfulUpdateCheck = DateTime.UtcNow;
				var latest = LatestAvailableSemanticVersion ?? new NuGetVersion(v.Version);
				return latest.CompareTo(AppUpdateService.CurrentSemanticVersion) > 0 ? v.DownloadUrl : null;
			}
			catch (Exception)
			{
				// ignore errors getting the version info
				return null;
			}
		}
	}
}
