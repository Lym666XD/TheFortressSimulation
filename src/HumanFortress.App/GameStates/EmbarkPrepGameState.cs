using HumanFortress.App.Session;
using HumanFortress.App.States;

namespace HumanFortress.App.GameStates;

internal sealed class EmbarkPrepGameState : ScreenGameState<EmbarkPrepState>
{
    private readonly IAppStateNavigator _navigator;
    private readonly FortressSessionContext _session;

    internal EmbarkPrepGameState(
        IAppStateNavigator navigator,
        FortressSessionContext session,
        IGameScreenPresenter screenPresenter)
        : base(screenPresenter)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal override GameStateType Type => GameStateType.EmbarkPrep;

    protected override EmbarkPrepState CreateScreen()
    {
        return new EmbarkPrepState(_navigator, _session);
    }
}
