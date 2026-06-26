using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressMapClickController
{
    public static bool TryHandleWorkshopCellClick(FortressMapClickControllerContext context, Point worldPos)
    {
        var workshop = FindWorkshopAt(context.Workshops, worldPos, context.CurrentZ);
        if (!workshop.HasValue)
            return false;

        var value = workshop.Value;
        context.Ui.OpenWorkshopPanel(value.WorkshopGuid, new Point(value.X, value.Y), value.Z);
        context.Redraw();
        return true;
    }

    private static WorkshopSummaryView? FindWorkshopAt(SimulationWorkshopDebugData workshops, Point worldPos, int z)
    {
        foreach (var workshop in workshops.Workshops)
        {
            if (workshop.Z != z)
                continue;

            if (worldPos.X >= workshop.X
                && worldPos.X < workshop.X + workshop.FootprintW
                && worldPos.Y >= workshop.Y
                && worldPos.Y < workshop.Y + workshop.FootprintD)
            {
                return workshop;
            }
        }

        return null;
    }
}
