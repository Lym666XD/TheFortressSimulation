using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.App.Jobs;

public sealed class ProfessionRegistry
{
    public sealed record Definition(string Id, string Name, string[] JobTags, bool IsDefault);

    public IReadOnlyList<Definition> Definitions { get; }
    public Definition DefaultProfession { get; }

    private ProfessionRegistry(IReadOnlyList<Definition> definitions)
    {
        Definitions = definitions;
        DefaultProfession = definitions.FirstOrDefault(d => d.IsDefault) ?? definitions.First();
    }

    public static ProfessionRegistry Load(string baseDir)
    {
        try
        {
            var path = Path.Combine(baseDir, "content", "registries", "professions.json");
            if (!File.Exists(path))
            {
                Logger.Log("[PROFESSIONS] Registry missing; falling back to Laborer-only default.");
                return new ProfessionRegistry(new[]
                {
                    new Definition("laborer", "Laborer", new[] { "hauling", "construction", "support" }, true)
                });
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = new List<Definition>();
            if (doc.RootElement.TryGetProperty("professions", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    string id = el.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    string name = el.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? id) : id;
                    bool isDefault = el.TryGetProperty("default", out var defEl) && defEl.GetBoolean();
                    string[] tags = el.TryGetProperty("job_tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                        ? tagsEl.EnumerateArray().Select(t => t.GetString() ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray()
                        : Array.Empty<string>();
                    list.Add(new Definition(id, name, tags, isDefault));
                }
            }

            if (list.Count == 0)
                list.Add(new Definition("laborer", "Laborer", new[] { "hauling", "construction", "support" }, true));

            return new ProfessionRegistry(list);
        }
        catch (Exception ex)
        {
            Logger.Log($"[PROFESSIONS] Failed to load registry: {ex.Message}");
            return new ProfessionRegistry(new[]
            {
                new Definition("laborer", "Laborer", new[] { "hauling", "construction", "support" }, true)
            });
        }
    }

    public IReadOnlyList<Definition> GetProfessionsForJob(string jobTag)
    {
        if (string.IsNullOrWhiteSpace(jobTag)) return Definitions;
        var matches = Definitions.Where(d => d.JobTags.Contains(jobTag, StringComparer.OrdinalIgnoreCase)).ToList();
        return matches.Count > 0 ? matches : Definitions;
    }
}

public enum WorkerSelectionStrategy
{
    Closest,
    IdleFirst,
    HighestSkill
}

public sealed class ProfessionAssignments
{
    private readonly ProfessionRegistry _registry;
    private readonly ICreatureDefinitionCatalog? _creatureDefinitions;
    private readonly Dictionary<Guid, Dictionary<string, int>> _weights = new();
    private readonly Dictionary<Guid, Dictionary<string, int>> _skillLevels = new();

    public ProfessionAssignments(
        ProfessionRegistry registry,
        ICreatureDefinitionCatalog? creatureDefinitions = null)
    {
        _registry = registry;
        _creatureDefinitions = creatureDefinitions;
    }

    public ProfessionRegistry Registry => _registry;

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

    private int GetSkill(Guid worker, string jobTag)
    {
        if (_skillLevels.TryGetValue(worker, out var dict) && dict.TryGetValue(jobTag, out var level))
            return level;
        return 0;
    }

    public void SetWeight(Guid workerId, string professionId, int weight)
    {
        var profile = EnsureProfile(workerId);
        int clamped = Math.Clamp(weight, 0, 9);
        if (!profile.ContainsKey(professionId))
            profile[professionId] = clamped;
        else
            profile[professionId] = clamped;
    }

    public int GetWeight(Guid workerId, string professionId)
    {
        var profile = EnsureProfile(workerId);
        if (profile.TryGetValue(professionId, out var weight))
            return weight;
        return 5;
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
            return Array.Empty<CreatureInstance>();

        bool IsIdle(CreatureInstance c) => !reservations.IsCreatureReserved(c.Guid, currentTick, out _, out _);

        IOrderedEnumerable<(CreatureInstance Worker, int Weight)> ordered = pool
            .OrderByDescending(p => p.Weight);

        ordered = strategy switch
        {
            WorkerSelectionStrategy.IdleFirst => ordered
                .ThenByDescending(p => IsIdle(p.Worker) ? 1 : 0)
                .ThenBy(p => DistanceSq(referencePoint, p.Worker)),
            WorkerSelectionStrategy.HighestSkill => ordered
                .ThenByDescending(p => GetSkill(p.Worker.Guid, jobTag))
                .ThenBy(p => DistanceSq(referencePoint, p.Worker)),
            _ => ordered.ThenBy(p => DistanceSq(referencePoint, p.Worker))
        };

        return ordered.Select(p => p.Worker).ToList();
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
                dict[def.Id] = 5;
            _weights[workerId] = dict;
        }
        else
        {
            foreach (var def in _registry.Definitions)
                if (!dict.ContainsKey(def.Id))
                    dict[def.Id] = 5;
        }
        return dict;
    }

    private static int GetEffectiveWeight(Dictionary<string, int> profile, IReadOnlyList<ProfessionRegistry.Definition> profs)
    {
        int best = 0;
        foreach (var prof in profs)
        {
            if (profile.TryGetValue(prof.Id, out var w) && w > best)
                best = w;
        }
        return best;
    }

    public readonly record struct ProfessionRosterEntry(Guid WorkerId, string Name, IReadOnlyDictionary<string, int> Weights);
}
