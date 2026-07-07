using SadRogue.Primitives;

namespace HumanFortress.Jobs.Craft;

internal readonly record struct PlannedCraftJob(
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    int DurationTicks,
    Point Anchor,
    int Z);

internal readonly record struct CraftJobStatsSnapshot(int Intake, int Active, int Backlog, int CompletedDelta);

internal readonly record struct ActiveCraftJobView(Guid WorkerId, Guid WorkshopGuid, string RecipeId, string Stage, int RemainingTicks);

internal sealed class ActiveCraftJob
{
    internal Guid WorkerId { get; set; }
    internal Guid WorkshopGuid { get; set; }
    internal Guid QueueEntryId { get; set; }
    internal string RecipeId { get; set; } = string.Empty;
    internal CraftJobStage Stage { get; set; }
    internal int WorkTicksRemaining { get; set; }
    internal Point Anchor { get; set; }
    internal int Z { get; set; }
}

internal enum CraftJobStage
{
    ToWorkshop,
    Working
}

internal enum CraftAssignmentResult
{
    Invalid,
    Assigned,
    Backlog
}

internal enum CraftJobFinishReason
{
    Completed,
    InputsUnavailable,
    WorkerMissing
}
