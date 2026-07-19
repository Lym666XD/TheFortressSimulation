using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Identity;

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
    private readonly LiveEntityIdentityIndex _identityIndex = new();
    private readonly Dictionary<uint, List<Guid>> _legacyEntityIdIndex = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;

    // Position index for fast per-tile queries
    private readonly Dictionary<(int X, int Y, int Z), List<Guid>> _posIndex = new();

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;
    private IDiagnosticSink _diagnostics;

    internal ItemManager(IDiagnosticSink? diagnostics = null)
    {
        _diagnostics = diagnostics ?? DiagnosticHub.Sink;
    }

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

    internal LiveEntityIdentityAuthoritySnapshot GetIdentityAuthoritySnapshot()
    {
        lock (_instanceLock)
        {
            return _identityIndex.GetAuthoritySnapshot(_nextInstanceSequence);
        }
    }

    private Guid CreateNextInstanceGuidLocked()
    {
        Guid guid;
        EntityIdentityClaimResult validation;
        do
        {
            guid = DeterministicGuidGenerator.GenerateFromSequence(ItemInstanceGuidScope, ++_nextInstanceSequence);
            validation = _identityIndex.ValidateNew(guid);
        }
        while (!validation.Success);

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

    internal void SetDiagnostics(IDiagnosticSink diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    private void Emit(string message)
    {
        SimulationDiagnostics.Information(_diagnostics, "Simulation.Items", message);
    }
}
