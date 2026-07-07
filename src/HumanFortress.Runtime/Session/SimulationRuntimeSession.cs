using HumanFortress.Navigation.Implementation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Session;

/// <summary>
/// Immutable handle for one composed simulation session.
/// </summary>
internal sealed class SimulationRuntimeSession<THost>
    where THost : class
{
    internal SimulationRuntimeSession(World world, NavigationManager navigation, THost host)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    internal World World { get; }
    internal NavigationManager Navigation { get; }
    internal THost Host { get; }
}
