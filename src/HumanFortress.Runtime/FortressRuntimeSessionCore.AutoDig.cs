namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionBootstrapPort.EnqueueStartupAutoDig(int currentZ)
    {
        var world = World;
        if (world == null)
            return;

        RuntimeAutoDigSeeder.EnqueueAfterWorldFill(world, currentZ, EnqueueCurrentTickCommand, _log);
    }
}
