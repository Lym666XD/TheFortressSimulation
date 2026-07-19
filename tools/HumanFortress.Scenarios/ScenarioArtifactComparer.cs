using System.Text.Json;

namespace HumanFortress.Scenarios;

internal static class ScenarioArtifactComparer
{
    internal static bool Compare(
        string leftPath,
        string rightPath,
        out IReadOnlyList<string> differences)
    {
        var left = Load(leftPath);
        var right = Load(rightPath);
        var found = new List<string>();

        CompareValue("schemaVersion", left.SchemaVersion, right.SchemaVersion, found);
        CompareValue("profileId", left.Identity.ProfileId, right.Identity.ProfileId, found);
        CompareValue("profileHash", left.Identity.ProfileHash, right.Identity.ProfileHash, found);
        CompareValue("scenarioHash", left.Identity.ScenarioHash, right.Identity.ScenarioHash, found);
        CompareValue("journalId", left.Identity.JournalId, right.Identity.JournalId, found);
        CompareValue("journalHash", left.Identity.JournalHash, right.Identity.JournalHash, found);
        CompareValue("hashAlgorithm", left.Identity.HashAlgorithm, right.Identity.HashAlgorithm, found);

        var leftDeterministic = JsonSerializer.Serialize(left.Deterministic, ScenarioJson.Strict);
        var rightDeterministic = JsonSerializer.Serialize(right.Deterministic, ScenarioJson.Strict);
        if (!string.Equals(leftDeterministic, rightDeterministic, StringComparison.Ordinal))
        {
            CompareDeterministic(left.Deterministic, right.Deterministic, found);
            if (found.Count == 0)
                found.Add("deterministic evidence differs");
        }

        differences = found.AsReadOnly();
        return found.Count == 0;
    }

    private static ScenarioRunArtifact Load(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scenario artifact does not exist: {path}", path);

        try
        {
            var artifact = JsonSerializer.Deserialize<ScenarioRunArtifact>(
                File.ReadAllBytes(path),
                ScenarioJson.Strict)
                ?? throw new InvalidDataException($"Scenario artifact is JSON null: {path}");
            if (artifact.SchemaVersion != 1)
                throw new InvalidDataException($"Unsupported scenario artifact version '{artifact.SchemaVersion}'.");
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Scenario artifact is invalid at {exception.Path ?? "<root>"}: {exception.Message}",
                exception);
        }
    }

    private static void CompareDeterministic(
        ScenarioDeterministicEvidence left,
        ScenarioDeterministicEvidence right,
        ICollection<string> differences)
    {
        CompareValue("runtimeSeed", left.RuntimeSeed, right.RuntimeSeed, differences);
        CompareValue("generationSeed", left.GenerationSeed, right.GenerationSeed, differences);
        CompareValue("totalTicks", left.TotalTicks, right.TotalTicks, differences);
        CompareValue("contentSignature", left.ContentSignature, right.ContentSignature, differences);
        CompareValue("contentMechanicalHash", left.ContentMechanicalHash, right.ContentMechanicalHash, differences);
        CompareValue("initialAuthority", left.InitialAuthority, right.InitialAuthority, differences);
        CompareValue("finalAuthority", left.FinalAuthority, right.FinalAuthority, differences);
        CompareValue("counters", left.Counters, right.Counters, differences);
        CompareValue(
            "checkpointCount",
            left.ReplayCheckpoints.Count,
            right.ReplayCheckpoints.Count,
            differences);

        var count = Math.Min(left.ReplayCheckpoints.Count, right.ReplayCheckpoints.Count);
        for (var index = 0; index < count; index++)
        {
            if (Equals(left.ReplayCheckpoints[index], right.ReplayCheckpoints[index]))
                continue;

            differences.Add(
                $"checkpoint[{index}] differs at ticks "
                + $"{left.ReplayCheckpoints[index].Tick}/{right.ReplayCheckpoints[index].Tick}: "
                + $"aggregate={left.ReplayCheckpoints[index].AggregateHash}/"
                + right.ReplayCheckpoints[index].AggregateHash);
            break;
        }
    }

    private static void CompareValue<T>(
        string name,
        T left,
        T right,
        ICollection<string> differences)
    {
        if (!EqualityComparer<T>.Default.Equals(left, right))
            differences.Add($"{name} differs: '{left}' != '{right}'");
    }
}
