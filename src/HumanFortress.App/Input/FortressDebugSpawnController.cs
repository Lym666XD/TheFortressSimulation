using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressDebugSpawnContext(
    UiStore Ui,
    IFortressRuntimeDebugSpawnAccess Runtime,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressDebugSpawnController
{
    public static bool TryHandleMapClick(FortressDebugSpawnContext context, Point worldPos)
    {
        var ui = context.Ui;
        if (!ui.DebugOpen)
            return false;

        var spawn = context.Runtime.GetDebugSpawnData();
        if (!spawn.HasWorld)
            return false;

        Logger.Log($"[DEBUG] Debug menu open, tab={ui.DebugMenuTab}, world=true");

        if (ui.DebugMenuTab == 1)
        {
            Logger.Log($"[DEBUG] Attempting creature spawn: id={ui.DebugSelectedCreature}, pos=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
            Logger.Log($"[DEBUG] Creature definitions count: {spawn.CreatureDefinitions}");

            context.Runtime.QueueCreatureSpawn(
                ui.DebugSelectedCreature,
                worldPos,
                context.CurrentZ,
                "player");
            ui.AddToast($"Spawn queued: {ui.DebugSelectedCreature}", context.UiTick + 100);
            context.Redraw();
            return true;
        }

        if (ui.DebugMenuTab == 2)
        {
            Logger.Log($"[DEBUG] Attempting item spawn: id={ui.DebugSelectedItem}, pos=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
            Logger.Log($"[DEBUG] Item definitions count: {spawn.ItemDefinitions}");

            context.Runtime.QueueItemSpawn(
                ui.DebugSelectedItem,
                worldPos,
                context.CurrentZ,
                quantity: 1);
            ui.AddToast($"Spawn queued: {ui.DebugSelectedItem}", context.UiTick + 100);
            context.Redraw();
            return true;
        }

        return false;
    }
}
