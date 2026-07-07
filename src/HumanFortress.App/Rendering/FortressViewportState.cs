using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal readonly record struct FortressViewportSnapshot(
    Point CameraPosition,
    Point CursorPosition,
    int CurrentZ,
    int ZoomLevel,
    Point? LastMousePosition);

internal sealed class FortressViewportState
{
    public Point CameraPosition { get; private set; }
    public Point CursorPosition { get; private set; }
    public int CurrentZ { get; private set; } = 25;
    public int ZoomLevel { get; private set; } = 1;
    public Point? LastMousePosition { get; private set; }

    public FortressViewportSnapshot Capture()
    {
        return new FortressViewportSnapshot(CameraPosition, CursorPosition, CurrentZ, ZoomLevel, LastMousePosition);
    }

    public void Initialize(int fortressSize)
    {
        var initial = FortressViewportMath.CreateInitial(fortressSize);
        CameraPosition = initial.CameraPosition;
        CursorPosition = initial.CursorPosition;
    }

    public void ApplyWheel(int zoomLevel, int currentZ)
    {
        ZoomLevel = zoomLevel;
        CurrentZ = currentZ;
    }

    public void ApplyKeyboard(Point cameraPosition, int currentZ)
    {
        CameraPosition = cameraPosition;
        CurrentZ = currentZ;
    }

    public void ApplyHover(Point? lastMousePosition, Point cursorPosition)
    {
        LastMousePosition = lastMousePosition;
        CursorPosition = cursorPosition;
    }

    public void ClampCamera(int fortressSize, int mapSurfaceWidth, int mapSurfaceHeight)
    {
        CameraPosition = FortressViewportMath.ClampCamera(
            CameraPosition,
            fortressSize,
            mapSurfaceWidth,
            mapSurfaceHeight,
            ZoomLevel);
    }
}
