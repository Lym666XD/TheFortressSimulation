using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime;
using SadRogue.Primitives;

internal static class RuntimeAppFrameBehaviorTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== Runtime App Frame Behavior Tests ===");
        TestActiveRendererUsesOneUnifiedFrameRead();
        TestRuntimePublishesOneExactCommittedAppFrame();
        TestAppQueriesUseOnlyExactCommittedFrameCache();
        Console.WriteLine("=== Runtime App Frame Behavior Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestActiveRendererUsesOneUnifiedFrameRead()
    {
        string root = TestRepositoryPaths.FindRepositoryRoot();
        string frameRenderer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Rendering",
            "FortressFrameRenderer.cs"));
        string overlayRenderer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Rendering",
            "FortressUiOverlayRenderer.cs"));
        string mapOverlayRenderer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Rendering",
            "FortressMapOverlayRenderer.cs"));
        string placementOverlayRenderer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Rendering",
            "FortressPlacementOverlayRenderer.Anchored.cs"));
        string navigationController = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Input",
            "FortressNavigationDebugController.cs"));
        string runtimeAccess = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.App",
            "Runtime",
            "FortressRuntimeAccess.cs"));
        string publicReadPort = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.Runtime",
            "FortressRuntimeSessionPorts.Read.cs"));
        string sessionPorts = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HumanFortress.Runtime",
            "FortressRuntimeSessionPorts.cs"));
        string publicAppPorts = sessionPorts[..sessionPorts.IndexOf(
            "internal interface IFortressRuntimeSessionPorts",
            StringComparison.Ordinal)];
        var committedQueryFiles = new[]
        {
            "FortressRuntimeAccess.DebugQueries.cs",
            "FortressRuntimeAccess.MapInspectionQueries.cs",
            "FortressRuntimeAccess.NavigationQueries.cs",
            "FortressRuntimeAccess.UiInputQueries.cs",
            "FortressRuntimeAccess.WorkshopPanelQueries.cs",
        };
        bool committedQueriesAvoidSnapshots = committedQueryFiles.All(file =>
            !File.ReadAllText(Path.Combine(
                    root,
                    "src",
                    "HumanFortress.App",
                    "Runtime",
                    file))
                .Contains("_snapshots", StringComparison.Ordinal));

        RegressionAssert.True(
            CountOccurrences(frameRenderer, ".GetCommittedAppFrame(") == 1
            && !frameRenderer.Contains(".GetFrameRenderData(", StringComparison.Ordinal)
            && !frameRenderer.Contains(".GetUiOverlayFrameData(", StringComparison.Ordinal)
            && !overlayRenderer.Contains("GetUiOverlayFrameData(", StringComparison.Ordinal)
            && !overlayRenderer.Contains("context.Runtime.Read", StringComparison.Ordinal)
            && !overlayRenderer.Contains("runtime.SimulationStatus", StringComparison.Ordinal)
            && !mapOverlayRenderer.Contains("GetPlacementPreviewData(", StringComparison.Ordinal)
            && !mapOverlayRenderer.Contains("context.Runtime.Read", StringComparison.Ordinal)
            && !placementOverlayRenderer.Contains("GetPlacementPreviewData(", StringComparison.Ordinal)
            && !placementOverlayRenderer.Contains("context.Runtime.Read", StringComparison.Ordinal)
            && !navigationController.Contains("FindNavigationDebugPath(", StringComparison.Ordinal)
            && committedQueriesAvoidSnapshots
            && !runtimeAccess.Contains("IFortressRuntimeSessionSnapshotPort", StringComparison.Ordinal)
            && !publicReadPort.Contains("GetFrameRenderData(", StringComparison.Ordinal)
            && !publicReadPort.Contains("GetUiOverlayFrameData(", StringComparison.Ordinal)
            && !publicReadPort.Contains("GetPlacementPreviewData(", StringComparison.Ordinal)
            && !publicReadPort.Contains("SimulationStatus", StringComparison.Ordinal)
            && !publicAppPorts.Contains(
                "IFortressRuntimeSessionSnapshotPort",
                StringComparison.Ordinal),
            "The active App renderer still performed split or live frame reads.");

        Console.WriteLine("[PASS] Active renderer uses one unified committed frame read");
    }

    private static void TestRuntimePublishesOneExactCommittedAppFrame()
    {
        var runtime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 3);
        var bounds = runtime.GetWorldAvailabilityData().WorldBounds;
        var placementRequests = new[]
        {
            new SimulationPlacementPreviewRequestData(
                new RuntimePoint(2, 1),
                new RuntimePoint(0, 0),
                0,
                SimulationPlacementPreviewMode.GroundItems),
            new SimulationPlacementPreviewRequestData(
                new RuntimePoint(0, 0),
                new RuntimePoint(2, 1),
                0,
                SimulationPlacementPreviewMode.MiningDig),
            new SimulationPlacementPreviewRequestData(
                new RuntimePoint(0, 0),
                new RuntimePoint(2, 1),
                0,
                SimulationPlacementPreviewMode.GroundItems),
        };
        var firstRequest = CreateRequest(bounds, cameraX: 0, placementRequests);

        RegressionAssert.True(
            !runtime.GetCommittedAppFrame(firstRequest).IsAvailable,
            "Bootstrap returned a live fallback instead of Unavailable.");

        runtime.StartFortressPlay(enqueueAutoDig: false);
        try
        {
            var first = WaitForExactFrame(runtime, firstRequest);
            AssertCoherentIdentity(first, firstRequest);
            AssertCanonicalPlacementPreviews(first, placementRequests, expectedCount: 2);
            string oldIdentityHash = first.CheckpointIdentity.AggregateHash;
            string oldMapPayloadHash = first.FrameRender.MapViewport.Delta.PayloadHash;
            int oldCameraX = first.FrameRender.MapViewport.CameraX;
            var oldCells = first.FrameRender.MapViewport.Cells.ToArray();

            var exposedSections = first.CheckpointIdentity.Sections
                as IList<HumanFortress.Contracts.Runtime.Checkpoints.RuntimeCheckpointSectionIdentityData>;
            RegressionAssert.True(
                exposedSections is { Count: > 0 },
                "The App-frame immutability test could not mutate its consumer-owned section copy.");
            var originalSection = first.CheckpointIdentity.Sections[0];
            exposedSections![0] = originalSection with { SectionId = "consumer-mutation" };
            var freshFirst = runtime.GetCommittedAppFrame(firstRequest);
            bool freshHasSection = freshFirst.CheckpointIdentity.Sections is { Count: > 0 };
            var immutabilityChecks = new (string Name, bool Passed)[]
            {
                ("available", freshFirst.IsAvailable),
                ("section-present", freshHasSection),
                ("section-id-owned", freshHasSection
                    && freshFirst.CheckpointIdentity.Sections[0].SectionId == originalSection.SectionId
                    && freshFirst.CheckpointIdentity.Sections[0].SectionId != "consumer-mutation"),
            };
            RegressionAssert.True(
                immutabilityChecks.All(static check => check.Passed),
                "A consumer mutation changed the retained committed App frame: "
                + string.Join(", ", immutabilityChecks
                    .Where(static check => !check.Passed)
                    .Select(static check => check.Name)));

            var changedPlacementRequests = placementRequests.Append(
                new SimulationPlacementPreviewRequestData(
                    new RuntimePoint(1, 1),
                    new RuntimePoint(3, 2),
                    0,
                    SimulationPlacementPreviewMode.ConstructionFloor))
                .ToArray();
            var previewChangedRequest = firstRequest with
            {
                PlacementPreviewRequests = changedPlacementRequests,
            };
            var previewChangedImmediate = runtime.GetCommittedAppFrame(previewChangedRequest);
            RegressionAssert.True(
                !previewChangedImmediate.IsAvailable,
                "A changed placement-preview request reused the previous committed frame.");
            var previewChanged = WaitForExactFrame(runtime, previewChangedRequest);
            AssertCoherentIdentity(previewChanged, previewChangedRequest);
            AssertCanonicalPlacementPreviews(
                previewChanged,
                changedPlacementRequests,
                expectedCount: 3);
            RegressionAssert.True(
                previewChanged.CheckpointIdentity.RuntimeTick > first.CheckpointIdentity.RuntimeTick,
                "Changed placement previews were not committed by a later simulation tick.");

            var secondRequest = previewChangedRequest with
            {
                Viewport = previewChangedRequest.Viewport with
                {
                    CameraWorldOrigin = new RuntimePoint(1, 0),
                },
            };
            var immediate = runtime.GetCommittedAppFrame(secondRequest);
            RegressionAssert.True(
                !immediate.IsAvailable,
                "A committed frame for the old camera was returned as the new request.");

            var second = WaitForExactFrame(runtime, secondRequest);
            AssertCoherentIdentity(second, secondRequest);

            RegressionAssert.True(
                first.CheckpointIdentity.AggregateHash == oldIdentityHash
                && first.FrameRender.MapViewport.Delta.PayloadHash == oldMapPayloadHash
                && first.FrameRender.MapViewport.CameraX == oldCameraX
                && first.FrameRender.MapViewport.Cells.SequenceEqual(oldCells)
                && second.FrameRender.MapViewport.CameraX == 1
                && second.CheckpointIdentity.RuntimeTick > previewChanged.CheckpointIdentity.RuntimeTick,
                "Publishing a later exact request mutated or masqueraded as the old committed frame.");
        }
        finally
        {
            runtime.StopIfRunning();
        }

        Console.WriteLine("[PASS] Runtime publishes coherent exact-request committed App frames");
    }

    private static SimulationAppFrameData WaitForExactFrame(
        IFortressRuntimeAppSessionPorts runtime,
        SimulationAppFrameRequestData request)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        SimulationAppFrameData frame;
        do
        {
            frame = runtime.GetCommittedAppFrame(request);
            if (frame.IsAvailable)
                return frame;
            Thread.Sleep(5);
        }
        while (DateTime.UtcNow < deadline);

        throw new InvalidOperationException("Runtime did not publish an exact committed App frame before timeout.");
    }

    private static void TestAppQueriesUseOnlyExactCommittedFrameCache()
    {
        var session = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var runtime = new FortressRuntimeAccess(session);
        session.InitializeWorld(sizeInChunks: 2, maxZ: 3);
        var bounds = session.GetWorldAvailabilityData().WorldBounds;
        var navigationRequest = new SimulationNavigationPathRequestData(
            new RuntimePoint(0, 0),
            0,
            new RuntimePoint(1, 0),
            0);
        var request = CreateRequest(bounds, cameraX: 0) with
        {
            NavigationPathRequest = navigationRequest,
        };

        session.StartFortressPlay(enqueueAutoDig: false);
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            SimulationAppFrameData frame;
            do
            {
                frame = runtime.GetCommittedAppFrame(request);
                if (frame.IsAvailable)
                    break;
                Thread.Sleep(5);
            }
            while (DateTime.UtcNow < deadline);

            var exactPath = runtime.FindNavigationDebugPath(
                new Point(0, 0),
                0,
                new Point(1, 0),
                0);
            var mismatchedPath = runtime.FindNavigationDebugPath(
                new Point(0, 0),
                0,
                new Point(2, 0),
                0);
            bool exactPathWasAvailable = exactPath.HasResult;
            bool mismatchedPathWasUnavailable = !mismatchedPath.HasResult;
            var changedRequest = request with
            {
                NavigationPathRequest = navigationRequest with
                {
                    Destination = new RuntimePoint(2, 0),
                },
            };
            var changedImmediate = runtime.GetCommittedAppFrame(changedRequest);
            var stalePathAfterMismatch = runtime.FindNavigationDebugPath(
                new Point(0, 0),
                0,
                new Point(1, 0),
                0);
            bool mismatchedQueriesWereEmpty =
                !runtime.FindZoneAt(new Point(0, 0), z: 1).HasZone
                && !runtime.FindStockpileAt(new Point(0, 0), z: 1).HasZone
                && !runtime.GetTileInspectionData(new Point(2, 2), tileZ: 0).HasTile
                && runtime.GetWorkshopPanelData(Guid.NewGuid()) == null
                && !runtime.GetDebugMenuData().WorldStatus.HasWorld
                && runtime.GetWorkforceInputData().TotalWorkers == 0;
            deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            SimulationAppFrameData changedFrame;
            do
            {
                changedFrame = runtime.GetCommittedAppFrame(changedRequest);
                if (changedFrame.IsAvailable)
                    break;
                Thread.Sleep(5);
            }
            while (DateTime.UtcNow < deadline);
            RegressionAssert.True(
                frame.IsAvailable
                && frame.NavigationPath.IsAvailable
                && frame.NavigationPath.Request == navigationRequest
                && frame.NavigationPath.Metadata.RuntimeTick
                    == frame.CheckpointIdentity.RuntimeTick
                && exactPathWasAvailable
                && mismatchedPathWasUnavailable
                && !changedImmediate.IsAvailable
                && !stalePathAfterMismatch.HasResult
                && mismatchedQueriesWereEmpty
                && changedFrame.IsAvailable
                && changedFrame.NavigationPath.Request == changedRequest.NavigationPathRequest
                && changedFrame.NavigationPath.Metadata.RuntimeTick
                    == changedFrame.CheckpointIdentity.RuntimeTick
                && changedFrame.CheckpointIdentity.RuntimeTick
                    > frame.CheckpointIdentity.RuntimeTick,
                "App query cache returned a mismatched or live-derived result.");
        }
        finally
        {
            session.StopIfRunning();
        }

        Console.WriteLine("[PASS] App queries return only exact committed-frame cache results");
    }

    private static void AssertCoherentIdentity(
        SimulationAppFrameData frame,
        SimulationAppFrameRequestData request)
    {
        ulong tick = frame.CheckpointIdentity.RuntimeTick;
        var sectionIds = frame.CheckpointIdentity.Sections
            .Select(static section => section.SectionId)
            .ToHashSet(StringComparer.Ordinal);
        RegressionAssert.True(
            frame.IsAvailable
            && frame.FrameRender.Metadata.RuntimeTick == tick
            && frame.UiOverlay.Metadata.RuntimeTick == tick
            && frame.PlacementPreviews.Metadata.RuntimeTick == tick
            && frame.NavigationPath.Metadata.RuntimeTick == tick
            && frame.SimulationStatus.CurrentTick == tick
            && frame.FrameRender.MapViewport.CameraX == request.Viewport.CameraWorldOrigin.X
            && frame.FrameRender.MapViewport.CameraY == request.Viewport.CameraWorldOrigin.Y
            && sectionIds.SetEquals(new[]
            {
                "app-frame.overlay",
                "app-frame.navigation-path",
                "app-frame.placement-previews",
                "app-frame.render",
                "app-frame.request",
                "app-frame.status",
                "jobs.professions",
                "runtime-diagnostics",
                "runtime-replay",
            }),
            "Committed App frame metadata or checkpoint section identity was torn.");
    }

    private static void AssertCanonicalPlacementPreviews(
        SimulationAppFrameData frame,
        IReadOnlyList<SimulationPlacementPreviewRequestData> requested,
        int expectedCount)
    {
        var expected = SimulationPlacementPreviewRequestData.CanonicalizeAll(requested);
        var actual = frame.PlacementPreviews.Rows
            .Select(static row => row.Request)
            .ToArray();
        RegressionAssert.True(
            actual.Length == expectedCount
            && actual.SequenceEqual(expected)
            && frame.PlacementPreviews.Rows.All(static row => row.Preview.TotalCells > 0),
            "Placement previews were not canonical, deduplicated, or keyed by request.");
    }

    private static SimulationAppFrameRequestData CreateRequest(
        RuntimeWorldBounds bounds,
        int cameraX,
        IReadOnlyList<SimulationPlacementPreviewRequestData>? placementPreviewRequests = null)
    {
        return new SimulationAppFrameRequestData(
            IncludeMapViewport: true,
            Viewport: new RuntimeViewportGeometry(
                new RuntimeRect(0, 0, 10, 7),
                new RuntimePoint(cameraX, 0),
                ZoomLevel: 1,
                CurrentZ: 0,
                bounds),
            CursorPosition: new RuntimePoint(cameraX, 0),
            CursorGlyph: 'X',
            NavigationMode: SimulationNavigationOverlayMode.None,
            SelectedNavigationTarget: null,
            TileInspectionWorldPosition: new RuntimePoint(cameraX, 0),
            TileInspectionZ: 0,
            ShowZoneOverlay: false,
            IncludeManagementDrawer: false,
            IncludeWorkDrawer: false,
            IncludeDebugMenu: false,
            StockpileDetailZoneId: null,
            ZoneDetailId: null,
            PlacementPreviewRequests: placementPreviewRequests
                ?? Array.Empty<SimulationPlacementPreviewRequestData>(),
            NavigationPathRequest: null);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
