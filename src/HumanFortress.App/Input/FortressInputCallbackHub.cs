using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed class FortressInputCallbackHub
{
    private FortressStateInputController? _controller;

    public void Bind(FortressStateInputController controller)
    {
        if (_controller is not null)
        {
            throw new InvalidOperationException("Fortress input callbacks are already bound.");
        }

        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public bool ApplyMouseHover(Point mapLocal, bool updateSelection, bool logMapEvent)
    {
        return Controller.ApplyMouseHover(mapLocal, updateSelection, logMapEvent);
    }

    public void HideTilePanel()
    {
        Controller.HideTilePanel();
    }

    public void RedrawAfterInput()
    {
        Controller.RedrawAfterInput();
    }

    public void OnMapLeftClicked(Point local)
    {
        Controller.OnMapLeftClicked(local);
    }

    private FortressStateInputController Controller =>
        _controller ?? throw new InvalidOperationException("Fortress input callbacks used before controller binding.");
}
