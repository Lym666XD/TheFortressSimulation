using HumanFortress.Simulation.Identity;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<ItemInstance> Instances,
        LiveEntityIdentityAuthoritySnapshot Identity);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_instanceLock)
        {
            return new MutationMemento(
                _instances.Values
                    .OrderBy(static item => item.Guid)
                    .Select(CloneForMutationMemento)
                    .ToArray(),
                _identityIndex.GetAuthoritySnapshot(_nextInstanceSequence));
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_instanceLock)
        {
            _instances.Clear();
            _legacyEntityIdIndex.Clear();
            _posIndex.Clear();

            foreach (var item in memento.Instances
                .OrderBy(static item => item.Guid)
                .Select(CloneForMutationMemento))
            {
                _instances.Add(item.Guid, item);
                LegacyEntityIdIndexAdd(item.Guid);
                IndexAdd(item.Guid, item.Position, item.Z);
            }

            _identityIndex.RestoreAuthoritySnapshot(
                memento.Identity,
                _instances.Keys);
            _nextInstanceSequence = memento.Identity.NextAllocationSequence;
        }
    }

    private static ItemInstance CloneForMutationMemento(ItemInstance item)
    {
        return new ItemInstance(
            item.Guid,
            item.DefinitionId,
            item.Position,
            item.Z,
            item.StackCount,
            item.SpawnedAtTick)
        {
            MaterialId = item.MaterialId,
            ContainedBy = item.ContainedBy,
            CarriedBy = item.CarriedBy,
            EquippedBy = item.EquippedBy,
            InstalledAt = item.InstalledAt == null
                ? null
                : new PlacementData
                {
                    AnchorWorld = item.InstalledAt.AnchorWorld,
                    Z = item.InstalledAt.Z,
                    Rotation = item.InstalledAt.Rotation,
                    StateId = item.InstalledAt.StateId
                },
            OwnerFactionId = item.OwnerFactionId,
            OwnerCreatureGuid = item.OwnerCreatureGuid,
            UsePolicy = item.UsePolicy,
            Forbidden = item.Forbidden,
            ReservationTokens = item.ReservationTokens
                .Select(static token => new ReservationToken
                {
                    JobGuid = token.JobGuid,
                    ClaimantCreatureGuid = token.ClaimantCreatureGuid,
                    ReservedCount = token.ReservedCount,
                    ExpiresAtTick = token.ExpiresAtTick,
                    ReservationType = token.ReservationType
                })
                .ToList(),
            QualityTier = item.QualityTier,
            Artifact = item.Artifact,
            ArtifactName = item.ArtifactName,
            ConditionState = item.ConditionState,
            DurabilityCurrent = item.DurabilityCurrent,
            DurabilityMax = item.DurabilityMax,
            CraftedBy = item.CraftedBy,
            MakerFactionId = item.MakerFactionId,
            StyleTag = item.StyleTag,
            Improvements = CloneImprovements(item.Improvements),
            Perishable = ClonePerishable(item.Perishable)
        };
    }
}
