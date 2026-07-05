using HumanFortress.App.Runtime;

namespace HumanFortress.App.Rendering;

internal sealed class FortressViewRuntimePorts
{
    internal FortressViewRuntimePorts(
        IFortressRuntimeReadAccess read,
        IFortressRuntimeUiInputAccess uiInput)
    {
        Read = new FortressViewReadRuntimePorts(read);
        UiInput = new FortressViewUiInputRuntimePorts(uiInput);
    }

    internal FortressViewReadRuntimePorts Read { get; }

    internal FortressViewUiInputRuntimePorts UiInput { get; }
}
