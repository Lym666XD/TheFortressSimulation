using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Jobs;
using HumanFortress.Contracts.Simulation.Creatures;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Profession;

internal sealed class ProfessionAssignments
{
    private readonly IProfessionRegistry _registry;
    private readonly ICreatureDefinitionCatalog? _creatureDefinitions;
    private readonly Dictionary<Guid, Dictionary<string, int>> _weights = new();
    private readonly Dictionary<Guid, Dictionary<string, int>> _skillLevels = new();

    internal ProfessionAssignments(
        IProfessionRegistry registry,
        ICreatureDefinitionCatalog? creatureDefinitions = null)
    {
        _registry = registry;
        _creatureDefinitions = creatureDefinitions;
    }

    internal IProfessionRegistry Registry => _registry;

    internal void Initialize(IEnumerable<CreatureInstance> creatures)
    {
        foreach (var creature in creatures.OrderBy(static creature => creature.Guid))
        {
            EnsureProfile(creature.Guid);
        }
    }

    internal void RecordJobCompletion(Guid worker, string jobTag)
    {
        if (!_skillLevels.TryGetValue(worker, out var dict))
        {
            dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _skillLevels[worker] = dict;
        }

        dict.TryGetValue(jobTag, out var current);
        dict[jobTag] = current + 1;
    }

    internal void SetWeight(Guid workerId, string professionId, int weight)
    {
        var profile = EnsureProfile(workerId);
        profile[professionId] = Math.Clamp(weight, 0, 9);
    }

    internal int GetWeight(Guid workerId, string professionId)
    {
        return _weights.TryGetValue(workerId, out var profile)
            && profile.TryGetValue(professionId, out var weight)
                ? weight
                : 5;
    }

    internal IReadOnlyList<ProfessionRosterEntry> GetRosterSnapshot(HumanFortress.Simulation.World.World? world)
    {
        var list = new List<ProfessionRosterEntry>();
        if (world == null) return list;

        var definitions = _creatureDefinitions ?? world.Creatures;
        foreach (var creature in world.Creatures.GetAllInstances().OrderBy(static creature => creature.Guid))
        {
            var def = definitions.GetDefinition(creature.DefinitionId);
            string name = def?.Name ?? creature.DefinitionId;
            var weights = CreateProfileSnapshot(creature.Guid);
            list.Add(new ProfessionRosterEntry(creature.Guid, name, weights));
        }

        list.Sort((a, b) =>
        {
            int result = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            return result != 0 ? result : a.WorkerId.CompareTo(b.WorkerId);
        });
        return list;
    }

    internal IEnumerable<CreatureInstance> SelectCandidates(
        HumanFortress.Simulation.World.World world,
        string jobTag,
        WorkerSelectionStrategy strategy,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        HumanFortress.Contracts.Navigation.Point3? referencePoint)
    {
        var profDefs = _registry.GetProfessionsForJob(jobTag);
        if (profDefs.Count == 0) profDefs = _registry.Definitions;

        var pool = new List<(CreatureInstance Worker, int Weight)>();
        foreach (var creature in world.Creatures.GetAllInstances())
        {
            if (creature.HP <= 0) continue;
            if (busy.Contains(creature.Guid)) continue;

            var profile = CreateProfileSnapshot(creature.Guid);
            int weight = GetEffectiveWeight(profile, profDefs);
            if (weight <= 0) continue;

            pool.Add((creature, weight));
        }

        if (pool.Count == 0)
        {
            return Array.Empty<CreatureInstance>();
        }

        bool IsIdle(CreatureInstance creature) => !reservations.IsCreatureReserved(creature.Guid, currentTick, out _, out _);

        IOrderedEnumerable<(CreatureInstance Worker, int Weight)> ordered = pool
            .OrderByDescending(pair => pair.Weight);

        ordered = strategy switch
        {
            WorkerSelectionStrategy.IdleFirst => ordered
                .ThenByDescending(pair => IsIdle(pair.Worker) ? 1 : 0)
                .ThenBy(pair => DistanceSq(referencePoint, pair.Worker)),
            WorkerSelectionStrategy.HighestSkill => ordered
                .ThenByDescending(pair => GetSkill(pair.Worker.Guid, jobTag))
                .ThenBy(pair => DistanceSq(referencePoint, pair.Worker)),
            _ => ordered.ThenBy(pair => DistanceSq(referencePoint, pair.Worker))
        };

        return ordered
            .ThenBy(pair => pair.Worker.Guid)
            .Select(pair => pair.Worker)
            .ToList();
    }

