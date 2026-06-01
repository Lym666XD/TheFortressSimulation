namespace HumanFortress.App.Runtime;

/// <summary>
/// Read-only snapshot of runtime clock controls for UI rendering.
/// </summary>
public readonly record struct SimulationStatus(
    ulong CurrentTick,
    bool IsPaused,
    float SpeedMultiplier);
