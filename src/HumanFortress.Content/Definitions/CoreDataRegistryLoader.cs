using System;
using System.IO;
using System.Text.Json;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Definitions;

/// <summary>
/// Loads construction/workshop and recipe core data into immutable catalog snapshots.
/// </summary>
internal static partial class CoreDataRegistryLoader
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    internal static CoreDataLoadResult Load(string coreDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreDataPath);

        var constructions = LoadBuildableConstructions(coreDataPath);
        var recipes = LoadRecipeDefinitions(Path.Combine(coreDataPath, "recipes"));

        return new CoreDataLoadResult(constructions, recipes);
    }
}
