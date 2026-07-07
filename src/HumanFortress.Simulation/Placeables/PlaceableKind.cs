namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Placeable kind (Installable from item, or Construction built on-site)
/// </summary>
internal enum PlaceableKind
{
    /// <summary>
    /// Installable from item (has source_item_* fields, preserves quality/material/decorations)
    /// </summary>
    Installable,

    /// <summary>
    /// Built on-site from construction definition (no source item, quality always 0)
    /// </summary>
    Construction
}
