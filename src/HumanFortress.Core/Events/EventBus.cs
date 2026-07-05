using System.Collections.Concurrent;
using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Core.Events;

/// <summary>
/// Thread-safe event bus implementation.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
    {
        if (gameEvent == null)
            throw new ArgumentNullException(nameof(gameEvent));

        var eventType = typeof(TEvent);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            List<Delegate> handlersCopy;
            lock (_lock)
            {
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
                    DiagnosticHub.Error(
                        "Core.EventBus",
                        $"[ERROR] Event handler failed for {gameEvent.EventType}: {ex.Message}",
                        ex);
                }
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
            _handlers.AddOrUpdate(eventType,
                new List<Delegate> { handler },
                (_, list) =>
                {
                    list.Add(handler);
                    return list;
                });
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
                    _handlers.TryRemove(eventType, out _);
                }
            }
        }
    }
}
