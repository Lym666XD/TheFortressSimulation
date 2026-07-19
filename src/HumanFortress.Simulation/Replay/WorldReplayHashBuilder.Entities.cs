using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Identity;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static string BuildItemsHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.items.snapshot.v2");
            AddItemsHash(hash, world.Items.GetAllInstances());
            AddIdentityAuthorityHash(hash, world.Items.GetIdentityAuthoritySnapshot());
        });
    }

    private static string BuildCreaturesHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.creatures.snapshot.v2");
            AddCreaturesHash(hash, world.Creatures.GetAllInstances());
            AddIdentityAuthorityHash(hash, world.Creatures.GetIdentityAuthoritySnapshot());
        });
    }

    private static void AddItemsHash(ReplayHashBuilder hash, IEnumerable<ItemInstance> items)
    {
        var ordered = items
            .OrderBy(item => item.Guid)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var item in ordered)
        {
            hash.AddGuid(item.Guid);
            hash.AddString(item.DefinitionId);
            hash.AddNullableString(item.MaterialId);
            hash.AddInt32(item.StackCount);
            AddPointHash(hash, item.Position);
            hash.AddInt32(item.Z);
            AddNullableGuid(hash, item.ContainedBy);
            AddNullableGuid(hash, item.CarriedBy);
            AddNullableGuid(hash, item.EquippedBy);
            AddPlacementHash(hash, item.InstalledAt);
            hash.AddNullableString(item.OwnerFactionId);
            AddNullableGuid(hash, item.OwnerCreatureGuid);
            hash.AddInt32((int)item.UsePolicy);
            hash.AddBoolean(item.Forbidden);
            hash.AddInt32(item.QualityTier);
            hash.AddBoolean(item.Artifact);
            hash.AddNullableString(item.ArtifactName);
            hash.AddString(item.ConditionState);
            AddNullableInt32(hash, item.DurabilityCurrent);
            AddNullableInt32(hash, item.DurabilityMax);
            AddNullableGuid(hash, item.CraftedBy);
            hash.AddNullableString(item.MakerFactionId);
            hash.AddNullableString(item.StyleTag);
            hash.AddUInt64(item.SpawnedAtTick);
            AddReservationTokensHash(hash, item.ReservationTokens);
            AddImprovementsHash(hash, item.Improvements);
            AddPerishableHash(hash, item.Perishable);
        }
    }

    private static void AddCreaturesHash(ReplayHashBuilder hash, IEnumerable<CreatureInstance> creatures)
    {
        var ordered = creatures
            .OrderBy(creature => creature.Guid)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var creature in ordered)
        {
            hash.AddGuid(creature.Guid);
            hash.AddString(creature.DefinitionId);
            hash.AddString(creature.FactionId);
            AddPointHash(hash, creature.Position);
            hash.AddInt32(creature.Z);
            hash.AddInt32(creature.HP);
            hash.AddInt32(creature.MaxHP);
            hash.AddUInt64(creature.SpawnedAtTick);
        }
    }

    private static void AddIdentityAuthorityHash(
        ReplayHashBuilder hash,
        LiveEntityIdentityAuthoritySnapshot snapshot)
    {
        hash.AddUInt64(snapshot.NextAllocationSequence);
        hash.AddInt32(snapshot.HistoricalBindings.Count);
        foreach (var binding in snapshot.HistoricalBindings)
        {
            hash.AddUInt64(binding.EntityKey);
            hash.AddGuid(binding.OwnerGuid);
        }

        hash.AddInt32(snapshot.RetiredGuids.Count);
        foreach (var retiredGuid in snapshot.RetiredGuids)
            hash.AddGuid(retiredGuid);
    }

    private static void AddReservationTokensHash(ReplayHashBuilder hash, IEnumerable<ReservationToken> reservations)
    {
        var ordered = reservations
            .OrderBy(token => token.JobGuid)
            .ThenBy(token => token.ClaimantCreatureGuid)
            .ThenBy(token => token.ReservationType, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var token in ordered)
        {
            hash.AddGuid(token.JobGuid);
            AddNullableGuid(hash, token.ClaimantCreatureGuid);
            hash.AddInt32(token.ReservedCount);
            hash.AddUInt64(token.ExpiresAtTick);
            hash.AddString(token.ReservationType);
        }
    }

    private static void AddPlacementHash(ReplayHashBuilder hash, PlacementData? placement)
    {
        hash.AddBoolean(placement != null);
        if (placement == null)
            return;

        AddPointHash(hash, placement.AnchorWorld);
        hash.AddInt32(placement.Z);
        hash.AddInt32(placement.Rotation);
        hash.AddNullableString(placement.StateId);
    }

    private static void AddImprovementsHash(ReplayHashBuilder hash, IReadOnlyList<Improvement>? improvements)
    {
        hash.AddBoolean(improvements != null);
        if (improvements == null)
            return;

        var ordered = improvements
            .OrderBy(improvement => improvement.Type, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.MaterialId, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.QualityTier)
            .ThenBy(improvement => improvement.CreatedBy)
            .ThenBy(improvement => improvement.Description, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var improvement in ordered)
        {
            hash.AddString(improvement.Type);
            hash.AddNullableString(improvement.MaterialId);
            hash.AddInt32(improvement.QualityTier);
            AddNullableGuid(hash, improvement.CreatedBy);
            hash.AddNullableString(improvement.Description);
        }
    }

    private static void AddPerishableHash(ReplayHashBuilder hash, PerishableState? perishable)
    {
        hash.AddBoolean(perishable != null);
        if (perishable == null)
            return;

        hash.AddUInt64(perishable.CreatedAtTick);
        hash.AddInt32(perishable.FreshDurationTicks);
        hash.AddInt32(perishable.SpoilDurationTicks);
        hash.AddInt32(BitConverter.SingleToInt32Bits(perishable.CurrentFreshness));
    }
}
