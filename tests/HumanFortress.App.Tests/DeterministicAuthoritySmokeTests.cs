using System.Text.RegularExpressions;

internal static class DeterministicAuthoritySmokeTests
{
    private static readonly (string Name, Regex Pattern)[] ForbiddenAuthorityPatterns =
    {
        ("object GetHashCode()", new Regex(@"\bGetHashCode\s*\(", RegexOptions.Compiled)),
        ("dictionary Keys/Values view enumeration", new Regex(@"\.(Keys|Values)\b", RegexOptions.Compiled)),
        ("production Guid.NewGuid()", new Regex(@"\bGuid\.NewGuid\s*\(", RegexOptions.Compiled))
    };

    public static void RunAll()
    {
        Console.WriteLine("=== Deterministic Authority Smoke Tests ===");

        string root = TestRepositoryPaths.FindRepositoryRoot();
        TestSaveReplayAndHashAuthorityAvoidsUnstableInputs(root);

        Console.WriteLine("=== Deterministic Authority Smoke Tests Completed ===\n");
    }

    private static void TestSaveReplayAndHashAuthorityAvoidsUnstableInputs(string root)
    {
        var violations = new List<string>();
        foreach (var file in EnumerateAuthorityFiles(root))
        {
            string text = File.ReadAllText(file);
            string relative = TestRepositoryPaths.RelativePath(root, file);
            foreach (var rule in ForbiddenAuthorityPatterns)
            {
                if (rule.Pattern.IsMatch(text))
                    violations.Add($"{relative} contains {rule.Name}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Deterministic save/replay/hash authority violations:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Save/replay/hash authority avoids unstable object hashes and unordered dictionary views");
    }

    private static IEnumerable<string> EnumerateAuthorityFiles(string root)
    {
        string sourceRoot = Path.Combine(root, "src");
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(sourceRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (IsAuthorityPath(relative))
                yield return file;
        }
    }

    private static bool IsAuthorityPath(string relative)
    {
        if (relative.StartsWith("src/HumanFortress.Core/Commands/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Core/Determinism/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Runtime/Save/", StringComparison.Ordinal)
            || relative.StartsWith("src/HumanFortress.Runtime/Replay/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Simulation/Save/", StringComparison.Ordinal)
            || relative.StartsWith("src/HumanFortress.Simulation/Replay/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Jobs/Replay/", StringComparison.Ordinal))
            return true;

        if (!relative.StartsWith("src/HumanFortress.Runtime/", StringComparison.Ordinal))
            return false;

        string fileName = Path.GetFileName(relative);
        return fileName.Contains("Replay", StringComparison.Ordinal)
            || fileName.Contains("Save", StringComparison.Ordinal)
            || fileName.Contains("Checkpoint", StringComparison.Ordinal);
    }
}
