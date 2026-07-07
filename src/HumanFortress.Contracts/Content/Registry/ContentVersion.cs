using System;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Represents a content version with semantic versioning.
/// </summary>
public readonly struct ContentVersion : IComparable<ContentVersion>, IEquatable<ContentVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public ContentVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
            throw new ArgumentException("Version components must be non-negative");

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static ContentVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version string cannot be empty");

        var parts = version.Split('.');
        if (parts.Length != 3)
            throw new FormatException($"Invalid version format: {version}. Expected X.Y.Z");

        if (!int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor) ||
            !int.TryParse(parts[2], out int patch))
        {
            throw new FormatException($"Invalid version components in: {version}");
        }

        return new ContentVersion(major, minor, patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public int CompareTo(ContentVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        return Patch.CompareTo(other.Patch);
    }

    public bool Equals(ContentVersion other) =>
        Major == other.Major && Minor == other.Minor && Patch == other.Patch;

    public override bool Equals(object? obj) =>
        obj is ContentVersion other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Major, Minor, Patch);

    public static bool operator ==(ContentVersion left, ContentVersion right) =>
        left.Equals(right);

    public static bool operator !=(ContentVersion left, ContentVersion right) =>
        !left.Equals(right);

    public static bool operator <(ContentVersion left, ContentVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(ContentVersion left, ContentVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(ContentVersion left, ContentVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(ContentVersion left, ContentVersion right) =>
        left.CompareTo(right) >= 0;

    /// <summary>
    /// Checks if this version is compatible with a required version
    /// using semantic versioning rules (major must match, minor/patch can be higher)
    /// </summary>
    public bool IsCompatibleWith(ContentVersion required)
    {
        // Different major version = incompatible
        if (Major != required.Major)
            return false;

        // Same major, higher or equal minor = compatible
        if (Minor > required.Minor)
            return true;

        // Same major and minor, higher or equal patch = compatible
        return Minor == required.Minor && Patch >= required.Patch;
    }
}
