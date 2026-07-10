using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    private static string BuildUiOverlayRequestHash(RuntimeUiOverlayFrameRequest request)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.ui-overlay.request.v1");
            hash.AddInt32(request.CurrentZ);
            AddRect(hash, request.Viewport);
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
            hash.AddInt32(request.FortressSize);
            AddPoint(hash, request.CameraPosition);
            AddPoint(hash, request.CursorPosition);
            hash.AddInt32(request.CurrentZ);
            hash.AddInt32(request.ZoomLevel);
            hash.AddInt32(request.ViewWidth);
            hash.AddInt32(request.ViewHeight);
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
            hash.AddInt32(request.FortressSize);
            AddPoint(hash, request.CameraPosition);
            AddPoint(hash, request.CursorPosition);
            hash.AddInt32(request.CurrentZ);
            hash.AddInt32(request.ZoomLevel);
            hash.AddInt32(request.ViewWidth);
            hash.AddInt32(request.ViewHeight);
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
