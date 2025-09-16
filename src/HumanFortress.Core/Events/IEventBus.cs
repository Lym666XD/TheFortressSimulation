namespace HumanFortress.Core.Events;

/// <summary>
/// Event bus for post-commit game events per GAME_ARCHITECTURE.md.
/// Events are fired after write phase completes.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish an event to all subscribers.
    /// </summary>
    void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent;

    /// <summary>
    /// Subscribe to events of a specific type.
    /// </summary>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;

    /// <summary>
    /// Unsubscribe from events.
    /// </summary>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
}

/// <summary>
/// Base interface for all game events.
/// </summary>
public interface IGameEvent
{
    /// <summary>
    /// Tick when the event occurred.
    /// </summary>
    ulong Tick { get; }

    /// <summary>
    /// Event type for debugging.
    /// </summary>
    string EventType { get; }
}