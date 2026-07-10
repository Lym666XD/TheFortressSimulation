using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveContentCatalogSummaryFactory
{
    internal static RuntimeSaveContentCatalogSummaryData FromRuntimeContent(
        FortressRuntimeContentSnapshot? content)
    {
        if (content == null)
            return RuntimeSaveContentCatalogSummaryData.Unavailable;

        return new RuntimeSaveContentCatalogSummaryData(
            HasCatalog: true,
            MaterialNames: content.Materials.GetNameToIdSnapshot()
                .Select(static pair => pair.Key)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray(),
            TerrainKindNames: content.TerrainKinds.GetAllKinds()
                .Select(static kind => kind.Name)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray(),
            ConstructionIds: content.Constructions.GetAllConstructions()
                .Select(static definition => definition.Id)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray(),
            RecipeIds: content.Recipes.GetAllRecipes()
                .Select(static definition => definition.Id)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray(),
            GeologyIds: content.GeologyEntries
                .Select(static pair => pair.Key)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray(),
            ZoneIds: content.ZonesById
                .Select(static pair => pair.Key)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray());
    }
}
