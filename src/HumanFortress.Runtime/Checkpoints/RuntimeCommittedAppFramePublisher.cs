using System.Text.Json;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Geometry;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime.Checkpoints;

internal sealed class RuntimeCommittedAppFramePublisher
{
    internal const string RequestSectionId = "app-frame.request";
    internal const string RenderSectionId = "app-frame.render";
    internal const string OverlaySectionId = "app-frame.overlay";
    internal const string PlacementPreviewsSectionId = "app-frame.placement-previews";
    internal const string NavigationPathSectionId = "app-frame.navigation-path";
    internal const string StatusSectionId = "app-frame.status";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private GenerationState? _activeGeneration;

    internal void ActivateGeneration(
        RuntimeCheckpointGenerationLease generation,
        RuntimeSessionServices services,
        FortressRuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        if (!generation.IsValid)
            throw new InvalidOperationException("Cannot activate an invalid App-frame generation.");

        var world = session.World;
        var bounds = new RuntimeWorldBounds(
            0,
            0,
            world.SizeInTiles,
            world.SizeInTiles,
            0,
            world.MaxZ);
        var previous = Interlocked.Exchange(
            ref _activeGeneration,
            new GenerationState(generation, services, bounds));
        previous?.Clear();
    }

    internal void InvalidateGeneration(RuntimeCheckpointGenerationLease generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        while (true)
        {
            var observed = Volatile.Read(ref _activeGeneration);
            if (observed == null || !ReferenceEquals(observed.Generation, generation))
                return;

            if (!ReferenceEquals(
                    Interlocked.CompareExchange(ref _activeGeneration, null, observed),
                    observed))
            {
                continue;
            }

            observed.Clear();
            return;
        }
    }

    internal SimulationAppFrameData GetCommittedFrame(
        SimulationAppFrameRequestData request)
    {
        var state = Volatile.Read(ref _activeGeneration);
        if (!IsActive(state)
            || !state!.Services.TickScheduler.IsRunning)
        {
            return SimulationAppFrameData.Unavailable;
        }

        request = NormalizeRequest(request, state.WorldBounds);
        var requestHash = RuntimeFrameSnapshotPublisher.BuildAppFrameRequestHash(request);
        var published = Volatile.Read(ref state.Latest);
        Interlocked.Exchange(
            ref state.Pending,
            new PendingRequest(request, requestHash));

        if (published == null
            || !string.Equals(published.RequestHash, requestHash, StringComparison.Ordinal)
            || !RequestsEqual(published.Request, request)
            || published.CheckpointIdentity.SessionGeneration != state.Generation.Generation
            || !IsActive(state))
        {
            return SimulationAppFrameData.Unavailable;
        }

        return JsonSerializer.Deserialize<SimulationAppFrameData>(
            published.FramePayload,
            JsonOptions);
    }

    internal RuntimeCommittedAppFrameContribution? TryBuildContribution(
        RuntimeCheckpointGenerationLease generation,
        FortressRuntimeSession session,
        ulong committedTick,
        SimulationStatus status)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(session);
        var state = Volatile.Read(ref _activeGeneration);
        if (!IsActive(state, generation))
            return null;

        var pending = Interlocked.Exchange(ref state!.Pending, null);
        if (pending == null || !IsActive(state, generation))
            return null;

