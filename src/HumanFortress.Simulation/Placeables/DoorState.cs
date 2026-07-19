namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Door state component (only for placeables with passability=doorway)
/// </summary>
internal sealed record DoorState(bool IsOpen = false, bool IsLocked = false)
{
}
