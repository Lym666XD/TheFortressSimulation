using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Session;

/// <summary>
/// Named handle for the active fortress runtime session.
/// </summary>
internal sealed class FortressRuntimeSession
{
    private readonly SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> _session;

    internal FortressRuntimeSession(SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> Inner => _session;

    internal World World => _session.World;

    internal NavigationManager Navigation => _session.Navigation;

    internal SimulationRuntimeHost<SimulationRuntimeSystems> Host => _session.Host;
}
