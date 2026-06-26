namespace HumanFortress.App.UI;

internal static partial class WorkshopCategoryMapper
{
    private static Dictionary<string, string[]> GetFallback()
    {
        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "mining", new[] { "mining", "smelting", "fuel", "lime", "concrete" } },
            { "lumbering", new[] { "logging" } },
            { "farming", new[] { "farming", "butchery", "pasture", "tanning", "kitchen", "compost" } },
            { "industry", new[] { "stoneworks", "woodworking", "metalworks", "glass", "pottery", "chemistry", "paper", "tailoring" } },
            { "crafts", new[] { "crafts", "precision", "firearms", "alchemy", "enchanting" } }
        };
    }
}
