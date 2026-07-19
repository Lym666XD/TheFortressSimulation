using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal readonly record struct FortressViewportSnapshot(
    Point CameraPosition,
    Point CursorPosition,
    int CurrentZ,
    int ZoomLevel,
    Point? LastMousePosition,
    RuntimeWorldBounds WorldBounds)
{
    internal RuntimeViewportGeometry CreateGeometry(RuntimeRect surface)
    {
        return RuntimeViewportGeometryMath.Normalize(new RuntimeViewportGeometry(
            surface,
            new RuntimePoint(CameraPosition.X, CameraPosition.Y),
            ZoomLevel,
            CurrentZ,
            WorldBounds));
    }
}

internal sealed class FortressViewportState
{
    public Point CameraPosition { get; private set; }
    public Point CursorPosition { get; private set; }
    public int CurrentZ { get; private set; }
    public int ZoomLevel { get; private set; } = 1;
    public Point? LastMousePosition { get; private set; }
    public RuntimeWorldBounds WorldBounds { get; private set; } = RuntimeWorldBounds.Empty;

    public FortressViewportSnapshot Capture()
    {
        return new FortressViewportSnapshot(
            CameraPosition,
            CursorPosition,
            CurrentZ,
            ZoomLevel,
            LastMousePosition,
            WorldBounds);
    }

    public void Initialize(RuntimeWorldBounds worldBounds, RuntimeRect surface)
    {
        WorldBounds = worldBounds;
        var initial = FortressViewportMath.CreateInitial(worldBounds, surface, ZoomLevel);
        CameraPosition = initial.CameraPosition;
        CursorPosition = initial.CursorPosition;
        CurrentZ = worldBounds.IsEmpty
            ? 0
            : Math.Clamp(CurrentZ, worldBounds.MinZ, worldBounds.MaxZExclusive - 1);
        LastMousePosition = null;
    }

    public void ApplyWheel(int zoomLevel, int currentZ)
    {
        ZoomLevel = zoomLevel;
        CurrentZ = ClampZ(currentZ);
        LastMousePosition = null;
    }

    public void ApplyKeyboard(Point cameraPosition, int currentZ)
    {
        CameraPosition = cameraPosition;
        CurrentZ = ClampZ(currentZ);
        LastMousePosition = null;
    }

    public void ApplyHover(Point? lastMousePosition, Point cursorPosition)
    {
        LastMousePosition = lastMousePosition;
        CursorPosition = cursorPosition;
    }

    public void ClampCamera(RuntimeRect surface)
    {
        CameraPosition = FortressViewportMath.ClampCamera(
            CameraPosition,
            WorldBounds,
            surface,
            ZoomLevel,
            CurrentZ);
    }

    private int ClampZ(int value)
    {
        return WorldBounds.IsEmpty
            ? 0
            : Math.Clamp(value, WorldBounds.MinZ, WorldBounds.MaxZExclusive - 1);
    }
}
