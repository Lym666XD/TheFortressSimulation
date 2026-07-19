using System.Text.Json;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Core.Determinism;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Checkpoints;

internal sealed class RuntimeCommittedCheckpointOwner
{
    internal const string ReplaySectionId = "runtime-replay";
    internal const string ProfessionReplaySectionId = "jobs.professions";
    internal const string DiagnosticsSectionId = "runtime-diagnostics";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RuntimeCheckpointCoordinator _coordinator = new();
    private ActiveGeneration? _activeGeneration;

    internal int RetainedCount => _coordinator.Store.RetainedCount;

    internal RuntimeCheckpointGenerationLease ActivateGeneration(
        FortressRuntimeContentSnapshot? content)
    {
        var generation = _coordinator.BeginGeneration();
        Volatile.Write(
            ref _activeGeneration,
            new ActiveGeneration(generation, CreateContentIdentity(content)));
        return generation;
    }

    internal RuntimeCheckpointGenerationLease? InvalidateActiveGeneration()
    {
        while (true)
        {
            var observed = Volatile.Read(ref _activeGeneration);
            if (observed == null)
                return null;

            if (!ReferenceEquals(
                    Interlocked.CompareExchange(ref _activeGeneration, null, observed),
                    observed))
            {
                continue;
            }

            _coordinator.InvalidateGeneration(observed.Generation);
            return observed.Generation;
        }
    }

    internal bool TryPublishCommitted(
        RuntimeCheckpointGenerationLease generation,
        RuntimeSessionServices services,
        FortressRuntimeSession? session,
        SimulationRuntimeSystems? systems,
        ulong committedTick,
        RuntimeCommittedAppFramePublisher? appFrames,
        out RuntimeCheckpointIdentityData identity)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(services);
        var active = Volatile.Read(ref _activeGeneration);
        if (!IsActive(active, generation))
        {
            identity = default;
            return false;
        }

        var committedReplay = RuntimeCommittedReplayHashBuilder.Build(
            RuntimeReplayCheckpointHashBuilder.BuildData(
                services,
                session?.World,
                systems,
                committedTick),
            systems);
        var replay = committedReplay.Replay;
        var status = new SimulationStatus(
            committedTick,
            services.TickScheduler.IsPaused,
            services.TickScheduler.SpeedMultiplier);
        var diagnostics = CreateDiagnostics(
            replay,
            status,
            committedReplay.Professions.RecordCount);
        var appFrame = session == null
            ? null
            : appFrames?.TryBuildContribution(
                generation,
                session,
                committedTick,
                status);
        var sections = new List<RuntimeCheckpointSectionInput>
        {
            new(
                ReplaySectionId,
                SimulationSnapshotSchema.CurrentVersion,
                JsonSerializer.SerializeToUtf8Bytes(replay, JsonOptions)),
            new(
                DiagnosticsSectionId,
                SimulationSnapshotSchema.CurrentVersion,
                JsonSerializer.SerializeToUtf8Bytes(diagnostics, JsonOptions)),
            new(
                ProfessionReplaySectionId,
                SimulationSnapshotSchema.CurrentVersion,
                JsonSerializer.SerializeToUtf8Bytes(committedReplay.Professions, JsonOptions)),
        };
        if (appFrame != null)
            sections.AddRange(appFrame.Sections);

        if (!IsActive(Volatile.Read(ref _activeGeneration), generation)
            || !_coordinator.TryPublish(
                generation,
                committedTick,
                active!.ContentIdentity,
                sections,
                out var checkpoint)
            || checkpoint == null)
        {
            identity = default;
            return false;
        }

        identity = checkpoint.Identity;
        if (appFrame != null)
            appFrames!.CompleteContribution(generation, appFrame, checkpoint.Identity);
        return true;
    }

    internal bool TryGetLatestIdentity(out RuntimeCheckpointIdentityData identity)
    {
        if (TryGetLatest(out var checkpoint))
        {
            identity = CopyIdentity(checkpoint.Identity);
            return true;
        }

        identity = default;
        return false;
    }

    internal bool TryGetLatestReplay(out RuntimeReplayCheckpointData replay)
    {
        if (TryGetLatest(out var checkpoint)
            && checkpoint.TryCopySectionPayload(ReplaySectionId, out var payload))
        {
            replay = JsonSerializer.Deserialize<RuntimeReplayCheckpointData>(
                payload,
                JsonOptions);
            return true;
        }

        replay = default;
        return false;
    }

    internal bool TryGetLatestDiagnostics(out RuntimeCommittedDiagnosticsData diagnostics)
    {
        if (TryGetLatest(out var checkpoint)
            && checkpoint.TryCopySectionPayload(DiagnosticsSectionId, out var payload))
        {
            var facts = JsonSerializer.Deserialize<RuntimeCommittedDiagnosticsFactsData>(
                payload,
                JsonOptions);
            diagnostics = new RuntimeCommittedDiagnosticsData(
                CopyIdentity(checkpoint.Identity),
                facts);
            return true;
        }

        diagnostics = default;
        return false;
    }

    internal RuntimeCheckpointPublicationPlan ResolvePublication(
        string requestHash,
        string? previousRequestHash,
        string? requestedBaseAggregateHash)
    {
        return _coordinator.ResolvePublication(
            requestHash,
            previousRequestHash,
            requestedBaseAggregateHash);
    }

    private bool TryGetLatest(out RuntimeCommittedCheckpoint checkpoint)
    {
        var active = Volatile.Read(ref _activeGeneration);
        if (active != null
            && active.Generation.IsValid
            && _coordinator.Store.TryGetLatest(out var latest)
            && latest != null
            && latest.Identity.SessionGeneration == active.Generation.Generation)
        {
            checkpoint = latest;
            return true;
        }

        checkpoint = null!;
        return false;
    }

    private static bool IsActive(
        ActiveGeneration? active,
        RuntimeCheckpointGenerationLease generation)
    {
        return active != null
            && generation.IsValid
            && ReferenceEquals(active.Generation, generation);
    }

    private static RuntimeCommittedDiagnosticsFactsData CreateDiagnostics(
        RuntimeReplayCheckpointData replay,
        SimulationStatus status,
        int professionRecordCount)
    {
        return new RuntimeCommittedDiagnosticsFactsData(
            replay.Metadata,
            status.IsPaused,
            status.SpeedMultiplier,
            replay.WorldHash != null,
            replay.RngStreamCount,
            replay.CommandLogRecordCount,
            replay.PendingCommandLogRecordCount,
            replay.TransportRecordCount,
            replay.MiningRecordCount,
            replay.CraftRecordCount,
            professionRecordCount);
    }

    private static RuntimeCheckpointIdentityData CopyIdentity(
        RuntimeCheckpointIdentityData identity)
    {
        return identity with
        {
            Sections = Array.AsReadOnly(identity.Sections.ToArray()),
        };
    }

    private static RuntimeContentIdentityData CreateContentIdentity(
        FortressRuntimeContentSnapshot? content)
    {
        if (content == null)
        {
            return new RuntimeContentIdentityData(
                1,
                "content-unavailable",
                "content-unavailable",
                ReplayHashBuilder.Algorithm);
        }

        var contentHash = string.IsNullOrWhiteSpace(content.ContentHash)
            ? "content-hash-unavailable"
            : content.ContentHash;
        return new RuntimeContentIdentityData(
            1,
            $"{content.ContentVersion}:{contentHash}",
            contentHash,
            "sha256-truncated-64-legacy");
    }

    private sealed record ActiveGeneration(
        RuntimeCheckpointGenerationLease Generation,
        RuntimeContentIdentityData ContentIdentity);
}
