using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Diagnostics;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Global manager for item definitions and runtime instances.
/// Thread-safe for concurrent reads; writes use locks.
/// Follows data-driven principles per ITEMS_SPEC.md and UPDATE_ORDER.md
/// </summary>
internal sealed partial class ItemManager : IItemDefinitionCatalog
{
    private const ulong ItemInstanceGuidScope = 0x4954454D53544143UL;

    // Static definition catalog (loaded at startup, swapped as an immutable snapshot on reload)
    private ItemDefinitionCatalogStore _definitionCatalog = ItemDefinitionCatalogStore.Empty;

    // Runtime instances (modified during gameplay)
    private readonly Dictionary<Guid, ItemInstance> _instances = new();
    private readonly Dictionary<ulong, Guid> _entityKeyIndex = new();
    private readonly Dictionary<uint, List<Guid>> _legacyEntityIdIndex = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;

    // Position index for fast per-tile queries
    private readonly Dictionary<(int X, int Y, int Z), List<Guid>> _posIndex = new();

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;

    /// <summary>
    /// Optional logging callback (set by App layer to write to fortress_debug.log)
    /// </summary>
    internal static Action<string>? LogCallback { get; set; }

    internal int DefinitionCount => _definitionCatalog.DefinitionCount;

    int IItemDefinitionCatalog.DefinitionCount => DefinitionCount;

    internal int InstanceCount
    {
        get
        {
            lock (_instanceLock)
            {
                return _instances.Count;
            }
        }
    }

    private Guid CreateNextInstanceGuidLocked()
    {
        Guid guid;
        do
        {
            guid = DeterministicGuidGenerator.GenerateFromSequence(ItemInstanceGuidScope, ++_nextInstanceSequence);
        }
        while (_instances.ContainsKey(guid));

        return guid;
    }

    /// <summary>
    /// Set dependencies (called after initialization)
    /// </summary>
    internal void SetDependencies(HumanFortress.Simulation.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Items", message);
    }
}
