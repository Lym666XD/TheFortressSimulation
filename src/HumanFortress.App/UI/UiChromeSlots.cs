using SadConsole.Input;

namespace HumanFortress.App.UI;

internal static class UiChromeSlots
{
    private static readonly DockSlot[] DockSlotValues =
    {
        new(Keys.F1, DrawerId.Creature, "F1"),
        new(Keys.F2, DrawerId.Stock, "F2"),
        new(Keys.F3, DrawerId.Work, "F3"),
        new(Keys.F4, DrawerId.PlacementManagement, "F4"),
        new(Keys.F5, DrawerId.Military, "F5"),
        new(Keys.F6, DrawerId.Country, "F6"),
        new(Keys.F7, DrawerId.World, "F7"),
        new(Keys.F8, DrawerId.Log, "F8")
    };

    private static readonly QuickSlot[] QuickSlotValues =
    {
        new(Keys.Z, QuickMenuKind.Orders, "Z"),
        new(Keys.X, QuickMenuKind.Zones, "X"),
        new(Keys.C, QuickMenuKind.Build, "C"),
        new(Keys.V, QuickMenuKind.Stockpile, "V")
    };

    public static IReadOnlyList<DockSlot> DockSlots => DockSlotValues;

    public static IReadOnlyList<QuickSlot> QuickSlots => QuickSlotValues;

    public static bool TryGetDockSlot(int index, out DockSlot slot)
    {
        if (index < 0 || index >= DockSlotValues.Length)
        {
            slot = default;
            return false;
        }

        slot = DockSlotValues[index];
        return true;
    }

    public static bool TryGetQuickSlot(int index, out QuickSlot slot)
    {
        if (index < 0 || index >= QuickSlotValues.Length)
        {
            slot = default;
            return false;
        }

        slot = QuickSlotValues[index];
        return true;
    }

    internal readonly record struct DockSlot(Keys Key, DrawerId Drawer, string Label);

    internal readonly record struct QuickSlot(Keys Key, QuickMenuKind Menu, string Label);
}
