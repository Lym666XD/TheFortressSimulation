using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HumanFortress.Content.Registry;
using HumanFortress.Contracts.Content.Identity;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Identity;

internal enum GeneratedContentActivation : byte
{
    Inactive = 0,
    Active = 1,
}

internal enum GeneratedContentFreshness : byte
{
    Fresh = 0,
    Stale = 1,
    SourceMissing = 2,
    OutputMissing = 3,
    Invalid = 4,
}

internal sealed record GeneratedContentFreshnessData(
    string SourceId,
    string OutputId,
    GeneratedContentActivation Activation,
    GeneratedContentFreshness Freshness,
    string SourceSemanticHash,
    string OutputSemanticHash,
    int SourceDefinitionCount,
    int OutputDefinitionCount,
    string GeneratorId,
    bool GeneratorAvailable,
    string Detail)
{
    internal bool IsBlocking => Activation == GeneratedContentActivation.Active
        && Freshness != GeneratedContentFreshness.Fresh;
}

internal sealed class MechanicalContentPipelineGateResult
{
    internal MechanicalContentPipelineGateResult(
        MechanicalContentIdentityData identity,
        IReadOnlyList<GeneratedContentFreshnessData> generatedOutputs)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        GeneratedOutputs = Array.AsReadOnly(
            generatedOutputs?.ToArray() ?? throw new ArgumentNullException(nameof(generatedOutputs)));
        Issues = Array.AsReadOnly(BuildIssues(identity, GeneratedOutputs));
    }

    internal MechanicalContentIdentityData Identity { get; }

    internal IReadOnlyList<GeneratedContentFreshnessData> GeneratedOutputs { get; }

    internal IReadOnlyList<MechanicalContentIssueData> Issues { get; }

    internal bool IsValid => !Issues.Any(
        static issue => issue.Severity == MechanicalContentIssueSeverity.Error);

    private static MechanicalContentIssueData[] BuildIssues(
        MechanicalContentIdentityData identity,
        IReadOnlyList<GeneratedContentFreshnessData> generatedOutputs)
    {
        return identity.Issues
            .Concat(generatedOutputs
                .Where(static output => output.IsBlocking)
                .Select(static output => new MechanicalContentIssueData(
                    MechanicalContentIssueSeverity.Error,
                    FreshnessIssueCode(output.Freshness),
                    output.OutputId,
                    "$",
                    output.Detail)))
            .OrderBy(static issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Message, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Severity)
            .ToArray();
    }

    private static string FreshnessIssueCode(GeneratedContentFreshness freshness)
    {
        return freshness switch
        {
            GeneratedContentFreshness.Stale => "Content.GeneratedOutput.Stale",
            GeneratedContentFreshness.SourceMissing => "Content.GeneratedOutput.SourceMissing",
            GeneratedContentFreshness.OutputMissing => "Content.GeneratedOutput.OutputMissing",
            GeneratedContentFreshness.Invalid => "Content.GeneratedOutput.Invalid",
            _ => "Content.GeneratedOutput.Unverified",
        };
    }
}

/// <summary>
/// Strict, deterministic entry point used by CI. Generated artifacts only
/// block when Runtime can consume them; inactive legacy outputs remain visible
/// as evidence instead of being treated as fresh or silently ignored.
/// </summary>
internal static class MechanicalContentPipelineGate
{
    internal static MechanicalContentPipelineGateResult Evaluate(
        string repositoryRoot,
        bool requireSchemaValidation = true,
        IContentJsonSchemaValidationAdapter? schemaAdapter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var contentPath = Path.Combine(repositoryRoot, "content");
        var coreDataPath = Path.Combine(repositoryRoot, "data", "core");
        var sources = MechanicalContentSourceScanner.Scan(contentPath, coreDataPath);
        return Evaluate(
            sources.MechanicalSources,
            sources.Schemas,
            new[] { GeneratedContentFreshnessChecker.EvaluateMaterials(contentPath) },
            requireSchemaValidation,
            schemaAdapter);
    }

