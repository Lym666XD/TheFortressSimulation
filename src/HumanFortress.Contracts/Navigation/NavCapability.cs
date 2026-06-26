namespace HumanFortress.Contracts.Navigation;

/// <summary>
/// Navigation capability bits per NAVIGATION_SPEC.md section 3.1.
/// Packed into NavMask byte per tile.
/// </summary>
[Flags]
public enum NavCapability : byte
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>4-neighbor planar motion.</summary>
    Walk = 1 << 0,

    /// <summary>Reserved for tight tunnels.</summary>
    Crawl = 1 << 1,

    /// <summary>Allow motion when FluidDepth >= threshold.</summary>
    Swim = 1 << 2,

    /// <summary>Airborne, ignores many L0/L2 tests.</summary>
    Fly = 1 << 3,

    /// <summary>Valid "stop" tile for walkers.</summary>
    Standable = 1 << 4,

    /// <summary>Ladders/rope treated as vertical neighbor.</summary>
    EdgeClimb = 1 << 5,

    // bits 6..7 reserved for future use
}
