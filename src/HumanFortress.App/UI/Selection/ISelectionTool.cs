using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

internal interface ISelectionTool
{
    bool IsActive { get; }
    Selection3D Current { get; }

    bool Begin(Point worldStart, int z);
    void Update(Point worldCurrent);
    Selection3D Complete();
    void Cancel();
    void AdjustZRange(int delta);
    void SetZRangeEnd(int z);
    void SetWorldBounds(RuntimeWorldBounds worldBounds);

    event Action<Selection3D>? Started;
    event Action<Selection3D>? Changed;
    event Action<Selection3D>? Completed;
    event Action? Canceled;
}
