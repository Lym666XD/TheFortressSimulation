namespace HumanFortress.Contracts.Runtime;

public readonly record struct RuntimeWorldBounds(
    int MinX,
    int MinY,
    int Width,
    int Height,
    int MinZ,
    int MaxZExclusive)
{
    public static RuntimeWorldBounds Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public int MaxXExclusive => checked(MinX + Math.Max(0, Width));

    public int MaxYExclusive => checked(MinY + Math.Max(0, Height));

    public int Depth => Math.Max(0, MaxZExclusive - MinZ);

    public bool IsEmpty => Width <= 0 || Height <= 0 || Depth <= 0;

    public bool Contains(int x, int y, int z)
    {
        return ContainsXY(x, y) && z >= MinZ && z < MaxZExclusive;
    }

    public bool ContainsXY(int x, int y)
    {
        return x >= MinX && x < MaxXExclusive && y >= MinY && y < MaxYExclusive;
    }
}

public readonly record struct RuntimeViewportGeometry(
    RuntimeRect Surface,
    RuntimePoint CameraWorldOrigin,
    int ZoomLevel,
    int CurrentZ,
    RuntimeWorldBounds WorldBounds)
{
    public static RuntimeViewportGeometry Empty { get; } = new(
        new RuntimeRect(0, 0, 0, 0),
        new RuntimePoint(0, 0),
        1,
        0,
        RuntimeWorldBounds.Empty);
}

public static class RuntimeViewportGeometryMath
{
    public static RuntimeViewportGeometry Normalize(RuntimeViewportGeometry geometry)
    {
        var surface = geometry.Surface with
        {
            Width = Math.Max(0, geometry.Surface.Width),
            Height = Math.Max(0, geometry.Surface.Height),
        };
        var zoomLevel = Math.Max(1, geometry.ZoomLevel);
        var worldBounds = geometry.WorldBounds;
        if (worldBounds.IsEmpty)
        {
            return geometry with
            {
                Surface = surface,
                CameraWorldOrigin = new RuntimePoint(worldBounds.MinX, worldBounds.MinY),
                ZoomLevel = zoomLevel,
                CurrentZ = worldBounds.MinZ,
            };
        }

        int visibleWorldWidth = DivideRoundUp(surface.Width, zoomLevel);
        int visibleWorldHeight = DivideRoundUp(surface.Height, zoomLevel);
        int maxCameraX = Math.Max(worldBounds.MinX, worldBounds.MaxXExclusive - visibleWorldWidth);
        int maxCameraY = Math.Max(worldBounds.MinY, worldBounds.MaxYExclusive - visibleWorldHeight);

        return geometry with
        {
            Surface = surface,
            CameraWorldOrigin = new RuntimePoint(
                Math.Clamp(geometry.CameraWorldOrigin.X, worldBounds.MinX, maxCameraX),
                Math.Clamp(geometry.CameraWorldOrigin.Y, worldBounds.MinY, maxCameraY)),
            ZoomLevel = zoomLevel,
            CurrentZ = Math.Clamp(geometry.CurrentZ, worldBounds.MinZ, worldBounds.MaxZExclusive - 1),
        };
    }

    public static int VisibleWorldWidth(RuntimeViewportGeometry geometry)
    {
        var normalized = Normalize(geometry);
        return DivideRoundUp(normalized.Surface.Width, normalized.ZoomLevel);
    }

    public static int VisibleWorldHeight(RuntimeViewportGeometry geometry)
    {
        var normalized = Normalize(geometry);
        return DivideRoundUp(normalized.Surface.Height, normalized.ZoomLevel);
    }

