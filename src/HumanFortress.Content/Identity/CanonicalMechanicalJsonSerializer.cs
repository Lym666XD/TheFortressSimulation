using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HumanFortress.Content.Identity;

/// <summary>
/// Canonical mechanical JSON v2 rules:
/// object keys use ordinal order; null is preserved; strings are decoded then
/// emitted as UTF-8 JSON; finite numbers use invariant shortest available form;
/// definition/set arrays are sorted by canonical element bytes; sequence arrays
/// such as biome layers retain author order. A source-family policy declares
/// presentation fields; unknown fields remain mechanical by default.
/// </summary>
internal static class CanonicalMechanicalJsonSerializer
{
    internal const string FormatId = "humanfortress.mechanical-json.v2";
    internal static string CosmeticPolicyId =>
        MechanicalContentCanonicalPolicy.PolicyId
        + "|"
        + MechanicalContentSourceFamilyManifest.CanonicalPolicyId;

    internal static byte[] Serialize(JsonElement root)
    {
        return Serialize(root, "synthetic");
    }

    internal static byte[] Serialize(JsonElement root, string familyId)
    {
        var policy = MechanicalContentCanonicalPolicy.Resolve(familyId);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.Default,
                   Indented = false,
                   SkipValidation = false
               }))
        {
            WriteElement(writer, root, policy, propertyName: null, isRoot: true);
        }

        return stream.ToArray();
    }

    private static void WriteElement(
        Utf8JsonWriter writer,
        JsonElement element,
        MechanicalContentCanonicalPolicy policy,
        string? propertyName,
        bool isRoot = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(property => !policy.IsCosmeticProperty(property.Name))
                             .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value, policy, property.Name);
                }
                writer.WriteEndObject();
                return;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                var values = element.EnumerateArray()
                    .Select(value => SerializeNested(value, policy))
                    .ToArray();
                if (isRoot || (propertyName != null && policy.IsUnorderedArray(propertyName)))
                    Array.Sort(values, CanonicalBytesComparer.Instance);
                foreach (var value in values)
                    writer.WriteRawValue(value, skipInputValidation: true);
                writer.WriteEndArray();
                return;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                return;

            case JsonValueKind.Number:
                writer.WriteRawValue(NormalizeNumber(element.GetRawText()), skipInputValidation: true);
                return;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                return;

            default:
                throw new InvalidDataException($"Unsupported JSON token {element.ValueKind}.");
        }
    }

    private static byte[] SerializeNested(
        JsonElement value,
        MechanicalContentCanonicalPolicy policy)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.Default,
                   Indented = false
               }))
        {
            WriteElement(writer, value, policy, propertyName: null);
        }
        return stream.ToArray();
    }

    private static string NormalizeNumber(string raw)
    {
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
            return signed.ToString(CultureInfo.InvariantCulture);
        if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsigned))
            return unsigned.ToString(CultureInfo.InvariantCulture);
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
            return exact == decimal.Zero ? "0" : exact.ToString("G29", CultureInfo.InvariantCulture);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
            && double.IsFinite(floating))
        {
            return floating == 0d ? "0" : floating.ToString("R", CultureInfo.InvariantCulture);
        }

        throw new InvalidDataException($"JSON number '{raw}' is outside supported finite numeric range.");
    }

    private sealed class CanonicalBytesComparer : IComparer<byte[]>
    {
        internal static CanonicalBytesComparer Instance { get; } = new();

        int IComparer<byte[]>.Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.AsSpan().SequenceCompareTo(right);
        }
    }
}