    internal static MechanicalContentPipelineGateResult Evaluate(
        IEnumerable<MechanicalContentSourceDocument> sources,
        IEnumerable<ContentSchemaSourceDocument>? schemas,
        IEnumerable<GeneratedContentFreshnessData>? generatedOutputs = null,
        bool requireSchemaValidation = false,
        IContentJsonSchemaValidationAdapter? schemaAdapter = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var identity = MechanicalContentIdentityCompiler.Compile(
            sources,
            schemas,
            schemaAdapter ?? (requireSchemaValidation
                ? JsonSchemaNetContentValidationAdapter.Instance
                : UnavailableContentJsonSchemaValidationAdapter.Instance),
            requireSchemaValidation);
        return new MechanicalContentPipelineGateResult(
            identity,
            generatedOutputs?
                .OrderBy(static output => output.OutputId, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<GeneratedContentFreshnessData>());
    }
}

internal static class GeneratedContentFreshnessChecker
{
    private const string MaterialsSourceId = "content/registries/materials.authoring.json";
    private const string MaterialsOutputId = "content/registries/materials.registry.json";
    private const string LegacyGeneratorId = "tools/MaterialCompiler.cs:legacy-unbuildable";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    internal static GeneratedContentFreshnessData EvaluateMaterials(string contentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        var sourcePath = Path.Combine(contentPath, "registries", "materials.authoring.json");
        var outputPath = Path.Combine(contentPath, "registries", "materials.registry.json");
        var sourceExists = File.Exists(sourcePath);
        var outputExists = File.Exists(outputPath);
        var activation = sourceExists
            ? GeneratedContentActivation.Inactive
            : GeneratedContentActivation.Active;

        if (!sourceExists)
        {
            return Result(
                activation,
                GeneratedContentFreshness.SourceMissing,
                detail: outputExists
                    ? "The runtime registry is active, but its authoritative authoring source is missing and the legacy generator is unavailable."
                    : "Neither the authoritative material source nor a runtime registry exists.");
        }

        if (!outputExists)
        {
            return Result(
                activation,
                GeneratedContentFreshness.OutputMissing,
                detail: "No generated material registry is committed; Runtime consumes the authoring source directly.");
        }

        try
        {
            var source = BuildMaterialProjection(sourcePath, isAuthoringFormat: true);
            var output = BuildMaterialProjection(outputPath, isAuthoringFormat: false);
            var freshness = source.Hash == output.Hash
                ? GeneratedContentFreshness.Fresh
                : GeneratedContentFreshness.Stale;
            var detail = freshness == GeneratedContentFreshness.Fresh
                ? "The inactive registry is semantically equivalent to the authoring source."
                : "The inactive registry differs from the authoring source. Runtime consumes authoring directly; the legacy output must not be activated.";
            return Result(
                activation,
                freshness,
                source.Hash,
                output.Hash,
                source.DefinitionCount,
                output.DefinitionCount,
                detail);
        }
        catch (Exception exception) when (exception is JsonException
                                          or InvalidDataException
                                          or FormatException
                                          or InvalidOperationException
                                          or OverflowException)
        {
            return Result(
                activation,
                GeneratedContentFreshness.Invalid,
                detail: $"Material freshness projection failed: {exception.Message}");
        }
    }

    private static GeneratedContentFreshnessData Result(
        GeneratedContentActivation activation,
        GeneratedContentFreshness freshness,
        string sourceHash = "",
        string outputHash = "",
        int sourceCount = 0,
        int outputCount = 0,
        string detail = "")
    {
        return new GeneratedContentFreshnessData(
            MaterialsSourceId,
            MaterialsOutputId,
            activation,
            freshness,
            sourceHash,
            outputHash,
            sourceCount,
            outputCount,
            LegacyGeneratorId,
            GeneratorAvailable: false,
            detail);
    }

