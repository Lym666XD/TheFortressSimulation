using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Loads workshop category -> tags mapping from content/registries/ui.workshop_categories.json.
    /// Provides helpers to get tags or filter constructions by category.
    /// Falls back to a baked-in mapping if file is missing.
    /// </summary>
    public static class WorkshopCategoryMapper
    {
        private static readonly object _lock = new();
        private static bool _loaded = false;
        private static Dictionary<string, string[]> _map = new(StringComparer.OrdinalIgnoreCase);

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    var registryFile = FortressContentLoader.ResolveRegistryFile(baseDir, "ui.workshop_categories.json");
                    if (registryFile.ResolvedPath != null)
                    {
                        var json = File.ReadAllText(registryFile.ResolvedPath);
                        var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.NameEquals("_comment")) continue;
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                var tags = prop.Value.EnumerateArray()
                                    .Where(e => e.ValueKind == JsonValueKind.String)
                                    .Select(e => e.GetString() ?? "")
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .ToArray();
                                dict[prop.Name] = tags;
                            }
                        }
                        _map = dict;
                    }
                    else
                    {
                        _map = GetFallback();
                    }
                }
                catch
                {
                    _map = GetFallback();
                }
                finally { _loaded = true; }
            }
        }

        private static Dictionary<string, string[]> GetFallback()
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "mining",   new []{"mining","smelting","fuel","lime","concrete"} },
                { "lumbering",new []{"logging"} },
                { "farming",  new []{"farming","butchery","pasture","tanning","kitchen","compost"} },
                { "industry", new []{"stoneworks","woodworking","metalworks","glass","pottery","chemistry","paper","tailoring"} },
                { "crafts",   new []{"crafts","precision","firearms","alchemy","enchanting"} }
            };
        }

        public static string[] GetTagsForCategory(string category)
        {
            EnsureLoaded();
            if (_map.TryGetValue(category, out var tags)) return tags;
            return Array.Empty<string>();
        }

        public static List<ConstructionDefinition> GetWorkshopsByCategory(IConstructionCatalog? constructions, string category)
        {
            EnsureLoaded();
            var all = new List<ConstructionDefinition>();
            if (constructions == null)
                return all;

            foreach (var d in constructions.GetConstructionsByCategory("workshop")) all.Add(d);
            if (all.Count == 0) foreach (var d in constructions.GetConstructionsByCategory("workshops")) all.Add(d);

            var tags = new HashSet<string>(GetTagsForCategory(category), StringComparer.OrdinalIgnoreCase);
            bool HasTag(ConstructionDefinition d, string tag)
                => d.PlaceableProfile.Tags != null && Array.IndexOf(d.PlaceableProfile.Tags, tag) >= 0;

            var list = new List<ConstructionDefinition>();
            foreach (var d in all)
            {
                if (d.PlaceableProfile.Tags == null) continue;
                foreach (var tag in tags)
                {
                    if (HasTag(d, tag)) { list.Add(d); break; }
                }
            }
            return list;
        }
    }
}
