using HumanFortress.App.Commands;
using HumanFortress.App.UI;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressDebugSpawnContext(
    UiStore Ui,
    FortressRuntimeAccess Runtime,
    FortressLoadedSessionSnapshot LoadedSession,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressDebugSpawnController
{
    public static bool TryHandleMapClick(FortressDebugSpawnContext context, Point worldPos)
    {
        var ui = context.Ui;
        if (!ui.DebugOpen || context.LoadedSession.World is not { } world)
            return false;

        Logger.Log($"[DEBUG] Debug menu open, tab={ui.DebugMenuTab}, world=true");

        if (ui.DebugMenuTab == 1)
        {
            Logger.Log($"[DEBUG] Attempting creature spawn: id={ui.DebugSelectedCreature}, pos=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
            Logger.Log($"[DEBUG] Creature definitions count: {world.Creatures.DefinitionCount}");

            context.Runtime.EnqueueCurrentTickCommand(tick => new SpawnCreatureCommand(
                tick,
                ui.DebugSelectedCreature,
                worldPos,
                context.CurrentZ,
                "player"));
            ui.AddToast($"Spawn queued: {ui.DebugSelectedCreature}", context.UiTick + 100);
            context.Redraw();
            return true;
        }

        if (ui.DebugMenuTab == 2)
        {
            Logger.Log($"[DEBUG] Attempting item spawn: id={ui.DebugSelectedItem}, pos=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
            Logger.Log($"[DEBUG] Item definitions count: {world.Items.DefinitionCount}");

            context.Runtime.EnqueueCurrentTickCommand(tick => new SpawnItemCommand(
                tick,
                ui.DebugSelectedItem,
                worldPos,
                context.CurrentZ,
                quantity: 1));
            ui.AddToast($"Spawn queued: {ui.DebugSelectedItem}", context.UiTick + 100);
            context.Redraw();
            return true;
        }

        return false;
    }
}