    private static MaterialProjection BuildMaterialProjection(
        string path,
        bool isAuthoringFormat)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), JsonOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"'{Path.GetFileName(path)}' must contain a material array.");

        var materials = document.RootElement.EnumerateArray()
            .Select(element => ParseProjectedMaterial(element, isAuthoringFormat))
            .OrderBy(static material => material.StringId, StringComparer.Ordinal)
            .ToArray();
        var duplicate = materials
            .GroupBy(static material => material.StringId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidDataException($"Duplicate or ambiguous material id '{duplicate.Key}'.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddString(hash, "humanfortress.generated-material-semantics.v2");
        AddInt32(hash, materials.Length);
        foreach (var material in materials)
            AddMaterial(hash, material);

        return new MaterialProjection(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            materials.Length);
    }

    private static MaterialDefinition ParseProjectedMaterial(
        JsonElement element,
        bool isAuthoringFormat)
    {
        var material = MaterialParser.ParseMaterial(element, isAuthoringFormat);
        if (!isAuthoringFormat)
        {
            if (element.TryGetProperty("mechanics", out var mechanics))
            {
                material.Mechanics.HardnessEdgeFx = RequiredInt32(mechanics, "hardnessEdgeFx");
                material.Mechanics.ToughnessFracFx = RequiredInt32(mechanics, "toughnessFracFx");
                material.Mechanics.RigidityFx = RequiredInt32(mechanics, "rigidityFx");
            }

            if (element.TryGetProperty("electricMagic", out var electricMagic))
            {
                material.ElectricMagic.ManaConductivityFx = OptionalInt32(
                    electricMagic,
                    "manaConductivityFx",
                    FixedPoint.FX);
                material.ElectricMagic.ElectricCategory = OptionalString(
                    electricMagic,
                    "electricCategory",
                    "semi").ToUpperInvariant() switch
                {
                    "CONDUCTOR" => ElectricCategory.Conductor,
                    "INSULATOR" => ElectricCategory.Insulator,
                    _ => ElectricCategory.Semi,
                };
            }

            if (element.TryGetProperty("economy", out var economy))
            {
                material.Economy.ValueMulFx = OptionalInt32(economy, "valueMulFx", FixedPoint.FX);
                material.Economy.BeautyMulFx = OptionalInt32(economy, "beautyMulFx", FixedPoint.FX);
            }
        }

        if (element.TryGetProperty("navigation", out var navigation))
        {
            material.Navigation.MoveCostAdd = OptionalInt32(
                navigation,
                "moveCostAdd",
                OptionalInt32(navigation, "move_cost_add", 0));
            material.Navigation.HazardLevel = OptionalInt32(
                navigation,
                "hazardLevel",
                OptionalInt32(navigation, "hazard_level", 0));
            material.Navigation.HazardType = OptionalString(
                navigation,
                "hazardType",
                OptionalString(navigation, "hazard_type", "none"));
        }

        return material;
    }

    private static int RequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            throw new InvalidDataException($"Required material property '{propertyName}' is missing.");
        return value.GetInt32();
    }

    private static int OptionalInt32(JsonElement element, string propertyName, int fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetInt32()
            : fallback;
    }

    private static string OptionalString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static void AddMaterial(IncrementalHash hash, MaterialDefinition material)
    {
        AddString(hash, material.StringId);
        AddStrings(hash, material.Aliases);
        AddString(hash, material.Category);
        AddStrings(hash, material.Tags);
        AddInt32(hash, material.DensitySolid);
        AddInt32(hash, material.Mechanics.HardnessEdgeFx);
        AddInt32(hash, material.Mechanics.ToughnessFracFx);
        AddInt32(hash, material.Mechanics.RigidityFx);
        AddInt32(hash, (int)material.ElectricMagic.ElectricCategory);
        AddInt32(hash, material.ElectricMagic.ManaConductivityFx);
        AddInt32(hash, material.Economy.ValueMulFx);
        AddInt32(hash, material.Economy.BeautyMulFx);
        AddBoolean(hash, material.Work.Forgeable);
        AddBoolean(hash, material.Work.Weldable);
        AddBoolean(hash, material.Work.Carveable);
        AddInt32(hash, material.Work.ProcessDifficultyMulFx);
        AddString(hash, material.Phase ?? string.Empty);
        AddInt32(hash, material.Navigation.MoveCostAdd);
        AddInt32(hash, material.Navigation.FrictionMulFx);
        AddInt32(hash, material.Navigation.HazardLevel);
        AddString(hash, material.Navigation.HazardType);
    }

    private static void AddStrings(IncrementalHash hash, IEnumerable<string> values)
    {
        var ordered = values.OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        AddInt32(hash, ordered.Length);
        foreach (var value in ordered)
            AddString(hash, value);
    }

    private static void AddString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AddInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AddInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AddBoolean(IncrementalHash hash, bool value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        hash.AppendData(bytes);
    }

    private readonly record struct MaterialProjection(string Hash, int DefinitionCount);
}
