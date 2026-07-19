using SadRogue.Primitives;

namespace HumanFortress.Jobs.Craft;

internal readonly record struct CraftActiveJobStateSnapshot(
    int Order,
    Guid WorkerId,
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    CraftJobStage Stage,
    int WorkTicksRemaining,
    Point Anchor,
    int Z,
    byte PathSearchAttempt = 0);

internal readonly record struct CraftBacklogEntrySnapshot(
    int Order,
    PlannedCraftJob Job);

internal readonly record struct CraftJobReplaySnapshot(
    IReadOnlyList<CraftActiveJobStateSnapshot> ActiveJobs,
    IReadOnlyList<CraftBacklogEntrySnapshot> BacklogEntries);
