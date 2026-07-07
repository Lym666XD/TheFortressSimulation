namespace HumanFortress.App.GameStates;

/// <summary>
/// Game states per GAME_STATE_FLOW.md.
/// </summary>
internal enum GameStateType
{
    Boot,
    MainMenu,
    WorldGen,
    LoadSave,
    WorldMap,
    EmbarkPrep,
    FortressPlay,
    PauseMenu
}

/// <summary>
/// Base class for game states.
/// </summary>
internal abstract class GameState
{
    internal abstract GameStateType Type { get; }

    /// <summary>
    /// Called when entering this state.
    /// </summary>
    internal virtual void Enter() { }

    /// <summary>
    /// Called when exiting this state.
    /// </summary>
    internal virtual void Exit() { }

}
