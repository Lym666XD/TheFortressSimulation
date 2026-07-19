using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    internal static string BuildAppFrameRequestHash(SimulationAppFrameRequestData request)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.committed-app-frame.request.v3");
            hash.AddBoolean(request.IncludeMapViewport);
            AddViewport(hash, request.Viewport);
            AddPoint(hash, request.CursorPosition);
            hash.AddInt32(request.CursorGlyph);
            hash.AddInt32((int)request.NavigationMode);
            AddNullablePoint(hash, request.SelectedNavigationTarget);
            AddPoint(hash, request.TileInspectionWorldPosition);
            hash.AddInt32(request.TileInspectionZ);
            hash.AddBoolean(request.ShowZoneOverlay);
            hash.AddBoolean(request.IncludeManagementDrawer);
            hash.AddBoolean(request.IncludeWorkDrawer);
            hash.AddBoolean(request.IncludeDebugMenu);
            AddNullableInt32(hash, request.StockpileDetailZoneId);
            AddNullableInt32(hash, request.ZoneDetailId);
            var placementPreviews = SimulationPlacementPreviewRequestData.CanonicalizeAll(
                request.PlacementPreviewRequests);
            hash.AddInt32(placementPreviews.Length);
            foreach (var placementPreview in placementPreviews)
            {
                AddPoint(hash, placementPreview.First);
                AddPoint(hash, placementPreview.Second);
                hash.AddInt32(placementPreview.Z);
                hash.AddInt32((int)placementPreview.Mode);
            }

            var navigationPath = request.NavigationPathRequest?.Canonicalize(
                request.Viewport.WorldBounds);
            hash.AddBoolean(navigationPath.HasValue);
            if (navigationPath.HasValue)
            {
                AddPoint(hash, navigationPath.Value.Start);
                hash.AddInt32(navigationPath.Value.StartZ);
                AddPoint(hash, navigationPath.Value.Destination);
                hash.AddInt32(navigationPath.Value.DestinationZ);
            }
        });
    }

    private static string BuildUiOverlayRequestHash(RuntimeUiOverlayFrameRequest request)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.ui-overlay.request.v1");
            AddViewport(hash, request.Viewport);
            hash.AddBoolean(request.ShowZoneOverlay);
            hash.AddBoolean(request.IncludeManagementDrawer);
            hash.AddBoolean(request.IncludeWorkDrawer);
            hash.AddBoolean(request.IncludeDebugMenu);
            AddNullableInt32(hash, request.StockpileDetailZoneId);
            AddNullableInt32(hash, request.ZoneDetailId);
        });
    }

    private static string BuildFrameRenderRequestHash(RuntimeFrameRenderRequest request)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.render.request.v1");
            hash.AddBoolean(request.IncludeMapViewport);
            AddViewport(hash, request.Viewport);
            AddPoint(hash, request.CursorPosition);
            hash.AddInt32(request.CursorGlyph);
            hash.AddInt32((int)request.NavigationMode);
            AddNullablePoint(hash, request.SelectedNavigationTarget);
            AddPoint(hash, request.TileInspectionWorldPosition);
            hash.AddInt32(request.TileInspectionZ);
        });
    }

    private static string BuildMapViewportRequestHash(RuntimeFrameRenderRequest request)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.map-viewport.request.v1");
            hash.AddBoolean(request.IncludeMapViewport);
            AddViewport(hash, request.Viewport);
            AddPoint(hash, request.CursorPosition);
            hash.AddInt32(request.CursorGlyph);
        });
    }

    private static void AddRect(ReplayHashBuilder hash, RuntimeRect value)
    {
        hash.AddInt32(value.X);
        hash.AddInt32(value.Y);
        hash.AddInt32(value.Width);
        hash.AddInt32(value.Height);
    }

    private static void AddViewport(ReplayHashBuilder hash, RuntimeViewportGeometry value)
    {
        AddRect(hash, value.Surface);
        AddPoint(hash, value.CameraWorldOrigin);
        hash.AddInt32(value.ZoomLevel);
        hash.AddInt32(value.CurrentZ);
        hash.AddInt32(value.WorldBounds.MinX);
        hash.AddInt32(value.WorldBounds.MinY);
        hash.AddInt32(value.WorldBounds.Width);
        hash.AddInt32(value.WorldBounds.Height);
        hash.AddInt32(value.WorldBounds.MinZ);
        hash.AddInt32(value.WorldBounds.MaxZExclusive);
    }

    private static void AddPoint(ReplayHashBuilder hash, RuntimePoint value)
    {
        hash.AddInt32(value.X);
        hash.AddInt32(value.Y);
    }

    private static void AddNullablePoint(ReplayHashBuilder hash, RuntimePoint? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
            AddPoint(hash, value.Value);
    }

    private static void AddNullableInt32(ReplayHashBuilder hash, int? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
            hash.AddInt32(value.Value);
    }
}
