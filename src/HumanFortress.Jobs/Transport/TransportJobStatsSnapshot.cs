namespace HumanFortress.Jobs.Transport;

internal readonly record struct TransportJobStatsSnapshot(
    int Intake,
    int Active,
    int Backlog,
    int CompletedDelta,
    int RequeuedDelta,
    int NoPathDelta,
    int CarryoverOld);
