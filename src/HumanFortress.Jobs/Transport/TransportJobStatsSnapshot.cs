namespace HumanFortress.Jobs.Transport;

public readonly record struct TransportJobStatsSnapshot(
    int Intake,
    int Active,
    int Backlog,
    int CompletedDelta,
    int RequeuedDelta,
    int NoPathDelta,
    int CarryoverOld);