    private int GetSkill(Guid worker, string jobTag)
    {
        if (_skillLevels.TryGetValue(worker, out var dict) && dict.TryGetValue(jobTag, out var level))
        {
            return level;
        }

        return 0;
    }

    private static int DistanceSq(HumanFortress.Contracts.Navigation.Point3? reference, CreatureInstance creature)
    {
        if (reference == null) return 0;
        int dx = reference.Value.X - creature.Position.X;
        int dy = reference.Value.Y - creature.Position.Y;
        return dx * dx + dy * dy;
    }

    private Dictionary<string, int> EnsureProfile(Guid workerId)
    {
        if (!_weights.TryGetValue(workerId, out var dict))
        {
            dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in _registry.Definitions)
            {
                dict[def.Id] = 5;
            }

            _weights[workerId] = dict;
        }
        else
        {
            foreach (var def in _registry.Definitions)
            {
                if (!dict.ContainsKey(def.Id))
                {
                    dict[def.Id] = 5;
                }
            }
        }

        return dict;
    }

    internal ProfessionAssignmentsReplaySnapshot GetReplaySnapshot()
    {
        var workerIds = _weights.Keys
            .Concat(_skillLevels.Keys)
            .Distinct()
            .OrderBy(static workerId => workerId)
            .ToArray();
        return new ProfessionAssignmentsReplaySnapshot(
            workerIds
                .Select(workerId => new ProfessionWorkerStateSnapshot(
                    workerId,
                    SnapshotValues(_weights.GetValueOrDefault(workerId)),
                    SnapshotValues(_skillLevels.GetValueOrDefault(workerId))))
                .ToArray());
    }

    internal void RestoreMutationMemento(ProfessionAssignmentsReplaySnapshot snapshot)
    {
        _weights.Clear();
        _skillLevels.Clear();
        foreach (var worker in snapshot.Workers.OrderBy(static worker => worker.WorkerId))
        {
            _weights.Add(
                worker.WorkerId,
                worker.Weights.ToDictionary(
                    static entry => entry.Id,
                    static entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase));
            _skillLevels.Add(
                worker.WorkerId,
                worker.SkillLevels.ToDictionary(
                    static entry => entry.Id,
                    static entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase));
        }
    }

    private Dictionary<string, int> CreateProfileSnapshot(Guid workerId)
    {
        var snapshot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_weights.TryGetValue(workerId, out var existing))
        {
            foreach (var entry in existing
                .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                snapshot[entry.Key] = entry.Value;
            }
        }

        foreach (var definition in _registry.Definitions
            .OrderBy(static definition => definition.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static definition => definition.Id, StringComparer.Ordinal))
        {
            snapshot.TryAdd(definition.Id, 5);
        }

        return snapshot;
    }

    private static IReadOnlyList<ProfessionValueStateSnapshot> SnapshotValues(
        IReadOnlyDictionary<string, int>? values)
    {
        return (values ?? new Dictionary<string, int>())
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => new ProfessionValueStateSnapshot(entry.Key, entry.Value))
            .ToArray();
    }

    private static int GetEffectiveWeight(IReadOnlyDictionary<string, int> profile, IReadOnlyList<ProfessionDefinition> professions)
    {
        int best = 0;
        foreach (var profession in professions)
        {
            if (profile.TryGetValue(profession.Id, out var weight) && weight > best)
            {
                best = weight;
            }
        }

        return best;
    }

    internal readonly record struct ProfessionRosterEntry(Guid WorkerId, string Name, IReadOnlyDictionary<string, int> Weights);
}
