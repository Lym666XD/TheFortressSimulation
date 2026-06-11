using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.World
{
    public struct WorldTile
    {
        public ushort BiomeId { get; set; }
        public float Elevation { get; set; }
        public float Temperature { get; set; }
        public float Rainfall { get; set; }
        public float Drainage { get; set; }
        public byte RiverClass { get; set; }
        public IReadOnlyList<ushort> StoneSet { get; set; }
        public bool HasAquifer { get; set; }
        public IReadOnlyList<int> LandmarkIds { get; set; }
        
        public bool IsEmbarkable => !GetEmbarkabilityFailures().Any();

        public IReadOnlyList<string> GetEmbarkabilityFailures()
        {
            var failures = new List<string>();

            if (Elevation <= 0.3f)
                failures.Add($"Elevation {Elevation:F2} <= 0.30");
            if (Elevation >= 0.8f)
                failures.Add($"Elevation {Elevation:F2} >= 0.80");
            if (RiverClass >= 3)
                failures.Add($"River class {RiverClass} >= 3");

            return failures;
        }
    }
    
    public enum BiomeType : ushort
    {
        Ocean = 0,
        Lake = 1,
        River = 2,
        Glacier = 10,
        Tundra = 11,
        Taiga = 12,
        TemperateForest = 20,
        TemperateGrassland = 21,
        Savanna = 30,
        Desert = 31,
        TropicalForest = 40,
        Swamp = 41,
        Mountain = 50,
        Hills = 51
    }
}
