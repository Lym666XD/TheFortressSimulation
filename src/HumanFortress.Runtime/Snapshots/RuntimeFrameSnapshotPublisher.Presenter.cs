using System.Text.Json;
using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    private SimulationSnapshotPresenterFrameData PublishPresenterFrame<TSnapshot>(
        string surface,
        string requestHash,
        TSnapshot snapshot,
        bool isUiOverlayFrame)
    {
        var payloadHash = BuildSnapshotPayloadHash(surface, requestHash, snapshot);
        lock (_gate)
        {
            var previousFrame = isUiOverlayFrame
                ? _lastUiOverlayFrame
                : _lastFrameRender;
            var canDiffFromPrevious = false;
            string? deltaBaseHash = null;
            if (previousFrame.HasValue)
            {
                var previous = previousFrame.Value;
                canDiffFromPrevious = string.Equals(previous.Surface, surface, StringComparison.Ordinal)
                    && string.Equals(previous.RequestHash, requestHash, StringComparison.Ordinal);
                if (canDiffFromPrevious)
                    deltaBaseHash = previous.PayloadHash;
            }

            var presenterFrame = SimulationSnapshotPresenterFrameData.FullSnapshot(
                ++_nextPublicationSequence,
                payloadHash,
                deltaBaseHash,
                canDiffFromPrevious);
            if (isUiOverlayFrame)
                _lastUiOverlayFrame = new PublishedFrameIdentity(surface, requestHash, payloadHash);
            else
                _lastFrameRender = new PublishedFrameIdentity(surface, requestHash, payloadHash);
            return presenterFrame;
        }
    }

    private static string BuildSnapshotPayloadHash<TSnapshot>(
        string surface,
        string requestHash,
        TSnapshot snapshot)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, PayloadJsonOptions);
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.snapshot.presenter.payload.v1");
            hash.AddString(surface);
            hash.AddString(requestHash);
            hash.AddBytes(payload);
        });
    }
}
