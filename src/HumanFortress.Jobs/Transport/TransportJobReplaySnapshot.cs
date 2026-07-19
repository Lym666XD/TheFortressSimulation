using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal readonly record struct TransportActiveJobStateSnapshot(
    int Order,
    Guid CreatureId,
    Guid ItemId,
    Point3 Destination,
    JobStage Stage,
    int Quantity,
    int InvalidReplanCount,
    TransportReason Reason,
    byte PathSearchAttempt = 0,
    MovementCursorData? MovementCursor = null);

internal readonly record struct TransportJobReplaySnapshot(
    int? IntakeCapHint,
    int? MaxActiveCapHint,
    int ReserveSlotsHint,
    IReadOnlyList<TransportActiveJobStateSnapshot> ActiveJobs,
    IReadOnlyList<TransportBacklogEntrySnapshot> BacklogEntries);
