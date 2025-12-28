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

namespace ICSharpCode.ILSpy.Updates
{
	internal static class UpdateService
	{
		const string RepoOwner = "LeXtudio";
		const string RepoName = "ProjectRover";

		static readonly ProductHeaderValue ProductHeader = new ProductHeaderValue("ProjectRover");
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Updates");

		public static AvailableVersionInfo LatestAvailableVersion { get; private set; }
		public static Version? LatestAvailableSemanticVersion { get; private set; }

		public static async Task<AvailableVersionInfo> GetLatestVersionAsync()
		{
					   log.Debug("GetLatestVersionAsync called");
			var client = new GitHubClient(ProductHeader);
			var releaseInfo = await GetLatestReleaseAsync(client).ConfigureAwait(false);
			if (releaseInfo.Release == null || releaseInfo.Version == null)
			{
							   log.Debug("No release info found; returning current version");
				LatestAvailableSemanticVersion = AppUpdateService.CurrentVersion;
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
				Version = releaseInfo.Version,
				DownloadUrl = url
			};
					   log.Information("Found latest release: {Version} (semantic {Semantic}) url={Url}", LatestAvailableVersion.Version, LatestAvailableSemanticVersion, LatestAvailableVersion.DownloadUrl);
			return LatestAvailableVersion;
		}

		static async Task<(Release? Release, Version? Version)> GetLatestReleaseAsync(GitHubClient client)
		{
			try
			{
				// GitHub API already returns the latest non-draft, non-prerelease.
				var latest = await client.Repository.Release.GetLatest(RepoOwner, RepoName).ConfigureAwait(false);
				if (latest == null || latest.Draft || latest.Prerelease)
					return (null, null);

				if (!AppUpdateService.TryParseVersionString(latest.TagName, out var version))
					return (null, null);

				return (latest, version);
			}
			catch
			{
				return (null, null);
			}
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
			{
				log.Debug("Automatic update check disabled in settings.");
				return null;
			}

			// perform update check if we never did one before;
			// or if the last check wasn't in the past 7 days
			if (settings.LastSuccessfulUpdateCheck == null
				|| settings.LastSuccessfulUpdateCheck < DateTime.UtcNow.AddDays(-7)
				|| settings.LastSuccessfulUpdateCheck > DateTime.UtcNow)
			{
				log.Debug("Performing update check based on LastSuccessfulUpdateCheck.");
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
				log.Debug("CheckForUpdateInternal: calling GetLatestVersionAsync");
				var v = await GetLatestVersionAsync().ConfigureAwait(false);
				settings.LastSuccessfulUpdateCheck = DateTime.UtcNow;
				var latest = LatestAvailableSemanticVersion ?? v.Version;
				log.Debug("LatestAvailableSemanticVersion={Latest} AppCurrent={Current}", LatestAvailableSemanticVersion, AppUpdateService.CurrentVersion);

				// If the current app version couldn't be resolved (0.0.0), skip update notifications
				if (AppUpdateService.IsUnset(AppUpdateService.CurrentVersion))
				{
					log.Debug("Current application semantic version appears to be unset (0.0.0); skipping update check.");
					return null;
				}
				bool isNewer = AppUpdateService.IsNewerThanCurrent(latest);
				log.Debug("Is newer: {IsNewer}", isNewer);
				return isNewer ? v.DownloadUrl : null;
			}
			catch (Exception)
			{
				// ignore errors getting the version info
				return null;
			}
		}
	}
}
