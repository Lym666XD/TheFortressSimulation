using System;
using HumanFortress.Contracts.Content.Registry;
using System.Text.Json.Nodes;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed class FortressGenerationContent
    {
        internal FortressGenerationContent(
            IRuntimeGeologyCatalog geology,
            string? mapgenTuningJson,
            string? oreTuningJson,
            string? cavernTuningJson)
        {
            Geology = geology ?? throw new ArgumentNullException(nameof(geology));
            MapgenTuning = ParseTuning(mapgenTuningJson);
            OreTuning = ParseTuning(oreTuningJson);
            CavernTuning = ParseTuning(cavernTuningJson);
        }

        internal IRuntimeGeologyCatalog Geology { get; }
        internal JsonObject? MapgenTuning { get; }
        internal JsonObject? OreTuning { get; }
        internal JsonObject? CavernTuning { get; }

        private static JsonObject? ParseTuning(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(json) as JsonObject;
            }
            catch
            {
                return null;
            }
        }
    }
}
