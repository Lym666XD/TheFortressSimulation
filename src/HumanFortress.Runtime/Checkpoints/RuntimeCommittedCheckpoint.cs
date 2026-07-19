using System.Collections.ObjectModel;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Core.Determinism;

namespace HumanFortress.Runtime.Checkpoints;

internal readonly record struct RuntimeCheckpointSectionInput(
    string SectionId,
    int SchemaVersion,
    ReadOnlyMemory<byte> Payload);

internal sealed class RuntimeCommittedCheckpointSection
{
    private readonly byte[] _payload;

    internal RuntimeCommittedCheckpointSection(RuntimeCheckpointSectionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.SectionId))
            throw new ArgumentException("Checkpoint section id must not be blank.", nameof(input));
        if (input.SchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Checkpoint section schema version must be positive.");

        _payload = input.Payload.ToArray();
        Identity = new RuntimeCheckpointSectionIdentityData(
            input.SectionId,
            input.SchemaVersion,
            RuntimeCheckpointIdentityHashBuilder.BuildPayloadHash(_payload),
            ReplayHashBuilder.Algorithm,
            _payload.Length);
    }

    internal RuntimeCheckpointSectionIdentityData Identity { get; }

    internal byte[] CopyPayload()
    {
        return (byte[])_payload.Clone();
    }
}

internal sealed class RuntimeCommittedCheckpoint
{
    private readonly ReadOnlyCollection<RuntimeCommittedCheckpointSection> _sections;

    internal RuntimeCommittedCheckpoint(
        RuntimeCheckpointGenerationLease generationLease,
        ulong runtimeTick,
        RuntimeContentIdentityData content,
        IEnumerable<RuntimeCheckpointSectionInput> sections)
    {
        ArgumentNullException.ThrowIfNull(generationLease);
        ArgumentNullException.ThrowIfNull(sections);
        if (!generationLease.IsValid)
            throw new InvalidOperationException("Cannot construct a checkpoint for an invalid generation.");
        ValidateContent(content);

        var ownedSections = sections
            .Select(static input => new RuntimeCommittedCheckpointSection(input))
            .OrderBy(static section => section.Identity.SectionId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < ownedSections.Length; index++)
        {
            if (string.Equals(
                    ownedSections[index - 1].Identity.SectionId,
                    ownedSections[index].Identity.SectionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Checkpoint contains duplicate section '{ownedSections[index].Identity.SectionId}'.",
                    nameof(sections));
            }
        }

        var identities = ownedSections
            .Select(static section => section.Identity)
            .ToArray();
        var aggregateHash = RuntimeCheckpointIdentityHashBuilder.BuildAggregateHash(
            generationLease.Generation,
            runtimeTick,
            content,
            identities);

        _sections = Array.AsReadOnly(ownedSections);
        Identity = new RuntimeCheckpointIdentityData(
            generationLease.Generation,
            runtimeTick,
            content,
            aggregateHash,
            ReplayHashBuilder.Algorithm,
            Array.AsReadOnly(identities));
    }

    internal RuntimeCheckpointIdentityData Identity { get; }

    internal IReadOnlyList<RuntimeCommittedCheckpointSection> Sections => _sections;

    internal bool TryCopySectionPayload(string sectionId, out byte[] payload)
    {
        foreach (var section in _sections)
        {
            if (!string.Equals(section.Identity.SectionId, sectionId, StringComparison.Ordinal))
                continue;

            payload = section.CopyPayload();
            return true;
        }

        payload = Array.Empty<byte>();
        return false;
    }

    private static void ValidateContent(RuntimeContentIdentityData content)
    {
        if (content.SchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(content), "Content schema version must be positive.");
        if (string.IsNullOrWhiteSpace(content.Signature))
            throw new ArgumentException("Content signature must not be blank.", nameof(content));
        if (string.IsNullOrWhiteSpace(content.MechanicalHash))
            throw new ArgumentException("Content mechanical hash must not be blank.", nameof(content));
        if (string.IsNullOrWhiteSpace(content.HashAlgorithm))
            throw new ArgumentException("Content hash algorithm must not be blank.", nameof(content));
    }
}
