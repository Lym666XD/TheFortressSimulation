using HumanFortress.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Immutable handle for one composed simulation session.
/// </summary>
public sealed class SimulationRuntimeSession<THost>
    where THost : class
{
    public SimulationRuntimeSession(World world, NavigationManager navigation, THost host)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public World World { get; }
    public NavigationManager Navigation { get; }
    public THost Host { get; }
}
