namespace HumanFortress.Runtime.Snapshots;

internal static partial class ManagementDrawerSnapshotBuilder
{
    private static string NormalizeKind(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? string.Empty
            : kind.Trim().ToLowerInvariant();
    }

    private static bool IsGenericResourceName(string name)
    {
        var normalized = name.ToLowerInvariant();
        return normalized == "boulder"
            || normalized == "block"
            || normalized == "plank"
            || normalized == "log";
    }

    private static string MaterialSuffixFriendly(string materialId)
    {
        var parts = materialId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return materialId;

        var last = parts[^1];
        return last.Length == 0
            ? materialId
            : char.ToUpperInvariant(last[0]) + last[1..].Replace('_', ' ');
    }
}
