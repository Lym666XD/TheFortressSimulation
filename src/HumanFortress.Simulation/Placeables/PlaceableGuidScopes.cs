namespace HumanFortress.Simulation.Placeables;

internal static class PlaceableGuidScopes
{
    internal const ulong ConstructionGhost = 0x504C47484F535431UL; // PLGHOST1
    internal const ulong ConstructionSite = 0x504C435349544531UL; // PLCSITE1
    internal const ulong CompletedConstruction = 0x504C434F4D504C31UL; // PLCOMPL1
    internal const ulong InstalledItem = 0x504C494E53544C31UL; // PLINSTL1
    internal const ulong UninstalledItem = 0x554E494E53544954UL; // UNINSTIT
}
