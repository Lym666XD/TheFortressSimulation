namespace HumanFortress.Navigation;

/// <summary>
/// Optional flags for pathfinding behavior.
/// </summary>
[Flags]
public enum PathFlags : byte
{
    /// <summary>No special flags.</summary>
    None = 0,

    /// <summary>Avoid hazardous areas (fire, miasma).</summary>
    AvoidHazard = 1 << 0,

    /// <summary>Prefer low-traffic roads.</summary>
    PreferRoad = 1 << 1,

    /// <summary>Allow passing through doors.</summary>
    AllowDoors = 1 << 2,

    /// <summary>Avoid water when possible.</summary>
    AvoidWater = 1 << 3,

    /// <summary>Allow diagonal movement (8-neighbor).</summary>
    AllowDiagonal = 1 << 4,
}