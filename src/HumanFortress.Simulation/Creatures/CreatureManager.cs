using HumanFortress.Contracts.Simulation.Creatures;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Identity;
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
    private readonly LiveEntityIdentityIndex _identityIndex = new();
    private readonly Dictionary<uint, List<Guid>> _legacyEntityIdIndex = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;

    private SimulationWorld? _world;
    private IDiagnosticSink _diagnostics;

    internal CreatureManager(IDiagnosticSink? diagnostics = null)
    {
        _diagnostics = diagnostics ?? DiagnosticHub.Sink;
    }

    internal int DefinitionCount => _definitionCatalog.DefinitionCount;

    int ICreatureDefinitionCatalog.DefinitionCount => DefinitionCount;

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

    /// <summary>
    /// Set world reference (called after World is created)
    /// </summary>
    internal void SetWorld(SimulationWorld world)
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
        SimulationDiagnostics.Information(_diagnostics, "Simulation.Creatures", message);
    }

    private Guid CreateNextInstanceGuidLocked()
    {
        Guid guid;
        EntityIdentityClaimResult validation;
        do
        {
            guid = DeterministicGuidGenerator.GenerateFromSequence(CreatureInstanceGuidScope, ++_nextInstanceSequence);
            validation = _identityIndex.ValidateNew(guid);
        }
        while (!validation.Success);

        return guid;
    }
}
