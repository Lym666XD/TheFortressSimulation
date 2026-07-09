using HumanFortress.Contracts.Simulation.Save;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    internal IReadOnlyList<string> RestoreCreaturesSnapshot(IReadOnlyList<WorldSaveCreaturePayloadData>? creatures)
    {
        var issues = new List<string>();
        if (creatures == null)
        {
            issues.Add("World creature payload is missing.");
            return issues;
        }

        var seen = new HashSet<Guid>();
        for (var i = 0; i < creatures.Count; i++)
        {
            ValidateCreaturePayload(creatures[i], i, seen, issues);
        }

        if (issues.Count > 0)
            return issues;

        lock (_instanceLock)
        {
            _instances.Clear();
            _entityKeyIndex.Clear();
            _legacyEntityIdIndex.Clear();
            foreach (var payload in creatures.OrderBy(creature => creature.Guid))
            {
                var instance = new CreatureInstance(
                    payload.Guid,
                    payload.DefinitionId,
                    payload.FactionId,
                    new Point(payload.Position.X, payload.Position.Y),
                    payload.Z,
                    payload.MaxHP,
                    payload.SpawnedAtTick)
                {
                    HP = payload.HP,
                    MaxHP = payload.MaxHP
                };
                _instances[instance.Guid] = instance;
                EntityKeyIndexAdd(instance.Guid);
            }

            _nextInstanceSequence = (ulong)_instances.Count;
        }

        return Array.Empty<string>();
    }

    private void ValidateCreaturePayload(
        WorldSaveCreaturePayloadData payload,
        int index,
        ISet<Guid> seen,
        ICollection<string> issues)
    {
        var prefix = $"World creature payload[{index}]";

        if (payload.Guid == Guid.Empty)
        {
            issues.Add($"{prefix} has an empty creature guid.");
        }
        else if (!seen.Add(payload.Guid))
        {
            issues.Add($"{prefix} contains duplicate creature guid {payload.Guid}.");
        }

        if (string.IsNullOrWhiteSpace(payload.DefinitionId))
        {
            issues.Add($"{prefix} has a blank definition id.");
        }

        if (string.IsNullOrWhiteSpace(payload.FactionId))
        {
            issues.Add($"{prefix} has a blank faction id.");
        }

        if (payload.MaxHP <= 0)
        {
            issues.Add($"{prefix} has non-positive max HP {payload.MaxHP}.");
        }

        if (payload.HP < 0 || payload.HP > payload.MaxHP)
        {
            issues.Add($"{prefix} HP {payload.HP} is outside 0..MaxHP.");
        }

        if (_world == null)
        {
            issues.Add($"{prefix} cannot validate position because the creature manager has no world.");
        }
        else if (!_world.IsValidPosition(payload.Position.X, payload.Position.Y, payload.Z))
        {
            issues.Add($"{prefix} position ({payload.Position.X},{payload.Position.Y},{payload.Z}) is outside world bounds.");
        }
    }
}
