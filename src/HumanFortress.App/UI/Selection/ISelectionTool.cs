using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

public interface ISelectionTool
{
    bool IsActive { get; }
    Selection3D Current { get; }

    bool Begin(Point worldStart, int z);
    void Update(Point worldCurrent);
    Selection3D Complete();
    void Cancel();
    void AdjustZRange(int delta);
    void SetZRangeEnd(int z);

    event Action<Selection3D>? Started;
    event Action<Selection3D>? Changed;
    event Action<Selection3D>? Completed;
    event Action? Canceled;
}
