using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    private static WorldSaveItemPayloadData ToPayloadItem(ItemInstance item)
    {
        return new WorldSaveItemPayloadData(
            item.Guid,
            item.DefinitionId,
            item.MaterialId,
            item.StackCount,
            ToPayloadPoint(item.Position),
            item.Z,
            item.ContainedBy,
            item.CarriedBy,
            item.EquippedBy,
            ToPayloadPlacement(item.InstalledAt),
            item.OwnerFactionId,
            item.OwnerCreatureGuid,
            (int)item.UsePolicy,
            item.Forbidden,
            ToPayloadReservationTokens(item.ReservationTokens),
            item.QualityTier,
            item.Artifact,
            item.ArtifactName,
            item.ConditionState,
            item.DurabilityCurrent,
            item.DurabilityMax,
            item.CraftedBy,
            item.MakerFactionId,
            item.StyleTag,
            ToPayloadImprovements(item.Improvements),
            ToPayloadPerishable(item.Perishable),
            item.SpawnedAtTick);
    }

    private static WorldSaveCreaturePayloadData ToPayloadCreature(CreatureInstance creature)
    {
        return new WorldSaveCreaturePayloadData(
            creature.Guid,
            creature.DefinitionId,
            creature.FactionId,
            ToPayloadPoint(creature.Position),
            creature.Z,
            creature.HP,
            creature.MaxHP,
            creature.SpawnedAtTick);
    }

    private static WorldSavePlacementData? ToPayloadPlacement(PlacementData? placement)
    {
        if (placement == null)
            return null;

        return new WorldSavePlacementData(
            ToPayloadPoint(placement.AnchorWorld),
            placement.Z,
            placement.Rotation,
            placement.StateId);
    }

    private static WorldSaveItemReservationTokenData[] ToPayloadReservationTokens(
        IEnumerable<ReservationToken> reservations)
    {
        return reservations
            .OrderBy(token => token.JobGuid)
            .ThenBy(token => token.ClaimantCreatureGuid)
            .ThenBy(token => token.ReservationType, StringComparer.Ordinal)
            .Select(token => new WorldSaveItemReservationTokenData(
                token.JobGuid,
                token.ClaimantCreatureGuid,
                token.ReservedCount,
                token.ExpiresAtTick,
                token.ReservationType))
            .ToArray();
    }

    private static WorldSaveItemImprovementData[]? ToPayloadImprovements(IReadOnlyList<Improvement>? improvements)
    {
        if (improvements == null)
            return null;

        return improvements
            .OrderBy(improvement => improvement.Type, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.MaterialId, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.QualityTier)
            .ThenBy(improvement => improvement.CreatedBy)
            .ThenBy(improvement => improvement.Description, StringComparer.Ordinal)
            .Select(improvement => new WorldSaveItemImprovementData(
                improvement.Type,
                improvement.MaterialId,
                improvement.QualityTier,
                improvement.CreatedBy,
                improvement.Description))
            .ToArray();
    }

    private static WorldSaveItemPerishableData? ToPayloadPerishable(PerishableState? perishable)
    {
        if (perishable == null)
            return null;

        return new WorldSaveItemPerishableData(
            perishable.CreatedAtTick,
            perishable.FreshDurationTicks,
            perishable.SpoilDurationTicks,
            perishable.CurrentFreshness);
    }
}
