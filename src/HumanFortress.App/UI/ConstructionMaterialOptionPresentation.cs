using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal static class ConstructionMaterialOptionPresentation
{
    private static readonly string[] ShortcutLabels = { "Z", "X", "C", "V", "F" };

    internal static IReadOnlyList<ConstructionMaterialOptionView> GetOptions(
        SimulationBuildCatalogData catalog,
        UiConstructionShape shape)
    {
        var runtimeShape = shape switch
        {
            UiConstructionShape.Wall => RuntimeConstructionShape.Wall,
            UiConstructionShape.Floor => RuntimeConstructionShape.Floor,
            UiConstructionShape.Ramp => RuntimeConstructionShape.Ramp,
            UiConstructionShape.Stairs => RuntimeConstructionShape.Stairs,
            _ => RuntimeConstructionShape.Wall
        };

        return catalog.ConstructionMaterialOptions?.Where(option => option.Shape == runtimeShape)
            .OrderBy(static option => option.Id, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<ConstructionMaterialOptionView>();
    }

    internal static string GetShortcutLabel(int optionIndex)
    {
        return optionIndex >= 0 && optionIndex < ShortcutLabels.Length
            ? ShortcutLabels[optionIndex]
            : "?";
    }
}
