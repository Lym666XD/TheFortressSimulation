using HumanFortress.App.Session;
using HumanFortress.App.States;

namespace HumanFortress.App.GameStates;

internal sealed class FortressPlayGameState : ScreenGameState<FortressState>
{
    private readonly IFortressPlayRuntimeHost _runtimeHost;
    private readonly FortressSessionContext _session;

    internal FortressPlayGameState(
        IFortressPlayRuntimeHost runtimeHost,
        FortressSessionContext session,
        IGameScreenPresenter screenPresenter)
        : base(screenPresenter)
    {
        _runtimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal override GameStateType Type => GameStateType.FortressPlay;

    protected override string ScreenOwnerName => "FortressPlayState";

    protected override FortressState CreateScreen()
    {
        Logger.Log("Entered Fortress Play");

        int fortressSize = FortressSessionSizeRules.Normalize(_session.FortressSize);
        if (!FortressSessionSizeRules.IsValid(_session.FortressSize))
        {
            Logger.Log($"[FortressPlayState] Invalid fortress size {_session.FortressSize}, defaulting to {fortressSize}");
        }

        _runtimeHost.InitializeWorld(fortressSize, 50);
        return new FortressState(_runtimeHost.CreateRuntimeAccess(), _session);
    }
}
