using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HumanFortress.Content.Loading;
using SadConsole.Input;

namespace HumanFortress.App.Input;

/// <summary>
/// Loads and provides access to input bindings from content/registries/input.bindings.json.
/// Minimal v1 used by Orders quick menu rendering and key handling.
/// </summary>
internal sealed class InputBindingsService
{
    private static InputBindingsService? _instance;
    internal static InputBindingsService Instance => _instance ??= new InputBindingsService();

    private readonly Dictionary<string, Dictionary<string, string>> _contexts = new(StringComparer.OrdinalIgnoreCase);

    private InputBindingsService() { }

    internal void Load(string baseDir)
    {
        var registryFile = FortressContentLoader.ResolveRegistryFile(baseDir, "input.bindings.json");
        if (registryFile.ResolvedPath == null) return;

        var json = File.ReadAllText(registryFile.ResolvedPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var presets = root.GetProperty("presets");
        var def = presets.GetProperty("default");
        foreach (var ctxProp in def.EnumerateObject())
        {
            var ctxName = ctxProp.Name; // e.g., global, menu.orders
            if (ctxProp.Value.ValueKind != JsonValueKind.Object) continue;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ctxProp.Value.EnumerateObject())
            {
                map[kv.Name] = kv.Value.GetString() ?? string.Empty;
            }
            _contexts[ctxName] = map;
        }
    }

    internal IReadOnlyDictionary<string, string> GetContext(string name)
    {
        return _contexts.TryGetValue(name, out var map) ? map : new Dictionary<string, string>();
    }

    internal bool IsActionForKey(string context, string keyName, string actionId)
    {
        return _contexts.TryGetValue(context, out var map) && map.TryGetValue(keyName, out var act) && string.Equals(act, actionId, StringComparison.OrdinalIgnoreCase);
    }

    internal bool TryResolveKey(Keys key, out string name)
    {
        name = key switch
        {
            Keys.Z => "Z",
            Keys.X => "X",
            Keys.C => "C",
            Keys.F => "F",
            Keys.F1 => "F1",
            Keys.F2 => "F2",
            Keys.F3 => "F3",
            Keys.F4 => "F4",
            Keys.F5 => "F5",
            Keys.F6 => "F6",
            Keys.F7 => "F7",
            Keys.OemComma => ",",
            Keys.Space => "Space",
            Keys.Escape => "Escape",
            _ => string.Empty,
        };
        return !string.IsNullOrEmpty(name);
    }
}
