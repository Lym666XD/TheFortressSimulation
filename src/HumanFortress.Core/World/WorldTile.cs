using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.World
{
    public struct WorldTile
    {
        private const float MinEmbarkElevation = 0.25f;
        private const float MaxEmbarkElevation = 0.80f;
        private const byte MaxEmbarkRiverClass = 3;

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

            if (Elevation <= MinEmbarkElevation)
                failures.Add($"Elevation {Elevation:F2} <= {MinEmbarkElevation:F2}");
            if (Elevation >= MaxEmbarkElevation)
                failures.Add($"Elevation {Elevation:F2} >= {MaxEmbarkElevation:F2}");
            if (RiverClass >= MaxEmbarkRiverClass)
                failures.Add($"River class {RiverClass} >= {MaxEmbarkRiverClass}");

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
