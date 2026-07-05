using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private bool FillRuntimeWorld(Action<World> fillWorld)
    {
        ArgumentNullException.ThrowIfNull(fillWorld);

        var world = World;
        if (world == null)
            return false;

        _log($"[GenerateFortressMap] World obtained: {world.SizeInChunks}x{world.SizeInChunks} chunks");
        _log($"[GenerateFortressMap] Creature definitions loaded: {world.Creatures.DefinitionCount}");
        _log($"[GenerateFortressMap] Item definitions loaded: {world.Items.DefinitionCount}");

        _log("[GenerateFortressMap] Filling world with terrain data");
        fillWorld(world);
        _log("[GenerateFortressMap] World filled with terrain data");

        _log("[GenerateFortressMap] Rebuilding shared navigation cache");
        RebuildNavigation();

        return true;
    }

    private void RebuildNavigation()
    {
        SimulationRuntimeSessionNavigation.RebuildAll(_runtimeSession);
    }
}
