using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Content validation result.
/// </summary>
public class ContentValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Content snapshot for saves.
/// </summary>
public class ContentSnapshot
{
    public ContentVersion Version { get; set; }
    public string ContentHash { get; set; } = "";
    public Dictionary<string, ushort> MaterialNames { get; set; } = new();
    public List<string> LoadedTemplates { get; set; } = new();
}
