using HumanFortress.Simulation.Identity;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<CreatureInstance> Instances,
        LiveEntityIdentityAuthoritySnapshot Identity);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_instanceLock)
        {
            return new MutationMemento(
                _instances.Values
                    .OrderBy(static creature => creature.Guid)
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
            foreach (var creature in memento.Instances
                .OrderBy(static creature => creature.Guid)
                .Select(CloneForMutationMemento))
            {
                _instances.Add(creature.Guid, creature);
                LegacyEntityIdIndexAdd(creature.Guid);
            }

            _identityIndex.RestoreAuthoritySnapshot(
                memento.Identity,
                _instances.Keys);
            _nextInstanceSequence = memento.Identity.NextAllocationSequence;
        }
    }

    private static CreatureInstance CloneForMutationMemento(CreatureInstance creature)
    {
        return new CreatureInstance(
            creature.Guid,
            creature.DefinitionId,
            creature.FactionId,
            creature.Position,
            creature.Z,
            creature.MaxHP,
            creature.SpawnedAtTick)
        {
            HP = creature.HP,
            MaxHP = creature.MaxHP
        };
    }
}