        var request = pending.Request;
        var frameRender = state.FrameSnapshots.PublishFrameRender(
            session,
            committedTick,
            allowCache: false,
            new RuntimeFrameRenderRequest(
                request.IncludeMapViewport,
                request.Viewport,
                request.CursorPosition,
                request.CursorGlyph,
                request.NavigationMode,
                request.SelectedNavigationTarget,
                request.TileInspectionWorldPosition,
                request.TileInspectionZ));
        var uiOverlay = state.FrameSnapshots.PublishUiOverlayFrame(
            session,
            committedTick,
            allowCache: false,
            new RuntimeUiOverlayFrameRequest(
                request.Viewport,
                request.ShowZoneOverlay,
                request.IncludeManagementDrawer,
                request.IncludeWorkDrawer,
                request.IncludeDebugMenu,
                request.StockpileDetailZoneId,
                request.ZoneDetailId));
        var placementPreviews = BuildPlacementPreviews(
            session,
            committedTick,
            request.PlacementPreviewRequests);
        var navigationPath = BuildNavigationPath(
            session,
            committedTick,
            request.NavigationPathRequest);
        var sections = new RuntimeCheckpointSectionInput[]
        {
            Serialize(RequestSectionId, request),
            Serialize(RenderSectionId, frameRender),
            Serialize(OverlaySectionId, uiOverlay),
            Serialize(PlacementPreviewsSectionId, placementPreviews),
            Serialize(NavigationPathSectionId, navigationPath),
            Serialize(StatusSectionId, status),
        };
        return new RuntimeCommittedAppFrameContribution(
            generation,
            request,
            pending.RequestHash,
            frameRender,
            uiOverlay,
            placementPreviews,
            navigationPath,
            status,
            sections);
    }

    internal void CompleteContribution(
        RuntimeCheckpointGenerationLease generation,
        RuntimeCommittedAppFrameContribution contribution,
        RuntimeCheckpointIdentityData checkpointIdentity)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(contribution);
        var state = Volatile.Read(ref _activeGeneration);
        if (!IsActive(state, generation)
            || !ReferenceEquals(contribution.Generation, generation)
            || checkpointIdentity.SessionGeneration != generation.Generation)
        {
            return;
        }

        var frame = new SimulationAppFrameData(
            true,
            checkpointIdentity,
            contribution.FrameRender,
            contribution.UiOverlay,
            contribution.PlacementPreviews,
            contribution.NavigationPath,
            contribution.Status);
        var published = new PublishedFrame(
            contribution.Request,
            contribution.RequestHash,
            checkpointIdentity,
            JsonSerializer.SerializeToUtf8Bytes(frame, JsonOptions));
        Volatile.Write(ref state!.Latest, published);
        if (!IsActive(state, generation))
            Interlocked.CompareExchange(ref state.Latest, null, published);
    }

    private static SimulationAppFrameRequestData NormalizeRequest(
        SimulationAppFrameRequestData request,
        RuntimeWorldBounds worldBounds)
    {
        return request with
        {
            Viewport = RuntimeViewportGeometryMath.Normalize(
                request.Viewport with { WorldBounds = worldBounds }),
            PlacementPreviewRequests = SimulationPlacementPreviewRequestData.CanonicalizeAll(
                request.PlacementPreviewRequests),
            NavigationPathRequest = request.NavigationPathRequest?.Canonicalize(worldBounds),
        };
    }

    private static bool RequestsEqual(
        SimulationAppFrameRequestData first,
        SimulationAppFrameRequestData second)
    {
        var empty = Array.Empty<SimulationPlacementPreviewRequestData>();
        return (first with { PlacementPreviewRequests = empty })
                == (second with { PlacementPreviewRequests = empty })
            && (first.PlacementPreviewRequests ?? empty)
                .SequenceEqual(second.PlacementPreviewRequests ?? empty);
    }

    private static SimulationPlacementPreviewFrameData BuildPlacementPreviews(
        FortressRuntimeSession session,
        ulong committedTick,
        IReadOnlyList<SimulationPlacementPreviewRequestData> requests)
    {
        var rows = requests
            .Select(request => new SimulationPlacementPreviewRowData(
                request,
                FortressRuntimeSessionSnapshotFacade.BuildPlacementPreviewSnapshot(
                    session,
                    request.First.ToSadRoguePoint(),
                    request.Second.ToSadRoguePoint(),
                    request.Z,
                    request.Mode)))
            .ToArray();
        return new SimulationPlacementPreviewFrameData(
            SimulationSnapshotMetadata.Current(committedTick),
            rows);
    }

    private static SimulationNavigationPathFrameData BuildNavigationPath(
        FortressRuntimeSession session,
        ulong committedTick,
        SimulationNavigationPathRequestData? request)
    {
        var metadata = SimulationSnapshotMetadata.Current(committedTick);
        if (!request.HasValue)
        {
            return new SimulationNavigationPathFrameData(
                metadata,
                false,
                null,
                SimulationNavigationPathData.Unavailable);
        }

        var value = request.Value;
        return new SimulationNavigationPathFrameData(
            metadata,
            true,
            value,
            FortressRuntimeSessionSnapshotFacade.FindNavigationDebugPath(
                session,
                value.Start.ToSadRoguePoint(),
                value.StartZ,
                value.Destination.ToSadRoguePoint(),
                value.DestinationZ));
    }

    private static RuntimeCheckpointSectionInput Serialize<T>(string sectionId, T value)
    {
        return new RuntimeCheckpointSectionInput(
            sectionId,
            SimulationSnapshotSchema.CurrentVersion,
            JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
    }

    private bool IsActive(GenerationState? state)
    {
        return state != null
            && state.Generation.IsValid
            && ReferenceEquals(Volatile.Read(ref _activeGeneration), state);
    }

    private bool IsActive(
        GenerationState? state,
        RuntimeCheckpointGenerationLease generation)
    {
        return IsActive(state)
            && ReferenceEquals(state!.Generation, generation);
    }

    private sealed class GenerationState
    {
        internal GenerationState(
            RuntimeCheckpointGenerationLease generation,
            RuntimeSessionServices services,
            RuntimeWorldBounds worldBounds)
        {
            Generation = generation;
            Services = services;
            WorldBounds = worldBounds;
        }

        internal RuntimeCheckpointGenerationLease Generation { get; }
        internal RuntimeSessionServices Services { get; }
        internal RuntimeWorldBounds WorldBounds { get; }
        internal RuntimeFrameSnapshotPublisher FrameSnapshots { get; } = new();
        internal PendingRequest? Pending;
        internal PublishedFrame? Latest;

        internal void Clear()
        {
            Interlocked.Exchange(ref Pending, null);
            Interlocked.Exchange(ref Latest, null);
        }
    }

    private sealed record PendingRequest(
        SimulationAppFrameRequestData Request,
        string RequestHash);

    private sealed record PublishedFrame(
        SimulationAppFrameRequestData Request,
        string RequestHash,
        RuntimeCheckpointIdentityData CheckpointIdentity,
        byte[] FramePayload);
}

internal sealed record RuntimeCommittedAppFrameContribution(
    RuntimeCheckpointGenerationLease Generation,
    SimulationAppFrameRequestData Request,
    string RequestHash,
    SimulationFrameRenderData FrameRender,
    SimulationUiOverlayFrameData UiOverlay,
    SimulationPlacementPreviewFrameData PlacementPreviews,
    SimulationNavigationPathFrameData NavigationPath,
    SimulationStatus Status,
    IReadOnlyList<RuntimeCheckpointSectionInput> Sections);
