namespace HumanFortress.App.Input;

internal sealed class FortressInputRuntimePorts
{
    internal FortressInputRuntimePorts(FortressInputRuntimePortDependencies dependencies)
    {
        Keyboard = new FortressKeyboardRuntimePorts(
            dependencies.BuildCatalog,
            dependencies.WorkshopQueries,
            dependencies.WorkshopCommands,
            dependencies.NavigationDebug,
            dependencies.SimulationControl);
        Map = new FortressMapRuntimePorts(
            new FortressPlacementRuntimePorts(
                dependencies.PlacementQueries,
                dependencies.PlacementCommands),
            dependencies.DebugSpawnQueries,
            dependencies.DebugSpawnCommands,
            dependencies.MapInspection);
    }

    internal FortressKeyboardRuntimePorts Keyboard { get; }

    internal FortressMapRuntimePorts Map { get; }
}
