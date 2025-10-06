namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Mining action kinds for advanced mining orders.
/// </summary>
public enum MiningAction : byte
{
    Dig,
    DigStairwell,
    DigRamp,
    DigChannel,
    RemoveDigging
}

