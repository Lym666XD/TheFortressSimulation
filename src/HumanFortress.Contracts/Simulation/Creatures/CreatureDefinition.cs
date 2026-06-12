using System.Collections.Generic;

namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Static definition of a creature type loaded from data/core/creatures/*.json.
/// Based on CREATURE_SPEC.md.
/// </summary>
public sealed class CreatureDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Size { get; set; } = "MEDIUM";
    public string Description { get; set; } = "";

    // Stats
    public float BaseSpeed { get; set; } = 1.0f;
    public int BaseStrength { get; set; } = 10;
    public int BaseAgility { get; set; } = 10;
    public int BaseToughness { get; set; } = 10;
    public int BaseIntelligence { get; set; } = 10;

    // Skills (optional)
    public List<string> Skills { get; set; } = new();
}
