using System.Collections.Generic;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Static definition of an item type loaded from data/core/items/*.json
/// Based on ITEMS_SPEC.md v3
/// </summary>
public sealed class ItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "resource"; // resource/weapon/armor/tool/container/consumable
    public List<string> Tags { get; set; } = new();
    public string? FixedMaterial { get; set; }
    public float BaseMassG { get; set; }
    public float BaseVolumeML { get; set; }
    public float ValueBase { get; set; }

    // Stack info
    public StackMode StackMode { get; set; } = StackMode.None;
    public int MaxPerStack { get; set; } = 1;
}

public enum StackMode
{
    None,
    Count,
    Charges
}