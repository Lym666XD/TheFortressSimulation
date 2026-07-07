using System.Collections.Generic;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Definition of an alias for backward compatibility.
/// </summary>
public class AliasDefinition
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public ContentVersion? DeprecatedVersion { get; set; }
    public ContentVersion? RemoveVersion { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Definition of a content migration between versions.
/// </summary>
public class ContentMigration
{
    public ContentVersion FromVersion { get; set; }
    public ContentVersion ToVersion { get; set; }
    public string Type { get; set; } = "";
    public List<MigrationRule> Rules { get; set; } = new();
    public string? Description { get; set; }
}

/// <summary>
/// A single migration rule.
/// </summary>
public class MigrationRule
{
    public string Type { get; set; } = "";
    public string Action { get; set; } = "";
    public List<string> Source { get; set; } = new();
    public List<string> Target { get; set; } = new();
    public Dictionary<string, object>? Transform { get; set; }
    public Dictionary<string, object>? Condition { get; set; }
}
