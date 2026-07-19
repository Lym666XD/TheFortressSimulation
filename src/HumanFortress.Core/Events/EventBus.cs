using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Core.Events;

/// <summary>
/// Thread-safe event bus implementation.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();
    private readonly IDiagnosticSink? _diagnostics;

    public EventBus(IDiagnosticSink? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
    {
        if (gameEvent == null)
            throw new ArgumentNullException(nameof(gameEvent));

        var eventType = typeof(TEvent);
        List<Delegate> handlersCopy;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
                return;

            handlersCopy = new List<Delegate>(handlers);
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                ((Action<TEvent>)handler)(gameEvent);
            }
            catch (Exception ex)
            {
                // Per ERROR_HANDLING_POLICY.md: catch and continue
                Diagnostics.Error(
                    "Core.EventBus",
                    $"[ERROR] Event handler failed for {gameEvent.EventType}: {ex.Message}",
                    ex);
            }
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _handlers[eventType] = handlers;
            }

            handlers.Add(handler);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(eventType);
                }
            }
        }
    }

    private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink;
}
