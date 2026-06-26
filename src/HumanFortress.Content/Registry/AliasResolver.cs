using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Registry;

/// <summary>
/// Resolves aliases and handles content migrations.
/// </summary>
internal sealed class AliasResolver
{
    private readonly List<AliasDefinition> _materialAliases = new();
    private readonly List<AliasDefinition> _terrainAliases = new();
    private readonly List<ContentMigration> _migrations = new();

    internal ContentVersion Version { get; private set; }

    /// <summary>
    /// Load aliases from definitions
    /// </summary>
    internal void LoadAliases(IEnumerable<AliasDefinition> materialAliases,
        IEnumerable<AliasDefinition> terrainAliases,
        IEnumerable<ContentMigration> migrations,
        ContentVersion version)
    {
        Version = version;
        Clear();

        _materialAliases.AddRange(materialAliases);
        _terrainAliases.AddRange(terrainAliases);
        _migrations.AddRange(migrations.OrderBy(m => m.FromVersion));

        ContentRegistryDiagnostics.Emit($"[AliasResolver] Loaded {_materialAliases.Count} material aliases, " +
                                        $"{_terrainAliases.Count} terrain aliases, {_migrations.Count} migrations");
    }

    /// <summary>
    /// Resolve a material name through aliases
    /// </summary>
    internal string ResolveMaterialName(string name, ContentVersion? fromVersion = null)
    {
        // If we have a from version, apply migrations first
        if (fromVersion != null)
        {
            name = ApplyMaterialMigrations(name, fromVersion.Value);
        }

        // Apply aliases
        var alias = _materialAliases.FirstOrDefault(a => a.From == name);
        if (alias != null)
        {
            ContentRegistryDiagnostics.Emit($"[AliasResolver] Resolved material alias: '{name}' -> '{alias.To}'");
            return alias.To;
        }

        return name;
    }

    /// <summary>
    /// Resolve a terrain kind name through aliases
    /// </summary>
    internal string ResolveTerrainName(string name, ContentVersion? fromVersion = null)
    {
        // If we have a from version, apply migrations first
        if (fromVersion != null)
        {
            name = ApplyTerrainMigrations(name, fromVersion.Value);
        }

        // Apply aliases
        var alias = _terrainAliases.FirstOrDefault(a => a.From == name);
        if (alias != null)
        {
            ContentRegistryDiagnostics.Emit($"[AliasResolver] Resolved terrain alias: '{name}' -> '{alias.To}'");
            return alias.To;
        }

        return name;
    }

    /// <summary>
    /// Apply material migrations from a specific version
    /// </summary>
    private string ApplyMaterialMigrations(string name, ContentVersion fromVersion)
    {
        var relevantMigrations = _migrations
            .Where(m => m.FromVersion >= fromVersion && m.FromVersion < Version)
            .OrderBy(m => m.FromVersion);

        foreach (var migration in relevantMigrations)
        {
            var rule = migration.Rules.FirstOrDefault(r =>
                r.Type == "material" && r.Source.Contains(name));

            if (rule != null)
            {
                name = ApplyMigrationRule(name, rule, migration);
            }
        }

        return name;
    }

    /// <summary>
    /// Apply terrain migrations from a specific version
    /// </summary>
    private string ApplyTerrainMigrations(string name, ContentVersion fromVersion)
    {
        var relevantMigrations = _migrations
            .Where(m => m.FromVersion >= fromVersion && m.FromVersion < Version)
            .OrderBy(m => m.FromVersion);

        foreach (var migration in relevantMigrations)
        {
            var rule = migration.Rules.FirstOrDefault(r =>
                r.Type == "terrain" && r.Source.Contains(name));

            if (rule != null)
            {
                name = ApplyMigrationRule(name, rule, migration);
            }
        }

        return name;
    }

    /// <summary>
    /// Apply a single migration rule
    /// </summary>
    private string ApplyMigrationRule(string name, MigrationRule rule, ContentMigration migration)
    {
        switch (rule.Action)
        {
            case "rename":
                if (rule.Source.Count == 1 && rule.Source[0] == name)
                {
                    var newName = rule.Target.FirstOrDefault() ?? name;
                    ContentRegistryDiagnostics.Emit($"[AliasResolver] Migration {migration.FromVersion}->{migration.ToVersion}: " +
                                                    $"Renamed '{name}' to '{newName}'");
                    return newName;
                }
                break;

            case "replace":
                if (rule.Source.Contains(name))
                {
                    var newName = rule.Target.FirstOrDefault() ?? name;
                    ContentRegistryDiagnostics.Emit($"[AliasResolver] Migration {migration.FromVersion}->{migration.ToVersion}: " +
                                                    $"Replaced '{name}' with '{newName}'");
                    return newName;
                }
                break;

            case "remove":
                if (rule.Source.Contains(name))
                {
                    var fallback = rule.Target.FirstOrDefault() ?? "generic_stone";
                    ContentRegistryDiagnostics.Emit($"[AliasResolver] Migration {migration.FromVersion}->{migration.ToVersion}: " +
                                                    $"Removed '{name}', using fallback '{fallback}'");
                    return fallback;
                }
                break;
        }

        return name;
    }

    /// <summary>
    /// Get all active aliases for a given version
    /// </summary>
    internal Dictionary<string, string> GetActiveMaterialAliases(ContentVersion? maxVersion = null)
    {
        var result = new Dictionary<string, string>();

        foreach (var alias in _materialAliases)
        {
            // Skip if deprecated after max version
            if (maxVersion != null && alias.DeprecatedVersion != null &&
                alias.DeprecatedVersion > maxVersion)
            {
                continue;
            }

            result[alias.From] = alias.To;
        }

        return result;
    }

    /// <summary>
    /// Check if an alias is deprecated
    /// </summary>
    internal bool IsDeprecated(string aliasName, bool isMaterial = true)
    {
        var aliases = isMaterial ? _materialAliases : _terrainAliases;
        var alias = aliases.FirstOrDefault(a => a.From == aliasName);

        return alias?.DeprecatedVersion != null && alias.DeprecatedVersion <= Version;
    }

    /// <summary>
    /// Get deprecation warning for an alias
    /// </summary>
    internal string? GetDeprecationWarning(string aliasName, bool isMaterial = true)
    {
        var aliases = isMaterial ? _materialAliases : _terrainAliases;
        var alias = aliases.FirstOrDefault(a => a.From == aliasName);

        if (alias?.DeprecatedVersion != null && alias.DeprecatedVersion <= Version)
        {
            var type = isMaterial ? "Material" : "Terrain";
            var warning = $"{type} '{aliasName}' is deprecated since version {alias.DeprecatedVersion}. " +
                         $"Use '{alias.To}' instead.";

            if (alias.RemoveVersion != null)
            {
                warning += $" Support will be removed in version {alias.RemoveVersion}.";
            }

            if (!string.IsNullOrEmpty(alias.Reason))
            {
                warning += $" Reason: {alias.Reason}";
            }

            return warning;
        }

        return null;
    }

    /// <summary>
    /// Clear all aliases and migrations
    /// </summary>
    private void Clear()
    {
        _materialAliases.Clear();
        _terrainAliases.Clear();
        _migrations.Clear();
    }
}
