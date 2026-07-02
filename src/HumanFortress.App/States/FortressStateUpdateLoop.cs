using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.UI;

namespace HumanFortress.App.States;

internal sealed class FortressStateUpdateLoop
{
    private readonly UiStore _ui;
    private readonly FortressUiTickCounter _uiTicks;
    private readonly FortressStateInputController _inputController;
    private readonly FortressStateInitializer _initializer;
    private readonly Action _ensureFocused;
    private readonly Action _drawUi;
    private bool _initialized;

    internal FortressStateUpdateLoop(
        UiStore ui,
        FortressUiTickCounter uiTicks,
        FortressStateInputController inputController,
        FortressStateInitializer initializer,
        Action ensureFocused,
        Action drawUi)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _uiTicks = uiTicks ?? throw new ArgumentNullException(nameof(uiTicks));
        _inputController = inputController ?? throw new ArgumentNullException(nameof(inputController));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        _ensureFocused = ensureFocused ?? throw new ArgumentNullException(nameof(ensureFocused));
        _drawUi = drawUi ?? throw new ArgumentNullException(nameof(drawUi));
    }

    internal void Update()
    {
        var uiTick = _uiTicks.Advance();

        FortressConstructionHighlightDiagnostics.Log(_ui, uiTick);
        _ensureFocused();

        if (!_initialized)
            _initialized = _initializer.TryInitialize();

        FortressMouseWheelPoller.Poll(_inputController.ApplyMouseWheelInput);
        _ensureFocused();
        _drawUi();
    }
}
