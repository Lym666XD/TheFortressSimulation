namespace HumanFortress.Runtime.Save;

internal readonly record struct RuntimeSaveManifestSectionDefinition(
    string Name,
    bool RequiredForFortressMode);

internal static class RuntimeSaveManifestSections
{
    internal const string World = "world";
    internal const string WorldTerrain = "world.terrain";
    internal const string WorldItems = "world.items";
    internal const string WorldCreatures = "world.creatures";
    internal const string WorldReservations = "world.reservations";
    internal const string WorldStockpiles = "world.stockpiles";
    internal const string WorldPlaceables = "world.placeables";
    internal const string WorldOrders = "world.orders";
    internal const string Rng = "rng";
    internal const string CommandsExecuted = "commands.executed";
    internal const string CommandsPending = "commands.pending";
    internal const string JobsTransport = "jobs.transport";
    internal const string JobsMining = "jobs.mining";
    internal const string JobsCraft = "jobs.craft";

    private static readonly IReadOnlyList<RuntimeSaveManifestSectionDefinition> Definitions =
        Array.AsReadOnly(new[]
        {
            new RuntimeSaveManifestSectionDefinition(World, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldTerrain, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldItems, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldCreatures, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldReservations, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldStockpiles, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldPlaceables, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(WorldOrders, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(Rng, RequiredForFortressMode: true),
            new RuntimeSaveManifestSectionDefinition(CommandsExecuted, RequiredForFortressMode: false),
            new RuntimeSaveManifestSectionDefinition(CommandsPending, RequiredForFortressMode: false),
            new RuntimeSaveManifestSectionDefinition(JobsTransport, RequiredForFortressMode: false),
            new RuntimeSaveManifestSectionDefinition(JobsMining, RequiredForFortressMode: false),
            new RuntimeSaveManifestSectionDefinition(JobsCraft, RequiredForFortressMode: false)
        });

    internal static IReadOnlyList<RuntimeSaveManifestSectionDefinition> All => Definitions;

    internal static IEnumerable<string> OrderedNames =>
        Definitions
            .Select(static section => section.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

    internal static bool TryGetRequirement(string name, out bool requiredForFortressMode)
    {
        foreach (var definition in Definitions)
        {
            if (string.Equals(definition.Name, name, StringComparison.Ordinal))
            {
                requiredForFortressMode = definition.RequiredForFortressMode;
                return true;
            }
        }

        requiredForFortressMode = false;
        return false;
    }
}
