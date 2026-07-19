using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Core.Determinism;

namespace HumanFortress.Runtime.Checkpoints;

internal static class RuntimeCheckpointIdentityHashBuilder
{
    internal static string BuildPayloadHash(ReadOnlySpan<byte> payload)
    {
        var ownedPayload = payload.ToArray();
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.committed-checkpoint.section-payload.v1");
            hash.AddBytes(ownedPayload);
        });
    }

    internal static string BuildAggregateHash(
        ulong sessionGeneration,
        ulong runtimeTick,
        RuntimeContentIdentityData content,
        IEnumerable<RuntimeCheckpointSectionIdentityData> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var orderedSections = sections
            .OrderBy(static section => section.SectionId, StringComparer.Ordinal)
            .ToArray();

        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.committed-checkpoint.identity.v1");
            hash.AddUInt64(sessionGeneration);
            hash.AddUInt64(runtimeTick);
            hash.AddInt32(content.SchemaVersion);
            hash.AddString(content.Signature);
            hash.AddString(content.MechanicalHash);
            hash.AddString(content.HashAlgorithm);
            hash.AddInt32(orderedSections.Length);
            foreach (var section in orderedSections)
            {
                hash.AddString(section.SectionId);
                hash.AddInt32(section.SchemaVersion);
                hash.AddString(section.PayloadHash);
                hash.AddString(section.HashAlgorithm);
                hash.AddInt32(section.PayloadLength);
            }
        });
    }
}
