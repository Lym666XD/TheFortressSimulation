using System;
using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Complete definition of a biome generation template
/// </summary>
public class BiomeTemplateDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; } = 100;

    // Conditions for this biome
    public BiomeConditions Conditions { get; set; } = new();

    // Layer stack
    public List<LayerDefinition> Layers { get; set; } = new();

    // Surface features
    public SurfaceFeatures Surface { get; set; } = new();

    // Underground features
    public UndergroundFeatures Underground { get; set; } = new();

    // Ore distribution
    public OreDistribution Ores { get; set; } = new();

    // Fluid settings
    public FluidSettings Fluids { get; set; } = new();

    // Vegetation settings
    public VegetationSettings Vegetation { get; set; } = new();

    // Noise settings
    public NoiseSettings Noise { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("Biome template must have an ID");

        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException($"Biome template '{Id}' must have a name");

        if (Layers.Count == 0)
            throw new InvalidOperationException($"Biome template '{Id}' must have at least one layer");
    }
}

public class BiomeConditions
{
    public Range<float>? Temperature { get; set; } // Kelvin
    public Range<float>? Rainfall { get; set; } // mm/year
    public Range<float>? Elevation { get; set; } // meters
    public Range<float>? Latitude { get; set; } // degrees
    public bool? Coastal { get; set; }
    public bool? River { get; set; }

    public bool Matches(BiomeParameters parameters)
    {
        if (Temperature != null && !Temperature.Value.Contains(parameters.Temperature))
            return false;

        if (Rainfall != null && !Rainfall.Value.Contains(parameters.Rainfall))
            return false;

        if (Elevation != null && !Elevation.Value.Contains(parameters.Elevation))
            return false;

        if (Latitude != null && !Latitude.Value.Contains(Math.Abs(parameters.Latitude)))
            return false;

        if (Coastal.HasValue && Coastal.Value != parameters.IsCoastal)
            return false;

        if (River.HasValue && River.Value != parameters.HasRiver)
            return false;

        return true;
    }
}

public class BiomeParameters
{
    public float Temperature { get; set; }
    public float Rainfall { get; set; }
    public float Elevation { get; set; }
    public float Latitude { get; set; }
    public bool IsCoastal { get; set; }
    public bool HasRiver { get; set; }
}

public class LayerDefinition
{
    public string? Material { get; set; }
    public MaterialDistribution? MaterialDistribution { get; set; }
    public int DepthMin { get; set; } = 1;
    public int DepthMax { get; set; } = 1;
    public string? DepthSpecial { get; set; } // "remaining", "bedrock"
    public string TerrainKind { get; set; } = "solid_wall";
    public LayerNoise? Noise { get; set; }
    public List<Inclusion> Inclusions { get; set; } = new();
    public string Transition { get; set; } = "sharp";
}

public class MaterialDistribution
{
    public string Type { get; set; } = "weighted";
    public List<MaterialWeight> Materials { get; set; } = new();
    public int? Seed { get; set; }

    public string SelectMaterial(System.Random rng)
    {
        if (Materials.Count == 0)
            return "";

        if (Type == "weighted")
        {
            float totalWeight = 0;
            foreach (var mat in Materials)
                totalWeight += mat.Weight;

            float roll = (float)rng.NextDouble() * totalWeight;
            float current = 0;

            foreach (var mat in Materials)
            {
                current += mat.Weight;
                if (roll <= current)
                    return mat.Name;
            }
        }

        // Fallback
        return Materials[0].Name;
    }
}

public class MaterialWeight
{
    public string Name { get; set; } = "";
    public float Weight { get; set; } = 1.0f;
}

public class LayerNoise
{
    public bool Enabled { get; set; } = true;
    public float Amplitude { get; set; } = 0.5f;
    public float Frequency { get; set; } = 0.05f;
}

public class Inclusion
{
    public string Material { get; set; } = "";
    public float Frequency { get; set; } = 0.01f;
    public Range<int> SizeRange { get; set; } = new(1, 3);
    public string Shape { get; set; } = "blob";
}

public class SurfaceFeatures
{
    public TopsoilSettings? Topsoil { get; set; }
    public BoulderSettings? Boulders { get; set; }
    public PoolSettings? Pools { get; set; }
}

public class TopsoilSettings
{
    public string Material { get; set; } = "soil";
    public Range<int> Depth { get; set; } = new(1, 3);
    public float Coverage { get; set; } = 0.9f;
}

public class BoulderSettings
{
    public float Density { get; set; } = 0.01f;
    public List<string> Materials { get; set; } = new();
    public Range<int> SizeRange { get; set; } = new(1, 3);
}

