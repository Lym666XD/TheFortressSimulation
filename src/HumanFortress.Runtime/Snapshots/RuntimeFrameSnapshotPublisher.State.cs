using System.Text.Json;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal sealed partial class RuntimeFrameSnapshotPublisher
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private PublishedUiOverlayFrame? _uiOverlayFrame;
    private PublishedFrameRender? _frameRender;
    private PublishedFrameIdentity? _lastUiOverlayFrame;
    private PublishedFrameIdentity? _lastFrameRender;
    private PublishedUiOverlaySectionsFrame? _lastUiOverlaySections;
    private PublishedMapViewportFrame? _lastMapViewport;
    private ulong _nextPublicationSequence;

    private bool TryGetCachedUiOverlayFrame(
        ulong runtimeTick,
        RuntimeUiOverlayFrameRequest request,
        out SimulationUiOverlayFrameData data)
    {
        lock (_gate)
        {
            if (_uiOverlayFrame is { } cached
                && cached.RuntimeTick == runtimeTick
                && cached.Request.Equals(request))
            {
                data = cached.Data;
                return true;
            }
        }

        data = default!;
        return false;
    }

    private void CacheUiOverlayFrame(
        ulong runtimeTick,
        RuntimeUiOverlayFrameRequest request,
        SimulationUiOverlayFrameData data)
    {
        lock (_gate)
        {
            _uiOverlayFrame = new PublishedUiOverlayFrame(runtimeTick, request, data);
        }
    }

    private bool TryGetCachedFrameRender(
        ulong runtimeTick,
        RuntimeFrameRenderRequest request,
        out SimulationFrameRenderData data)
    {
        lock (_gate)
        {
            if (_frameRender is { } cached
                && cached.RuntimeTick == runtimeTick
                && cached.Request.Equals(request))
            {
                data = cached.Data;
                return true;
            }
        }

        data = default!;
        return false;
    }

    private void CacheFrameRender(
        ulong runtimeTick,
        RuntimeFrameRenderRequest request,
        SimulationFrameRenderData data)
    {
        lock (_gate)
        {
            _frameRender = new PublishedFrameRender(runtimeTick, request, data);
        }
    }

    internal void Invalidate()
    {
        lock (_gate)
        {
            _uiOverlayFrame = null;
            _frameRender = null;
            _lastUiOverlayFrame = null;
            _lastFrameRender = null;
            _lastUiOverlaySections = null;
            _lastMapViewport = null;
        }
    }

    private readonly record struct PublishedUiOverlayFrame(
        ulong RuntimeTick,
        RuntimeUiOverlayFrameRequest Request,
        SimulationUiOverlayFrameData Data);

    private readonly record struct PublishedFrameRender(
        ulong RuntimeTick,
        RuntimeFrameRenderRequest Request,
        SimulationFrameRenderData Data);

    private readonly record struct PublishedFrameIdentity(
        string Surface,
        string RequestHash,
        string PayloadHash);

    private readonly record struct PublishedUiOverlaySectionsFrame(
        string RequestHash,
        string PayloadHash,
        IReadOnlyDictionary<string, string> SectionHashesById);

    private readonly record struct PublishedMapViewportFrame(
        string RequestHash,
        string PayloadHash,
        IReadOnlyDictionary<int, MapViewportCellView> CellsByKey,
        IReadOnlyDictionary<int, string> RowPayloadHashesByY,
        IReadOnlyDictionary<long, string> RegionPayloadHashesByKey);
}
