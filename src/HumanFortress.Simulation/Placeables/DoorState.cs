namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Door state component (only for placeables with passability=doorway)
/// </summary>
internal sealed class DoorState
{
    /// <summary>
    /// Is door currently open (affects passability)
    /// </summary>
    public bool IsOpen { get; set; } = false;

    /// <summary>
    /// Is door locked (blocks opening)
    /// </summary>
    public bool IsLocked { get; set; } = false;
}
