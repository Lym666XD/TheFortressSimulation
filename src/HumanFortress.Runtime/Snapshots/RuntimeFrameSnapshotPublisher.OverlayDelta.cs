using System.Text.Json;
using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    private SimulationUiOverlayFrameDeltaData PublishUiOverlayDelta(
        SimulationUiOverlayFrameData frame,
        string requestHash)
    {
        var sectionHashes = BuildUiOverlaySectionHashes(frame);
        var payloadHash = BuildUiOverlaySectionPayloadHash(sectionHashes);

        lock (_gate)
        {
            var previousFrame = _lastUiOverlaySections;
            var hasPreviousFrame = previousFrame.HasValue;
            var previous = previousFrame.GetValueOrDefault();
            var canApplyToBase = hasPreviousFrame
                && string.Equals(previous.RequestHash, requestHash, StringComparison.Ordinal);
            var changedSections = canApplyToBase
                ? BuildChangedUiOverlaySections(sectionHashes, previous.SectionHashesById)
                : sectionHashes.Select(static section => section.Section).ToArray();

            _lastUiOverlaySections = new PublishedUiOverlaySectionsFrame(
                requestHash,
                payloadHash,
                sectionHashes.ToDictionary(
                    static section => section.Section,
                    static section => section.PayloadHash,
                    StringComparer.Ordinal));

            return canApplyToBase
                ? SimulationUiOverlayFrameDeltaData.Delta(
                    payloadHash,
                    previous.PayloadHash,
                    sectionHashes,
                    changedSections)
                : SimulationUiOverlayFrameDeltaData.FullSnapshot(
                    payloadHash,
                    sectionHashes,
                    changedSections);
        }
    }

    private static SimulationUiOverlaySectionHashData[] BuildUiOverlaySectionHashes(
        SimulationUiOverlayFrameData frame)
    {
        return new[]
        {
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.BuildCatalog, frame.BuildCatalog),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.Jobs, frame.Jobs),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.Workshops, frame.Workshops),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.StockpilePresets, frame.StockpilePresets),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.StockpileOverlay, frame.StockpileOverlay),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.StockpileDetail, frame.StockpileDetail),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.ZoneOverlay, frame.ZoneOverlay),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.ZoneDetail, frame.ZoneDetail),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.ZoneCatalog, frame.ZoneCatalog),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.ManagementDrawer, frame.ManagementDrawer),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.WorkDrawer, frame.WorkDrawer),
            BuildUiOverlaySectionHash(SimulationUiOverlayFrameSection.DebugMenu, frame.DebugMenu)
        };
    }

    private static SimulationUiOverlaySectionHashData BuildUiOverlaySectionHash<TSection>(
        string section,
        TSection sectionData)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(sectionData, PayloadJsonOptions);
        var payloadHash = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.ui-overlay.section.payload.v1");
            hash.AddString(section);
            hash.AddBytes(payload);
        });
        return new SimulationUiOverlaySectionHashData(section, payloadHash);
    }

    private static string BuildUiOverlaySectionPayloadHash(
        IReadOnlyList<SimulationUiOverlaySectionHashData> sectionHashes)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.frame.ui-overlay.sections.payload.v1");
            hash.AddInt32(sectionHashes.Count);
            foreach (var sectionHash in sectionHashes)
            {
                hash.AddString(sectionHash.Section);
                hash.AddString(sectionHash.PayloadHash);
            }
        });
    }

    private static string[] BuildChangedUiOverlaySections(
        IReadOnlyList<SimulationUiOverlaySectionHashData> currentSectionHashes,
        IReadOnlyDictionary<string, string> previousSectionHashesById)
    {
        return currentSectionHashes
            .Where(sectionHash =>
            {
                return !previousSectionHashesById.TryGetValue(sectionHash.Section, out var previousHash)
                    || !string.Equals(previousHash, sectionHash.PayloadHash, StringComparison.Ordinal);
            })
            .Select(static sectionHash => sectionHash.Section)
            .ToArray();
    }
}
