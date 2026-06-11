using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

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

    public void ApplyWheel(FortressMouseWheelResult wheel)
    {
        if (!wheel.Changed)
            return;

        ZoomLevel = wheel.ZoomLevel;
        CurrentZ = wheel.CurrentZ;
    }

    public void ApplyKeyboard(FortressKeyboardInputResult input)
    {
        CameraPosition = input.CameraPosition;
        CurrentZ = input.CurrentZ;
    }

    public void ApplyHover(FortressMouseHoverControllerResult hover)
    {
        LastMousePosition = hover.LastMousePosition;
        CursorPosition = hover.CursorPosition;
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
