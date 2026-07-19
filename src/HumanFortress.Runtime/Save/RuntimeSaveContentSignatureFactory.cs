using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveContentSignatureFactory
{
    internal static RuntimeSaveContentSignatureData FromRuntimeContent(
        FortressRuntimeContentSnapshot? content)
    {
        if (content == null)
            return RuntimeSaveContentSignatureData.Unavailable;

        return new RuntimeSaveContentSignatureData(
            HasContent: true,
            ContentVersion: content.ContentVersion.ToString(),
            ContentHash: content.ContentHash,
            MaterialContentHash: content.Materials.ContentHash,
            MaterialCount: content.Materials.GetNameToIdSnapshot().Count,
            TerrainKindCount: content.TerrainKinds.GetAllKinds().Count(),
            ConstructionCount: content.Constructions.Count,
            RecipeCount: content.Recipes.Count,
            GeologyCount: content.GeologyEntries.Count,
            ZoneCount: content.ZonesById.Count);
    }
}
