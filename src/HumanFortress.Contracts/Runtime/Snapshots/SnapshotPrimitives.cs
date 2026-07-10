namespace HumanFortress.Contracts.Runtime.Snapshots;

public static class SimulationSnapshotSchema
{
    public const int CurrentVersion = 1;
}

public static class SimulationSnapshotPublicationSchema
{
    public const int CurrentVersion = 1;
}

public static class SimulationSnapshotPresenterFrameSchema
{
    public const int CurrentVersion = 1;
}

public static class SimulationSnapshotPublicationSurface
{
    public const string UiOverlayFrame = "ui-overlay-frame";
    public const string FrameRender = "frame-render";
}

public static class SimulationSnapshotTransferMode
{
    public const string FullSnapshot = "full-snapshot";
}

public static class SimulationSnapshotPayloadHashAlgorithm
{
    public const string CanonicalSha256V1 = "sha256-v1";
    public const string JsonSha256V1 = "snapshot-json-sha256-v1";
}

public readonly record struct SimulationSnapshotMetadata(
    int SchemaVersion,
    ulong RuntimeTick)
{
    public static SimulationSnapshotMetadata Current(ulong runtimeTick)
    {
        return new SimulationSnapshotMetadata(
            SimulationSnapshotSchema.CurrentVersion,
            runtimeTick);
    }
}

public readonly record struct SimulationSnapshotPublicationData(
    int SchemaVersion,
    string Surface,
    string RequestHash,
    string RequestHashAlgorithm)
{
    public static SimulationSnapshotPublicationData Current(
        string surface,
        string requestHash,
        string requestHashAlgorithm)
    {
        return new SimulationSnapshotPublicationData(
            SimulationSnapshotPublicationSchema.CurrentVersion,
            surface,
            requestHash,
            requestHashAlgorithm);
    }
}

public readonly record struct SimulationSnapshotPresenterFrameData(
    int SchemaVersion,
    string TransferMode,
    ulong PublicationSequence,
    string PayloadHash,
    string PayloadHashAlgorithm,
    string? DeltaBasePayloadHash,
    bool CanDiffFromPrevious)
{
    public static SimulationSnapshotPresenterFrameData FullSnapshot(
        ulong publicationSequence,
        string payloadHash,
        string? deltaBasePayloadHash,
        bool canDiffFromPrevious)
    {
        return new SimulationSnapshotPresenterFrameData(
            SimulationSnapshotPresenterFrameSchema.CurrentVersion,
            SimulationSnapshotTransferMode.FullSnapshot,
            publicationSequence,
            payloadHash,
            SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1,
            deltaBasePayloadHash,
            canDiffFromPrevious);
    }
}

public readonly record struct SnapshotColor(int R, int G, int B);

public readonly record struct SnapshotPoint(int X, int Y);
