using HumanFortress.App.Session;
using HumanFortress.App.States;
using HumanFortress.App.WorldGeneration;

namespace HumanFortress.App.GameStates;

internal sealed class WorldGenGameState : ScreenGameState<WorldGenState>
{
    private readonly IAppStateNavigator _navigator;
    private readonly FortressSessionContext _session;

    internal WorldGenGameState(
        IAppStateNavigator navigator,
        FortressSessionContext session,
        IGameScreenPresenter screenPresenter)
        : base(screenPresenter)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal override GameStateType Type => GameStateType.WorldGen;

    protected override WorldGenState CreateScreen()
    {
        return new WorldGenState(_navigator, _session, WorldGenerationServiceProvider.Create());
    }
}
