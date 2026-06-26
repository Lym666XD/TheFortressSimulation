using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;

namespace HumanFortress.App.States;

internal sealed partial class FortressState
{
    public override void Update(TimeSpan delta)
    {
        base.Update(delta);
        _uiTick++;

        FortressConstructionHighlightDiagnostics.Log(_ui, _uiTick);
        EnsureFocused();

        if (!_initialized)
        {
            _initialized = _initializer.TryInitialize();
        }

        FortressMouseWheelPoller.Poll(_inputController.ApplyMouseWheelInput);
        EnsureFocused();
        DrawUI();
    }
}
