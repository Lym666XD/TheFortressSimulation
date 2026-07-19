using HumanFortress.App.Input;
using HumanFortress.App.UI.Selection;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime;
using SadRogue.Primitives;

internal static class ViewportGeometryRegressionTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== Viewport Geometry Regression Tests ===");
        TestOddViewportUsesCeilingDivisionAtEveryZoom();
        TestCurrentEventCoordinatesRemainAuthoritative();
        TestDragSelectionUsesIndependentWorldAndZBounds();
        TestKeyboardAndPlacementUseRuntimeBounds();
        TestRuntimeUsesActualWorldBounds();
        Console.WriteLine("=== Viewport Geometry Regression Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestOddViewportUsesCeilingDivisionAtEveryZoom()
    {
        var bounds = new RuntimeWorldBounds(0, 0, 11, 9, 2, 7);
        var surface = new RuntimeRect(5, 7, 7, 5);
        for (int zoom = 1; zoom <= 4; zoom++)
        {
            var viewport = RuntimeViewportGeometryMath.Normalize(new RuntimeViewportGeometry(
                surface,
                new RuntimePoint(999, 999),
                zoom,
                999,
                bounds));
            int expectedWidth = ((surface.Width - 1) / zoom) + 1;
            int expectedHeight = ((surface.Height - 1) / zoom) + 1;

            RegressionAssert.True(
                RuntimeViewportGeometryMath.VisibleWorldWidth(viewport) == expectedWidth
                && RuntimeViewportGeometryMath.VisibleWorldHeight(viewport) == expectedHeight
                && viewport.CameraWorldOrigin.X == bounds.MaxXExclusive - expectedWidth
                && viewport.CameraWorldOrigin.Y == bounds.MaxYExclusive - expectedHeight
                && viewport.CurrentZ == bounds.MaxZExclusive - 1,
                $"Odd viewport clamp was incorrect at zoom {zoom}.");

            var lastSurfaceCell = new RuntimePoint(surface.X + surface.Width - 1, surface.Y + surface.Height - 1);
            RegressionAssert.True(
                RuntimeViewportGeometryMath.TrySurfaceToWorld(viewport, lastSurfaceCell, out var lastWorldCell)
                && lastWorldCell.X == bounds.MaxXExclusive - 1
                && lastWorldCell.Y == bounds.MaxYExclusive - 1
                && RuntimeViewportGeometryMath.TryWorldToSurface(viewport, lastWorldCell, out var projected)
                && projected.X >= surface.X
                && projected.X < surface.X + surface.Width
                && projected.Y >= surface.Y
                && projected.Y < surface.Y + surface.Height,
                $"Surface/world border projection was inconsistent at zoom {zoom}.");
        }

        Console.WriteLine("[PASS] Odd viewport uses ceiling division at zoom 1-4");
    }

    private static void TestCurrentEventCoordinatesRemainAuthoritative()
    {
        var bounds = new RuntimeWorldBounds(0, 0, 32, 32, 0, 4);
        var beforeCameraMove = new RuntimeViewportGeometry(
            new RuntimeRect(0, 0, 9, 7),
            new RuntimePoint(1, 1),
            2,
            0,
            bounds);
        var afterCameraMove = beforeCameraMove with { CameraWorldOrigin = new RuntimePoint(5, 1) };
        var localClick = new Point(4, 2);

        RegressionAssert.True(
            FortressMapClickInput.TryResolveWorldPosition(
                localMousePosition: localClick,
                viewport: beforeCameraMove,
                worldPosition: out var oldWorld),
            "Initial map click did not resolve.");
        RegressionAssert.True(
            FortressMapClickInput.TryResolveWorldPosition(localClick, afterCameraMove, out var currentWorld)
            && oldWorld == new Point(3, 2)
            && currentWorld == new Point(7, 2),
            "Map click reused stale hover coordinates after the camera moved.");

        var hover = FortressMouseHoverInput.Handle(localClick, afterCameraMove, default);
        RegressionAssert.True(
            hover.Changed && hover.CursorPosition == currentWorld,
            "Hover and click did not use the same canonical transform.");

        Console.WriteLine("[PASS] Current event coordinates remain authoritative");
    }

    private static void TestDragSelectionUsesIndependentWorldAndZBounds()
    {
        var bounds = new RuntimeWorldBounds(0, 0, 100, 80, 0, 12);
        var selection = new DragRectSelectionTool(bounds);
        selection.Begin(new Point(500, 500), 500);
        selection.SetZRangeEnd(-10);
        var current = selection.Current;

        RegressionAssert.True(
            current.XY.X + current.XY.Width - 1 == 99
            && current.XY.Y + current.XY.Height - 1 == 79
            && current.ZMin == 0
            && current.ZMax == 11,
            "Drag selection reused XY size as its Z bound.");

        Console.WriteLine("[PASS] Drag selection uses independent XY and Z bounds");
    }

    private static void TestRuntimeUsesActualWorldBounds()
    {
        var runtime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 3);
        var request = new SimulationAppFrameRequestData(
            IncludeMapViewport: true,
            Viewport: new RuntimeViewportGeometry(
                new RuntimeRect(3, 4, 7, 5),
                new RuntimePoint(999, 999),
                4,
                999,
                new RuntimeWorldBounds(0, 0, 9999, 9999, 0, 999)),
            CursorPosition: new RuntimePoint(0, 0),
            CursorGlyph: 'X',
            NavigationMode: SimulationNavigationOverlayMode.None,
            SelectedNavigationTarget: null,
            TileInspectionWorldPosition: new RuntimePoint(0, 0),
            TileInspectionZ: 0,
            ShowZoneOverlay: false,
            IncludeManagementDrawer: false,
            IncludeWorkDrawer: false,
            IncludeDebugMenu: false,
            StockpileDetailZoneId: null,
            ZoneDetailId: null,
            PlacementPreviewRequests: Array.Empty<SimulationPlacementPreviewRequestData>(),
            NavigationPathRequest: null);
        runtime.StartFortressPlay(enqueueAutoDig: false);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        SimulationAppFrameData committed;
        do
        {
            committed = runtime.GetCommittedAppFrame(request);
            if (committed.IsAvailable)
                break;
            Thread.Sleep(5);
        }
        while (DateTime.UtcNow < deadline);
        runtime.StopIfRunning();
        var frame = committed.FrameRender;

        RegressionAssert.True(
            committed.IsAvailable
            && frame.MapViewport.Viewport.WorldBounds == new RuntimeWorldBounds(0, 0, 64, 64, 0, 3)
            && frame.MapViewport.Viewport.CameraWorldOrigin == new RuntimePoint(62, 62)
            && frame.MapViewport.CurrentZ == 2
            && frame.MapViewport.Width == 7
            && frame.MapViewport.Height == 5
            && frame.MapViewport.Cells.Count == 35,
            "Runtime trusted caller-supplied bounds instead of the active World dimensions.");

        Console.WriteLine("[PASS] Runtime uses actual world bounds");
    }

    private static void TestKeyboardAndPlacementUseRuntimeBounds()
    {
        var bounds = new RuntimeWorldBounds(10, 20, 30, 40, 3, 8);
        var clamped = HumanFortress.App.UI.Placement.FortressPlacementGeometry.ClampToWorld(
            new Point(999, -999),
            bounds);

        RegressionAssert.True(
            FortressKeyboardNavigationInput.ClampZ(-100, bounds) == 3
            && FortressKeyboardNavigationInput.ClampZ(100, bounds) == 7
            && clamped == new Point(39, 20),
            "Keyboard or placement derived bounds from hard-coded fortress dimensions.");

        Console.WriteLine("[PASS] Keyboard and placement use Runtime world bounds");
    }
}
