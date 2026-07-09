using HumanFortress.Contracts.Simulation.Creatures;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Diagnostics;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Global manager for creature definitions and runtime instances.
/// Thread-safe for concurrent reads; writes use locks.
/// Follows data-driven principles per CREATURE_SPEC.md and UPDATE_ORDER.md
/// </summary>
internal sealed partial class CreatureManager : ICreatureDefinitionCatalog
{
    private const ulong CreatureInstanceGuidScope = 0x4352454154555245UL;

    private CreatureDefinitionCatalogStore _definitionCatalog = CreatureDefinitionCatalogStore.Empty;

    private readonly Dictionary<Guid, CreatureInstance> _instances = new();
    private readonly Dictionary<ulong, Guid> _entityKeyIndex = new();
    private readonly Dictionary<uint, List<Guid>> _legacyEntityIdIndex = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;

    private SimulationWorld? _world;

    /// <summary>
    /// Optional logging callback set by the app diagnostics layer.
    /// </summary>
    public static Action<string>? LogCallback { get; set; }

    public int DefinitionCount => _definitionCatalog.DefinitionCount;

    public int InstanceCount
    {
        get
        {
            lock (_instanceLock)
            {
                return _instances.Count;
            }
        }
    }

    /// <summary>
    /// Set world reference (called after World is created)
    /// </summary>
    public void SetWorld(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Creatures", message);
    }

    private Guid CreateNextInstanceGuidLocked()
    {
        Guid guid;
        do
        {
            guid = DeterministicGuidGenerator.GenerateFromSequence(CreatureInstanceGuidScope, ++_nextInstanceSequence);
        }
        while (_instances.ContainsKey(guid));

        return guid;
    }
}
