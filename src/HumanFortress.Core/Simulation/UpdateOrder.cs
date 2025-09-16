namespace HumanFortress.Core.Simulation;

/// <summary>
/// Defines the authoritative update order for simulation systems per UPDATE_ORDER.md.
/// All systems execute in Read phase (parallel), then Write phase (serialized).
/// </summary>
public static class UpdateOrder
{
    /// <summary>
    /// System priorities for execution order. Lower values execute first.
    /// </summary>
    public static class Priority
    {
        // Read Phase (can be parallel)
        public const int PathfindingPropose = 100;
        public const int JobPropose = 200;
        public const int AIPlanning = 300;
        public const int FieldDecay = 400;
        public const int FluidPreCompute = 500;

        // Write Phase (must be serialized)
        public const int WorldTerrain = 1000;
        public const int Fluids = 1100;
        public const int Fields = 1200;
        public const int Furniture = 1300;
        public const int Items = 1400;
        public const int Creatures = 1500;
        public const int Jobs = 1600;
        public const int Combat = 1700;
        public const int Storyteller = 1800;
        public const int UI = 1900;
    }

    /// <summary>
    /// System identifiers for debugging and error reporting.
    /// </summary>
    public static class SystemId
    {
        public const string Pathfinding = "Navigation.Pathfinding";
        public const string JobScheduler = "Jobs.Scheduler";
        public const string AI = "AI.Planning";
        public const string Fields = "World.Fields";
        public const string Fluids = "World.Fluids";
        public const string Terrain = "World.Terrain";
        public const string Furniture = "World.Furniture";
        public const string Items = "World.Items";
        public const string Creatures = "Sim.Creatures";
        public const string Combat = "Sim.Combat";
        public const string Storyteller = "Sim.Storyteller";
        public const string UI = "UI.Update";
    }
}