public class PoolSettings
{
    public float Frequency { get; set; } = 0.005f;
    public string Fluid { get; set; } = "water";
    public Range<int> DepthRange { get; set; } = new(1, 3);
}

public class UndergroundFeatures
{
    public CaveSettings? Caves { get; set; }
    public CavernSettings? Caverns { get; set; }
    public TunnelSettings? Tunnels { get; set; }
    public VoidSettings? Voids { get; set; }
}

public class CaveSettings
{
    public float Frequency { get; set; } = 0.1f;
    public int MinDepth { get; set; } = 5;
    public int MaxDepth { get; set; } = 40;
    public string Algorithm { get; set; } = "cellular";
    public float Connectivity { get; set; } = 0.5f;
    public float Wetness { get; set; } = 0.2f;
}

public class CavernSettings
{
    public List<CavernLayer> Layers { get; set; } = new();
}

public class CavernLayer
{
    public int Depth { get; set; }
    public Range<int> Height { get; set; } = new(3, 8);
    public float Openness { get; set; } = 0.7f;
    public string FloorMaterial { get; set; } = "stone";
    public List<string> Features { get; set; } = new();
}

public class TunnelSettings
{
    public bool Natural { get; set; } = true;
    public float Frequency { get; set; } = 0.05f;
    public string Connectivity { get; set; } = "local";
}

public class VoidSettings
{
    public float Frequency { get; set; } = 0.001f;
    public Range<int> SizeRange { get; set; } = new(2, 5);
}

public class OreDistribution
{
    public List<OreVein> Veins { get; set; } = new();
    public List<OreCluster> Clusters { get; set; } = new();
    public List<ScatteredOre> Scattered { get; set; } = new();
}

public class OreVein
{
    public string Material { get; set; } = "";
    public List<string> HostRock { get; set; } = new();
    public float Frequency { get; set; } = 0.01f;
    public Range<int> DepthRange { get; set; } = new(5, 40);
    public Range<int> VeinSize { get; set; } = new(3, 8);
    public float Richness { get; set; } = 0.5f;
}

public class OreCluster
{
    public string Material { get; set; } = "";
    public float Frequency { get; set; } = 0.005f;
    public Range<int> ClusterSize { get; set; } = new(5, 15);
    public Range<int> NodeCount { get; set; } = new(3, 7);
}

public class ScatteredOre
{
    public string Material { get; set; } = "";
    public float Density { get; set; } = 0.001f;
    public Range<int> DepthRange { get; set; } = new(0, 50);
}

public class FluidSettings
{
    public WaterTableSettings? WaterTable { get; set; }
    public List<AquiferSettings> Aquifers { get; set; } = new();
    public SpringSettings? Springs { get; set; }
}

public class WaterTableSettings
{
    public int Depth { get; set; } = 10;
    public int Variation { get; set; } = 3;
    public bool Seasonal { get; set; } = true;
}

public class AquiferSettings
{
    public Range<int> Depth { get; set; } = new(15, 25);
    public string Pressure { get; set; } = "medium";
    public float Salinity { get; set; } = 0.0f;
}

public class SpringSettings
{
    public float Frequency { get; set; } = 0.001f;
    public Range<int> FlowRate { get; set; } = new(1, 5);
}

public class VegetationSettings
{
    public List<TreeSettings> Trees { get; set; } = new();
    public List<ShrubSettings> Shrubs { get; set; } = new();
    public List<GroundCoverSettings> GroundCover { get; set; } = new();
}

public class TreeSettings
{
    public string Species { get; set; } = "";
    public float Density { get; set; } = 0.1f;
    public Range<int> MaturityRange { get; set; } = new(10, 100);
}

public class ShrubSettings
{
    public string Species { get; set; } = "";
    public float Density { get; set; } = 0.05f;
}

public class GroundCoverSettings
{
    public string Species { get; set; } = "";
    public float Coverage { get; set; } = 0.8f;
}

public class NoiseSettings
{
    public NoiseLayer? Elevation { get; set; }
    public NoiseLayer? Moisture { get; set; }
    public NoiseLayer? Temperature { get; set; }
    public NoiseLayer? Fertility { get; set; }
}

public class NoiseLayer
{
    public string Type { get; set; } = "perlin";
    public int Octaves { get; set; } = 4;
    public float Frequency { get; set; } = 0.01f;
    public float Amplitude { get; set; } = 1.0f;
    public float Persistence { get; set; } = 0.5f;
    public float Lacunarity { get; set; } = 2.0f;
    public int? Seed { get; set; }
}

public struct Range<T> where T : IComparable<T>
{
    public T Min { get; set; }
    public T Max { get; set; }

    public Range(T min, T max)
    {
        Min = min;
        Max = max;
    }

    public bool Contains(T value)
    {
        return value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;
    }
}