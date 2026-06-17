using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.App.Jobs;

public sealed class ProfessionAssignments
{
    private readonly IProfessionRegistry _registry;
    private readonly ICreatureDefinitionCatalog? _creatureDefinitions;
    private readonly Dictionary<Guid, Dictionary<string, int>> _weights = new();
    private readonly Dictionary<Guid, Dictionary<string, int>> _skillLevels = new();

    public ProfessionAssignments(
        IProfessionRegistry registry,
        ICreatureDefinitionCatalog? creatureDefinitions = null)
    {
        _registry = registry;
        _creatureDefinitions = creatureDefinitions;
    }

    public IProfessionRegistry Registry => _registry;

    public void Initialize(IEnumerable<CreatureInstance> creatures)
    {
        foreach (var creature in creatures)
        {
            EnsureProfile(creature.Guid);
        }
    }

    public void RecordJobCompletion(Guid worker, string jobTag)
    {
        if (!_skillLevels.TryGetValue(worker, out var dict))
        {
            dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _skillLevels[worker] = dict;
        }

        dict.TryGetValue(jobTag, out var current);
        dict[jobTag] = current + 1;
    }

    public void SetWeight(Guid workerId, string professionId, int weight)
    {
        var profile = EnsureProfile(workerId);
        profile[professionId] = Math.Clamp(weight, 0, 9);
    }

    public int GetWeight(Guid workerId, string professionId)
    {
        var profile = EnsureProfile(workerId);
        return profile.TryGetValue(professionId, out var weight) ? weight : 5;
    }

    public IReadOnlyList<ProfessionRosterEntry> GetRosterSnapshot(HumanFortress.Simulation.World.World? world)
    {
        var list = new List<ProfessionRosterEntry>();
        if (world == null) return list;

        var definitions = _creatureDefinitions ?? world.Creatures;
        foreach (var creature in world.Creatures.GetAllInstances())
        {
            var def = definitions.GetDefinition(creature.DefinitionId);
            string name = def?.Name ?? creature.DefinitionId;
            var weights = new Dictionary<string, int>(EnsureProfile(creature.Guid), StringComparer.OrdinalIgnoreCase);
            list.Add(new ProfessionRosterEntry(creature.Guid, name, weights));
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return list;
    }

    public IEnumerable<CreatureInstance> SelectCandidates(
        HumanFortress.Simulation.World.World world,
        string jobTag,
        WorkerSelectionStrategy strategy,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        HumanFortress.Navigation.Point3? referencePoint)
    {
        var profDefs = _registry.GetProfessionsForJob(jobTag);
        if (profDefs.Count == 0) profDefs = _registry.Definitions;

        var pool = new List<(CreatureInstance Worker, int Weight)>();
        foreach (var creature in world.Creatures.GetAllInstances())
        {
            if (creature.HP <= 0) continue;
            if (busy.Contains(creature.Guid)) continue;

            var profile = EnsureProfile(creature.Guid);
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

        return ordered.Select(pair => pair.Worker).ToList();
    }

    private int GetSkill(Guid worker, string jobTag)
    {
        if (_skillLevels.TryGetValue(worker, out var dict) && dict.TryGetValue(jobTag, out var level))
        {
            return level;
        }

        return 0;
    }

    private static int DistanceSq(HumanFortress.Navigation.Point3? reference, CreatureInstance creature)
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

    private static int GetEffectiveWeight(Dictionary<string, int> profile, IReadOnlyList<ProfessionDefinition> professions)
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

    public readonly record struct ProfessionRosterEntry(Guid WorkerId, string Name, IReadOnlyDictionary<string, int> Weights);
}
