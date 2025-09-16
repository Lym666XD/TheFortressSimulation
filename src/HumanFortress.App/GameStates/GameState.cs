namespace HumanFortress.App.GameStates;

/// <summary>
/// Game states per GAME_STATE_FLOW.md.
/// </summary>
public enum GameStateType
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
public abstract class GameState
{
    public abstract GameStateType Type { get; }

    /// <summary>
    /// Called when entering this state.
    /// </summary>
    public virtual void Enter() { }

    /// <summary>
    /// Called when exiting this state.
    /// </summary>
    public virtual void Exit() { }

    /// <summary>
    /// Update the state (called each frame).
    /// </summary>
    public virtual void Update(double deltaTime) { }

    /// <summary>
    /// Render the state.
    /// </summary>
    public virtual void Render() { }

    /// <summary>
    /// Handle input for this state.
    /// </summary>
    public virtual void HandleInput() { }
}