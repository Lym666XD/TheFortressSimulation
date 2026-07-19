using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Geometry;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    internal SimulationUiOverlayFrameData PublishUiOverlayFrame(
        FortressRuntimeSession? session,
        ulong runtimeTick,
        bool allowCache,
        RuntimeUiOverlayFrameRequest request)
    {
        if (allowCache && TryGetCachedUiOverlayFrame(runtimeTick, request, out var cachedUiOverlayFrame))
        {
            return cachedUiOverlayFrame;
        }

        var requestHash = BuildUiOverlayRequestHash(request);
        var metadata = SimulationSnapshotMetadata.Current(runtimeTick);
        var publication = SimulationSnapshotPublicationData.Current(
            SimulationSnapshotPublicationSurface.UiOverlayFrame,
            requestHash,
            ReplayHashBuilder.Algorithm);
        var dataWithoutPresenterFrame = FortressRuntimeSessionSnapshotFacade.BuildUiOverlayFrameSnapshot(
            session,
            request.Viewport,
            request.ShowZoneOverlay,
            request.IncludeManagementDrawer,
            request.IncludeWorkDrawer,
            request.IncludeDebugMenu,
            request.StockpileDetailZoneId,
            request.ZoneDetailId,
            metadata) with
        {
            Publication = publication
        };
        var overlayDelta = PublishUiOverlayDelta(
            dataWithoutPresenterFrame,
            requestHash);
        var dataWithOverlayDelta = dataWithoutPresenterFrame with
        {
            Delta = overlayDelta
        };
        var presenterFrame = PublishPresenterFrame(
            SimulationSnapshotPublicationSurface.UiOverlayFrame,
            requestHash,
            dataWithoutPresenterFrame,
            isUiOverlayFrame: true);
        var data = dataWithOverlayDelta with
        {
            PresenterFrame = presenterFrame
        };

        if (allowCache)
        {
            CacheUiOverlayFrame(runtimeTick, request, data);
        }

        return data;
    }

    internal SimulationFrameRenderData PublishFrameRender(
        FortressRuntimeSession? session,
        ulong runtimeTick,
        bool allowCache,
        RuntimeFrameRenderRequest request)
    {
        if (allowCache && TryGetCachedFrameRender(runtimeTick, request, out var cachedFrameRender))
        {
            return cachedFrameRender;
        }

        var requestHash = BuildFrameRenderRequestHash(request);
        var metadata = SimulationSnapshotMetadata.Current(runtimeTick);
        var publication = SimulationSnapshotPublicationData.Current(
            SimulationSnapshotPublicationSurface.FrameRender,
            requestHash,
            ReplayHashBuilder.Algorithm);
        var dataWithoutPresenterFrame = FortressRuntimeSessionSnapshotFacade.BuildFrameRenderSnapshot(
            session,
            request.IncludeMapViewport,
            request.Viewport,
            request.CursorPosition.ToSadRoguePoint(),
            request.CursorGlyph,
            request.NavigationMode,
            request.SelectedNavigationTarget.ToSadRoguePoint(),
            request.TileInspectionWorldPosition.ToSadRoguePoint(),
            request.TileInspectionZ,
            metadata) with
        {
            Publication = publication
        };
        var mapViewportDelta = PublishMapViewportDelta(
            dataWithoutPresenterFrame.MapViewport,
            request);
        var dataWithMapViewportDelta = dataWithoutPresenterFrame with
        {
            MapViewport = dataWithoutPresenterFrame.MapViewport with
            {
                Delta = mapViewportDelta
            }
        };
        var presenterFrame = PublishPresenterFrame(
            SimulationSnapshotPublicationSurface.FrameRender,
            requestHash,
            dataWithoutPresenterFrame,
            isUiOverlayFrame: false);
        var data = dataWithMapViewportDelta with
        {
            PresenterFrame = presenterFrame
        };

        if (allowCache)
        {
            CacheFrameRender(runtimeTick, request, data);
        }

        return data;
    }
}

internal readonly record struct RuntimeUiOverlayFrameRequest(
    RuntimeViewportGeometry Viewport,
    bool ShowZoneOverlay,
    bool IncludeManagementDrawer,
    bool IncludeWorkDrawer,
    bool IncludeDebugMenu,
    int? StockpileDetailZoneId,
    int? ZoneDetailId);

internal readonly record struct RuntimeFrameRenderRequest(
    bool IncludeMapViewport,
    RuntimeViewportGeometry Viewport,
    RuntimePoint CursorPosition,
    int CursorGlyph,
    SimulationNavigationOverlayMode NavigationMode,
    RuntimePoint? SelectedNavigationTarget,
    RuntimePoint TileInspectionWorldPosition,
    int TileInspectionZ);
