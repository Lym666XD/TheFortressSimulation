using System;
using System.IO;
using System.Text.Json;
using HumanFortress.Core.Content;
using HumanFortress.Core.Content.Registry;

namespace HumanFortress.Tools;

/// <summary>
/// Compiler to convert materials.authoring.json to materials.registry.json
/// Converts human-readable percentages to FX integers for runtime performance
/// </summary>
public static class MaterialCompiler
{
    public static void Main(string[] args)
    {
        string inputPath = args.Length > 0 ? args[0] : "content/registries/materials.authoring.json";
        string outputPath = args.Length > 1 ? args[1] : "content/registries/materials.registry.json";

        Console.WriteLine($"[MaterialCompiler] Converting {inputPath} → {outputPath}");

        // Read authoring JSON
        var jsonText = File.ReadAllText(inputPath);
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        // Parse materials (authoring format)
        var materials = new System.Collections.Generic.List<MaterialDefinition>();
        foreach (var elem in root.EnumerateArray())
        {
            var material = MaterialParser.ParseMaterial(elem, isAuthoringFormat: true);
            materials.Add(material);
            Console.WriteLine($"  Parsed: {material.Id} - {material.Name}");
        }

        // Serialize to runtime format (FX integers)
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var outputJson = JsonSerializer.Serialize(materials, options);
        File.WriteAllText(outputPath, outputJson);

        Console.WriteLine($"[MaterialCompiler] Done! Compiled {materials.Count} materials.");
        Console.WriteLine($"[MaterialCompiler] Output written to: {outputPath}");
    }
}
