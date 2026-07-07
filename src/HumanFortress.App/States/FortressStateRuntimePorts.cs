using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;

namespace HumanFortress.App.States;

internal sealed class FortressStateRuntimePorts
{
    internal FortressStateRuntimePorts(
        FortressViewRuntimePorts view,
        FortressInputRuntimePorts input,
        FortressSessionRuntimePorts session)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal FortressViewRuntimePorts View { get; }

    internal FortressInputRuntimePorts Input { get; }

    internal FortressSessionRuntimePorts Session { get; }
}
