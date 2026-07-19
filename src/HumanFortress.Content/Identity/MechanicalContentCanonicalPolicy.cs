namespace HumanFortress.Content.Identity;

/// <summary>
/// Declares which authored JSON fields are presentation-only and which arrays
/// are mathematical sets/multisets. Everything not listed remains mechanical
/// and sequence-sensitive by default.
/// </summary>
internal sealed class MechanicalContentCanonicalPolicy
{
    internal const string PolicyId = "humanfortress.cosmetic-policy.v2";

    private static readonly HashSet<string> UniversalCosmeticProperties = new(
        new[]
        {
            "$schema",
            "_comment",
            "_comments",
            "background",
            "color",
            "desc",
            "description",
            "display",
            "display_name",
            "foreground",
            "glyph",
            "keybind",
            "notes",
            "particle_color",
            "ui_hints",
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> UniversalUnorderedArrays = new(
        new[]
        {
            "ai_tags",
            "aliases",
            "allow_tags",
            "allowed_host_tags",
            "allowed_material_tags",
            "allowed_materials",
            "allowedMaterials",
            "allowed_tags",
            "allows_actions",
            "attachment_slots",
            "attachments",
            "body_plans",
            "connect_groups",
            "connects_to",
            "constructions",
            "creatures",
            "definitions",
            "destroyer_immune_tags",
            "enablers",
            "equal_when",
            "features",
            "functional_roles",
            "geology",
            "host_rock",
            "hybrid_requirements",
            "inputs",
            "item_ids",
            "itemIds",
            "items",
            "job_tags",
            "material_costs",
            "materials",
            "materials_required",
            "orientation_mask",
            "outputs",
            "presets",
            "professions",
            "prototypes",
            "recipes",
            "requires_enablers",
            "requires_installed_components",
            "rotation_degrees",
            "skills",
            "tags",
            "templates",
            "terrainKinds",
            "workshops",
            "zones",
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> FamiliesWithCosmeticName = new(
        new[]
        {
            "content.registry.creature",
            "content.registry.orders",
            "content.registry.professions",
            "content.registry.stockpile-presets",
            "content.registry.stockpiles",
            "content.registry.zones",
            "core.construction",
            "core.construction.legacy-workshops",
            "core.creature",
            "core.item",
            "core.item.legacy-weapons",
            "core.placeable.terrain-designation",
            "core.placeable.workshop-attachment",
            "core.recipe",
            "core.workshop",
        },
        StringComparer.Ordinal);

    private readonly bool _nameIsCosmetic;
    private readonly bool _hotkeyIsCosmetic;
    private readonly bool _verbIsCosmetic;

    private MechanicalContentCanonicalPolicy(string familyId)
    {
        FamilyId = familyId;
        _nameIsCosmetic = FamiliesWithCosmeticName.Contains(familyId);
        _hotkeyIsCosmetic = familyId.Equals(
            "content.registry.orders",
            StringComparison.Ordinal);
        _verbIsCosmetic = familyId is "content.registry.creature" or "core.creature";
    }

    internal string FamilyId { get; }

    internal static MechanicalContentCanonicalPolicy Resolve(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            throw new InvalidDataException("Mechanical content source family is required.");

        if (!familyId.Equals("synthetic", StringComparison.Ordinal)
            && !MechanicalContentSourceFamilyManifest.TryGetDeclaration(familyId, out _))
        {
            throw new InvalidDataException(
                $"Mechanical content source family '{familyId}' has no canonical policy declaration.");
        }

        return new MechanicalContentCanonicalPolicy(familyId);
    }

    internal bool IsCosmeticProperty(string propertyName)
    {
        return UniversalCosmeticProperties.Contains(propertyName)
            || (_nameIsCosmetic && propertyName.Equals("name", StringComparison.OrdinalIgnoreCase))
            || (_hotkeyIsCosmetic && propertyName.Equals("hotkey", StringComparison.OrdinalIgnoreCase))
            || (_verbIsCosmetic && propertyName.Equals("verb", StringComparison.OrdinalIgnoreCase));
    }

    internal bool IsUnorderedArray(string propertyName)
    {
        return UniversalUnorderedArrays.Contains(propertyName);
    }
}
