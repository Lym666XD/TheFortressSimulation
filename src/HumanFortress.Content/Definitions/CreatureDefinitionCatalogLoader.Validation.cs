using HumanFortress.Contracts.Simulation.Creatures;

namespace HumanFortress.Content.Definitions;

internal static partial class CreatureDefinitionCatalogLoader
{
    private static void ValidateDefinition(CreatureDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
        {
            throw new ArgumentException("Creature ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException($"Creature '{def.Id}' has no name");
        }

        if (def.BaseSpeed <= 0)
        {
            throw new ArgumentException($"Creature '{def.Id}' has invalid speed: {def.BaseSpeed}");
        }

        if (def.BaseStrength < 1 || def.BaseStrength > 100)
        {
            throw new ArgumentException($"Creature '{def.Id}' has invalid strength: {def.BaseStrength}");
        }
    }
}
