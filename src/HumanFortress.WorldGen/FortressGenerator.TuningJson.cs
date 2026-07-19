using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed partial class FortressGenerator
    {
        private static JsonObject? Object(JsonObject? parent, string name)
        {
            return parent?[name] as JsonObject;
        }

        private static JsonArray? Array(JsonObject? parent, string name)
        {
            return parent?[name] as JsonArray;
        }

        private static T Value<T>(JsonObject? parent, string name, T fallback)
        {
            if (parent?[name] is JsonValue value && value.TryGetValue<T>(out var result))
            {
                return result is null ? fallback : result;
            }

            return fallback;
        }

        private static int ArrayValue(JsonArray? array, int index, int fallback)
        {
            if (array == null || index < 0 || index >= array.Count)
            {
                return fallback;
            }

            if (array[index] is JsonValue value && value.TryGetValue<int>(out var result))
            {
                return result;
            }

            return fallback;
        }

        private static IEnumerable<string> StringValues(JsonArray array)
        {
            foreach (var node in array)
            {
                if (node is JsonValue value &&
                    value.TryGetValue<string>(out var result) &&
                    !string.IsNullOrEmpty(result))
                {
                    yield return result;
                }
            }
        }
    }
}