    public static RuntimeRect VisibleWorldRect(RuntimeViewportGeometry geometry)
    {
        var normalized = Normalize(geometry);
        int width = Math.Min(
            VisibleWorldWidth(normalized),
            Math.Max(0, normalized.WorldBounds.MaxXExclusive - normalized.CameraWorldOrigin.X));
        int height = Math.Min(
            VisibleWorldHeight(normalized),
            Math.Max(0, normalized.WorldBounds.MaxYExclusive - normalized.CameraWorldOrigin.Y));
        return new RuntimeRect(
            normalized.CameraWorldOrigin.X,
            normalized.CameraWorldOrigin.Y,
            width,
            height);
    }

    public static bool TrySurfaceToWorld(
        RuntimeViewportGeometry geometry,
        RuntimePoint surfacePoint,
        out RuntimePoint worldPoint)
    {
        var normalized = Normalize(geometry);
        int localX = surfacePoint.X - normalized.Surface.X;
        int localY = surfacePoint.Y - normalized.Surface.Y;
        return TryLocalToWorld(normalized, new RuntimePoint(localX, localY), out worldPoint);
    }

    public static bool TryLocalToWorld(
        RuntimeViewportGeometry geometry,
        RuntimePoint localPoint,
        out RuntimePoint worldPoint)
    {
        var normalized = Normalize(geometry);
        if (localPoint.X < 0
            || localPoint.Y < 0
            || localPoint.X >= normalized.Surface.Width
            || localPoint.Y >= normalized.Surface.Height)
        {
            worldPoint = default;
            return false;
        }

        worldPoint = new RuntimePoint(
            normalized.CameraWorldOrigin.X + (localPoint.X / normalized.ZoomLevel),
            normalized.CameraWorldOrigin.Y + (localPoint.Y / normalized.ZoomLevel));
        return normalized.WorldBounds.ContainsXY(worldPoint.X, worldPoint.Y);
    }

    public static bool TryWorldToLocal(
        RuntimeViewportGeometry geometry,
        RuntimePoint worldPoint,
        out RuntimePoint localPoint)
    {
        var normalized = Normalize(geometry);
        if (!normalized.WorldBounds.ContainsXY(worldPoint.X, worldPoint.Y))
        {
            localPoint = default;
            return false;
        }

        long localX = (long)(worldPoint.X - normalized.CameraWorldOrigin.X) * normalized.ZoomLevel;
        long localY = (long)(worldPoint.Y - normalized.CameraWorldOrigin.Y) * normalized.ZoomLevel;
        if (localX < 0
            || localY < 0
            || localX >= normalized.Surface.Width
            || localY >= normalized.Surface.Height)
        {
            localPoint = default;
            return false;
        }

        localPoint = new RuntimePoint((int)localX, (int)localY);
        return true;
    }

    public static bool TryWorldToSurface(
        RuntimeViewportGeometry geometry,
        RuntimePoint worldPoint,
        out RuntimePoint surfacePoint)
    {
        var normalized = Normalize(geometry);
        if (!TryWorldToLocal(normalized, worldPoint, out var localPoint))
        {
            surfacePoint = default;
            return false;
        }

        surfacePoint = new RuntimePoint(
            checked(normalized.Surface.X + localPoint.X),
            checked(normalized.Surface.Y + localPoint.Y));
        return true;
    }

    public static bool TryGetWorldCellLocalRect(
        RuntimeViewportGeometry geometry,
        RuntimePoint worldPoint,
        out RuntimeRect localRect)
    {
        var normalized = Normalize(geometry);
        if (!TryWorldToLocal(normalized, worldPoint, out var localPoint))
        {
            localRect = default;
            return false;
        }

        localRect = new RuntimeRect(
            localPoint.X,
            localPoint.Y,
            Math.Min(normalized.ZoomLevel, normalized.Surface.Width - localPoint.X),
            Math.Min(normalized.ZoomLevel, normalized.Surface.Height - localPoint.Y));
        return localRect.Width > 0 && localRect.Height > 0;
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        if (value <= 0)
            return 0;

        return checked(((value - 1) / Math.Max(1, divisor)) + 1);
    }
}
