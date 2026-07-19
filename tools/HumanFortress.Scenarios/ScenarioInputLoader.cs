using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Runtime.Commands;

namespace HumanFortress.Scenarios;

internal sealed record LoadedScenarioInputs(
    ScenarioProfileDocument Profile,
    string ProfilePath,
    string ProfileHash,
    ScenarioCommandJournalDocument Journal,
    IReadOnlyList<CommandReplayRecord> CommandRecords);

internal static class ScenarioInputLoader
{
    internal static LoadedScenarioInputs Load(string profilePath)
    {
        profilePath = Path.GetFullPath(profilePath);
        var profile = Deserialize<ScenarioProfileDocument>(profilePath, "scenario profile");
        ValidateProfile(profile, profilePath);

        var journalPath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(profilePath)!,
                profile.CommandJournal));
        var journal = Deserialize<ScenarioCommandJournalDocument>(journalPath, "command journal");
        var records = DecodeAndValidateJournal(journal, journalPath);
        var profileBytes = JsonSerializer.SerializeToUtf8Bytes(profile, ScenarioJson.Strict);
        var profileHash = HashDomain("humanfortress.scenario.profile.v1", profileBytes);

        return new LoadedScenarioInputs(
            profile,
            profilePath,
            profileHash,
            journal,
            records);
    }

    internal static string BuildScenarioHash(
        LoadedScenarioInputs inputs,
        int totalTicks)
    {
        var canonical = Encoding.UTF8.GetBytes(
            string.Join(
                "\n",
                inputs.ProfileHash,
                inputs.Journal.JournalHash,
                totalTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return HashDomain("humanfortress.scenario.identity.v1", canonical);
    }

    private static T Deserialize<T>(string path, string description)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"The {description} does not exist: {path}", path);

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), ScenarioJson.Strict)
                ?? throw new InvalidDataException($"The {description} is JSON null: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The {description} is invalid at {exception.Path ?? "<root>"}: {exception.Message}",
                exception);
        }
    }

    private static void ValidateProfile(ScenarioProfileDocument profile, string path)
    {
        if (profile.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported scenario schema version '{profile.SchemaVersion}' in {path}.");
        if (string.IsNullOrWhiteSpace(profile.Id))
            throw new InvalidDataException($"Scenario id is blank in {path}.");
        if (profile.World.Mode is not ("flat" or "generated-fortress"))
            throw new InvalidDataException($"Unknown world mode '{profile.World.Mode}' in {path}.");
        if (profile.World.SizeInChunks is < 2 or > 16)
            throw new InvalidDataException("Scenario world size must be between 2 and 16 chunks.");
        if (profile.World.MaxZ <= 0
            || profile.World.StandableZ < 0
            || profile.World.StandableZ >= profile.World.MaxZ)
        {
            throw new InvalidDataException("Scenario world Z bounds are invalid.");
        }
        if (profile.World.Mode == "generated-fortress" && profile.World.MaxZ != 50)
            throw new InvalidDataException("Generated fortress scenarios require MaxZ 50.");
        if (profile.Workload.InitialCreatures < 0
            || profile.Workload.ItemInstances < 0
            || profile.Workload.TransportRequests < 0
            || profile.Workload.TransportRequests > profile.Workload.ItemInstances)
        {
            throw new InvalidDataException("Scenario workload counts are invalid.");
        }
        if (string.IsNullOrWhiteSpace(profile.Workload.ItemDefinitionId))
            throw new InvalidDataException("Scenario item definition id is blank.");
        if (profile.WarmupTicks < 0 || profile.MeasuredTicks <= 0)
            throw new InvalidDataException("Scenario tick counts are invalid.");
        if (profile.CheckpointInterval <= 0)
            throw new InvalidDataException("Scenario checkpoint interval must be positive.");
        if (string.IsNullOrWhiteSpace(profile.CommandJournal))
            throw new InvalidDataException("Scenario command journal path is blank.");
    }

    private static IReadOnlyList<CommandReplayRecord> DecodeAndValidateJournal(
        ScenarioCommandJournalDocument journal,
        string path)
    {
        if (journal.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported command journal version '{journal.SchemaVersion}' in {path}.");
        if (string.IsNullOrWhiteSpace(journal.Id))
            throw new InvalidDataException($"Command journal id is blank in {path}.");
        if (!string.Equals(journal.HashAlgorithm, ReplayHashBuilder.Algorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Command journal algorithm '{journal.HashAlgorithm}' does not match '{ReplayHashBuilder.Algorithm}'.");
        }

        var records = new List<CommandReplayRecord>(journal.Records.Count);
        long previousSequence = 0;
        foreach (var row in journal.Records)
        {
            if (!Guid.TryParseExact(row.CommandId, "D", out var commandId))
                throw new InvalidDataException($"Command journal contains invalid command id '{row.CommandId}'.");
            if (row.CommandIdentitySequence is not { } sequence || sequence <= previousSequence)
            {
                throw new InvalidDataException(
                    "Command journal identity sequences must be present, positive, and strictly increasing.");
            }

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(row.PayloadBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(
                    $"Command '{row.CommandId}' has invalid base64 payload.",
                    exception);
            }

            records.Add(new CommandReplayRecord(
                row.Tick,
                commandId,
                row.CommandType,
                payload,
                sequence));
            previousSequence = sequence;
        }

        var actualHash = CommandReplayJournalHashBuilder.Build(records);
        if (!string.Equals(actualHash, journal.JournalHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Command journal hash mismatch in {path}: expected '{journal.JournalHash}', actual '{actualHash}'.");
        }

        var factory = new RuntimeCommandReplayFactory();
        for (var index = 0; index < records.Count; index++)
        {
            if (!factory.TryCreateCommand(records[index], out _, out var error))
            {
                throw new InvalidDataException(
                    $"Command journal record {index} failed strict decode: {error ?? "unknown error"}");
            }
        }

        return records.AsReadOnly();
    }

    private static string HashDomain(string domain, ReadOnlySpan<byte> bytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain));
        hash.AppendData(new byte[] { 0 });
        hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
