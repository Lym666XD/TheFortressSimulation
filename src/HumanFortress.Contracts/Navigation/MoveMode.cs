namespace HumanFortress.Navigation;

/// <summary>
/// Movement mode for pathfinding requests.
/// Determines which capabilities are required.
/// </summary>
public enum MoveMode : byte
{
    /// <summary>Standard walking movement.</summary>
    Walk = 0,

    /// <summary>Crawling through tight spaces.</summary>
    Crawl = 1,

    /// <summary>Swimming through fluids.</summary>
    Swim = 2,

    /// <summary>Flying over obstacles.</summary>
    Fly = 3,
}
