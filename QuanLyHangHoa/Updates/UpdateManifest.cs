using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuanLyHangHoa.Updates;

public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> Prerelease) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Semantic version cannot be empty.");
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var sections = normalized.Split('-', 2);
        var core = sections[0].Split('.');
        if (core.Length != 3
            || !int.TryParse(core[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(core[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(core[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
            || major < 0
            || minor < 0
            || patch < 0)
        {
            throw new FormatException($"Invalid semantic version: {value}");
        }

        var prerelease = sections.Length == 2
            ? sections[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
            : [];
        if (sections.Length == 2 && prerelease.Length == 0)
        {
            throw new FormatException($"Invalid semantic version: {value}");
        }

        return new SemanticVersion(major, minor, patch, prerelease);
    }

    public int CompareTo(SemanticVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        if (Prerelease.Count == 0 && other.Prerelease.Count == 0) return 0;
        if (Prerelease.Count == 0) return 1;
        if (other.Prerelease.Count == 0) return -1;

        for (var index = 0; index < Math.Max(Prerelease.Count, other.Prerelease.Count); index++)
        {
            if (index >= Prerelease.Count) return -1;
            if (index >= other.Prerelease.Count) return 1;

            var left = Prerelease[index];
            var right = other.Prerelease[index];
            var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            var comparison = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : leftNumeric
                    ? -1
                    : rightNumeric
                        ? 1
                        : string.Compare(left, right, StringComparison.Ordinal);
            if (comparison != 0) return comparison;
        }

        return 0;
    }

    public override string ToString() =>
        Prerelease.Count == 0
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{string.Join('.', Prerelease)}";
}

public sealed class UpdateManifest
{
    public int SchemaVersion { get; set; }
    public string Version { get; set; } = string.Empty;
    public string MinimumClientVersion { get; set; } = string.Empty;
    public int MinimumSchemaVersion { get; set; }
    public int MaximumSchemaVersion { get; set; }
    public string InstallerAssetName { get; set; } = string.Empty;
    public long InstallerSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool Mandatory { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;
}

public sealed record UpdateRelease(
    SemanticVersion Version,
    bool Draft,
    bool Prerelease,
    Uri InstallerUri,
    long InstallerSize,
    UpdateManifest Manifest);

public sealed record UpdateCandidate(UpdateRelease Release, bool Mandatory)
{
    public SemanticVersion Version => Release.Version;
}

public sealed record PreparedUpdate(string Version, string InstallerPath);

public enum UpdateCheckStatus
{
    NoUpdate,
    UpdateAvailable,
    Offline,
    Skipped,
    InvalidRelease
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateCandidate? Candidate = null,
    string? ErrorCode = null);
