using System;
using System.Text.RegularExpressions;

namespace ICSharpCode.ILSpy.Updates
{
	internal readonly struct SemanticVersion : IComparable<SemanticVersion>
	{
		static readonly Regex SemVerRegex = new Regex(
			@"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?",
			RegexOptions.Compiled);

		public int Major { get; }
		public int Minor { get; }
		public int Patch { get; }
		public string? PreRelease { get; }

		public SemanticVersion(int major, int minor, int patch, string? preRelease)
		{
			Major = major;
			Minor = minor;
			Patch = patch;
			PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
		}

		public static bool TryParse(string? value, out SemanticVersion version)
		{
			version = default;
			if (string.IsNullOrWhiteSpace(value))
				return false;

			var match = SemVerRegex.Match(value);
			if (!match.Success)
				return false;

			if (!int.TryParse(match.Groups["major"].Value, out var major)
				|| !int.TryParse(match.Groups["minor"].Value, out var minor)
				|| !int.TryParse(match.Groups["patch"].Value, out var patch))
				return false;

			var preRelease = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
			version = new SemanticVersion(major, minor, patch, preRelease);
			return true;
		}

		public static SemanticVersion FromVersion(Version version)
		{
			return new SemanticVersion(version.Major, version.Minor, Math.Max(0, version.Build), null);
		}

		public Version ToVersion()
		{
			return new Version(Major, Minor, Patch, 0);
		}

		public int CompareTo(SemanticVersion other)
		{
			var majorCompare = Major.CompareTo(other.Major);
			if (majorCompare != 0)
				return majorCompare;

			var minorCompare = Minor.CompareTo(other.Minor);
			if (minorCompare != 0)
				return minorCompare;

			var patchCompare = Patch.CompareTo(other.Patch);
			if (patchCompare != 0)
				return patchCompare;

			var leftPre = PreRelease;
			var rightPre = other.PreRelease;

			if (leftPre == null && rightPre == null)
				return 0;
			if (leftPre == null)
				return 1;
			if (rightPre == null)
				return -1;

			var leftIds = leftPre.Split('.');
			var rightIds = rightPre.Split('.');
			var length = Math.Min(leftIds.Length, rightIds.Length);

			for (var i = 0; i < length; i++)
			{
				var leftId = leftIds[i];
				var rightId = rightIds[i];

				var leftIsNumeric = int.TryParse(leftId, out var leftNum);
				var rightIsNumeric = int.TryParse(rightId, out var rightNum);

				if (leftIsNumeric && rightIsNumeric)
				{
					var numCompare = leftNum.CompareTo(rightNum);
					if (numCompare != 0)
						return numCompare;
				}
				else if (leftIsNumeric != rightIsNumeric)
				{
					return leftIsNumeric ? -1 : 1;
				}
				else
				{
					var idCompare = string.CompareOrdinal(leftId, rightId);
					if (idCompare != 0)
						return idCompare;
				}
			}

			return leftIds.Length.CompareTo(rightIds.Length);
		}
	}
}
