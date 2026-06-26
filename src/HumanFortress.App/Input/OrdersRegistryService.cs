using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HumanFortress.Content.Loading;

namespace HumanFortress.App.Input;

/// <summary>
/// Loads orders registry to provide display names for orders/tools.
/// File: content/registries/orders.registry.json
/// </summary>
internal sealed class OrdersRegistryService
{
    private static OrdersRegistryService? _instance;
    internal static OrdersRegistryService Instance => _instance ??= new OrdersRegistryService();

    private readonly Dictionary<string, string> _orderNames = new(StringComparer.OrdinalIgnoreCase);

    private OrdersRegistryService() { }

    internal void Load(string baseDir)
    {
        var registryFile = FortressContentLoader.ResolveRegistryFile(baseDir, "orders.registry.json");
        if (registryFile.ResolvedPath == null) return;

        var json = File.ReadAllText(registryFile.ResolvedPath);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("orders", out var orders)) return;
        foreach (var prop in orders.EnumerateObject())
        {
            var id = prop.Name; // e.g., haul
            var name = prop.Value.TryGetProperty("name", out var n) ? (n.GetString() ?? id) : id;
            _orderNames[$"orders.{id}"] = name;
        }
    }

    internal string GetOrderName(string orderId)
    {
        return _orderNames.TryGetValue(orderId, out var name) ? name : orderId;
    }
}
