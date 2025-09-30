using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HumanFortress.App.Input;

/// <summary>
/// Loads orders registry to provide display names for orders/tools.
/// File: content/registries/orders.registry.json
/// </summary>
public sealed class OrdersRegistryService
{
    private static OrdersRegistryService? _instance;
    public static OrdersRegistryService Instance => _instance ??= new OrdersRegistryService();

    private readonly Dictionary<string, string> _orderNames = new(StringComparer.OrdinalIgnoreCase);

    private OrdersRegistryService() { }

    public void Load(string baseDir)
    {
        var path = Path.Combine(baseDir, "content", "registries", "orders.registry.json");
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("orders", out var orders)) return;
        foreach (var prop in orders.EnumerateObject())
        {
            var id = prop.Name; // e.g., haul
            var name = prop.Value.TryGetProperty("name", out var n) ? (n.GetString() ?? id) : id;
            _orderNames[$"orders.{id}"] = name;
        }
    }

    public string GetOrderName(string orderId)
    {
        return _orderNames.TryGetValue(orderId, out var name) ? name : orderId;
    }
}

