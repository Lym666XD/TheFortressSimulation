namespace HumanFortress.Runtime.Session;

internal static class SimulationRuntimeSessionNavigation
{
    internal static void RebuildAll<THost>(SimulationRuntimeSession<THost>? session)
        where THost : class
    {
        session?.Navigation.RebuildAll();
    }
}
