using SadRogue.Primitives;

namespace HumanFortress.Jobs.Craft;

public readonly record struct PlannedCraftJob(
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    int DurationTicks,
    Point Anchor,
    int Z);

public readonly record struct CraftJobStatsSnapshot(int Intake, int Active, int Backlog, int CompletedDelta);

public readonly record struct ActiveCraftJobView(Guid WorkerId, Guid WorkshopGuid, string RecipeId, string Stage, int RemainingTicks);

internal sealed class ActiveCraftJob
{
    public Guid WorkerId { get; set; }
    public Guid WorkshopGuid { get; set; }
    public Guid QueueEntryId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public CraftJobStage Stage { get; set; }
    public int WorkTicksRemaining { get; set; }
    public Point Anchor { get; set; }
    public int Z { get; set; }
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
