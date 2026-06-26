using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI
{
    internal static partial class DebugLayoutCalculator
    {
        private static readonly CategoryOption[] CategoryOptions =
        {
            new(DebugItemCategory.Boulders, "Boulders"),
            new(DebugItemCategory.Blocks, "Blocks"),
            new(DebugItemCategory.Logs, "Logs"),
            new(DebugItemCategory.Planks, "Planks"),
            new(DebugItemCategory.Tools, "Tools"),
            new(DebugItemCategory.Weapons, "Weapons"),
            new(DebugItemCategory.Ammo, "Ammo"),
            new(DebugItemCategory.SiegeWeapons, "Siege")
        };

        /// <summary>
        /// Category display labels in order. Shared by renderer and hit-testing to avoid drift.
        /// </summary>
        public static string[] GetCategoryLabels()
        {
            var labels = new string[CategoryOptions.Length];
            for (int i = 0; i < CategoryOptions.Length; i++)
                labels[i] = CategoryOptions[i].Label;
            return labels;
        }

        public static IReadOnlyList<CategoryOption> GetCategoryOptions() => CategoryOptions;

        public static bool TryGetCategoryByIndex(int index, out DebugItemCategory category)
        {
            if (index < 0 || index >= CategoryOptions.Length)
            {
                category = default;
                return false;
            }

            category = CategoryOptions[index].Category;
            return true;
        }
    }
}
