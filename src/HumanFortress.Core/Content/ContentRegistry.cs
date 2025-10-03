using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

#pragma warning disable CA1002 // Do not expose generic lists
#pragma warning disable CA2227 // Collection properties should be read only

namespace HumanFortress.Core.Content
{
    /// <summary>
    /// Central registry for all data-driven content per CONTENT_REGISTRY_OVERVIEW.md.
    /// Loads materials, geology, items, buildables, etc. from JSON with validation.
    /// </summary>
    public sealed class ContentRegistry
    {
        private static ContentRegistry? _instance;
        public static ContentRegistry Instance => _instance ??= new ContentRegistry();

        private readonly Dictionary<string, MaterialData> _materials = new();
        private readonly Dictionary<string, GeologyData> _geology = new();
        private readonly Dictionary<string, ZoneDefinitionData> _zones = new();
        private readonly Dictionary<string, JObject> _tuning = new();
        private readonly List<ContentError> _errors = new();

        // Numeric handles for runtime performance
        private readonly Dictionary<string, ushort> _materialHandles = new();
        private readonly Dictionary<string, ushort> _geologyHandles = new();
        private readonly Dictionary<ushort, string> _handleToMaterialId = new();
        private readonly Dictionary<ushort, string> _handleToGeologyId = new();
        private readonly Dictionary<(string material, string kind), ushort> _geologyByMaterialAndKind = new();

        public IReadOnlyDictionary<string, MaterialData> Materials => _materials;
        public IReadOnlyDictionary<string, GeologyData> GeologyEntries => _geology;
        public IReadOnlyDictionary<string, ZoneDefinitionData> Zones => _zones;
        public IReadOnlyList<ContentError> Errors => _errors;

        private ContentRegistry() { }

        /// <summary>
        /// Load all content from the content directory structure.
        /// </summary>
        public void LoadContent(string contentPath)
        {
            _errors.Clear();

            var registriesPath = Path.Combine(contentPath, "registries");
            var schemasPath = Path.Combine(contentPath, "schemas");

            if (!Directory.Exists(registriesPath))
            {
                _errors.Add(new ContentError("ContentRegistry", "registries", null,
                    $"Registries directory not found: {registriesPath}"));
                return;
            }

            // Load tuning files first
            LoadTuningFiles(registriesPath);

            // Load materials
            LoadMaterials(Path.Combine(registriesPath, "materials.json"), schemasPath);

            // Load geology/terrain
            LoadGeology(Path.Combine(registriesPath, "geology.json"), schemasPath);

            // Load zones
            LoadZones(Path.Combine(registriesPath, "zones.json"));

            // Assign stable handles
            AssignHandles();

            // Build fast geology index for (material,kind)
            BuildGeologyMaterialKindIndex();

            // Validate cross-references
            ValidateCrossReferences();

            Console.WriteLine($"[ContentRegistry] Loaded: {_materials.Count} materials, {_geology.Count} geology entries, {_zones.Count} zone definitions");
            if (_errors.Count > 0)
            {
                Console.WriteLine($"[ContentRegistry] {_errors.Count} errors during loading");
            }
        }

        /// <summary>
        /// Get zone definition data by ID.
        /// </summary>
        public ZoneDefinitionData? GetZoneDefinition(string id)
        {
            return _zones.TryGetValue(id, out var zone) ? zone : null;
        }

        private void BuildGeologyMaterialKindIndex()
        {
            _geologyByMaterialAndKind.Clear();
            foreach (var kv in _geology)
            {
                var id = kv.Key;
                var g = kv.Value;
                try
                {
                    var mat = g.Material;
                    var kind = g.TerrainBits?.Kind;
                    if (string.IsNullOrWhiteSpace(mat) || string.IsNullOrWhiteSpace(kind)) continue;
                    var handle = GetGeologyHandle(id);
                    _geologyByMaterialAndKind[(mat, kind!)] = handle;
                }
                catch { }
            }
        }

        public bool TryGetGeologyHandleByMaterialAndKind(string materialId, string terrainKindName, out ushort handle)
        {
            if (string.IsNullOrWhiteSpace(materialId) || string.IsNullOrWhiteSpace(terrainKindName))
            {
                handle = 0; return false;
            }
            return _geologyByMaterialAndKind.TryGetValue((materialId, terrainKindName), out handle);
        }

        private void LoadTuningFiles(string registriesPath)
        {
            var tuningFiles = new[]
            {
                "tuning.tile.json",
                "tuning.damage.json",
                "tuning.mapgen.json",
                "tuning.ore.json",
                "tuning.cavern.json",
                "tuning.navigation.json",
                // Added to support data-driven hauling/stockpile tuning
                "tuning.hauling.json",
                "tuning.stockpile.json",
                // Mining tuning (dig time, drops)
                "tuning.mining.json"
            };

            foreach (var file in tuningFiles)
            {
                var path = Path.Combine(registriesPath, file);
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var obj = JObject.Parse(json);
                        var key = Path.GetFileNameWithoutExtension(file);
                        _tuning[key] = obj;
                    }
                    catch (Exception ex)
                    {
                        _errors.Add(new ContentError("Tuning", file, null, ex.Message));
                    }
                }
            }
        }

        private void LoadMaterials(string path, string schemasPath)
        {
            if (!File.Exists(path))
            {
                _errors.Add(new ContentError("Materials", "materials.json", null, "File not found"));
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var materials = JsonConvert.DeserializeObject<List<MaterialData>>(json, new JsonSerializerSettings());

                if (materials != null)
                {
                    foreach (var material in materials)
                    {
                        if (string.IsNullOrEmpty(material.Id))
                        {
                            _errors.Add(new ContentError("Materials", "materials.json", null, "Material missing ID"));
                            continue;
                        }

                        if (_materials.ContainsKey(material.Id))
                        {
                            _errors.Add(new ContentError("Materials", "materials.json", material.Id,
                                $"Duplicate material ID: {material.Id}"));
                            continue;
                        }

                        _materials[material.Id] = material;
                    }
                }
            }
            catch (Exception ex)
            {
                _errors.Add(new ContentError("Materials", "materials.json", null, ex.Message));
            }
        }

        private void LoadGeology(string path, string schemasPath)
        {
            if (!File.Exists(path))
            {
                _errors.Add(new ContentError("Geology", "geology.json", null, "File not found"));
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var entries = JsonConvert.DeserializeObject<List<GeologyData>>(json, new JsonSerializerSettings());

                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.Id))
                        {
                            _errors.Add(new ContentError("Geology", "geology.json", null, "Geology entry missing ID"));
                            continue;
                        }

                        if (_geology.ContainsKey(entry.Id))
                        {
                            _errors.Add(new ContentError("Geology", "geology.json", entry.Id,
                                $"Duplicate geology ID: {entry.Id}"));
                            continue;
                        }

                        _geology[entry.Id] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                _errors.Add(new ContentError("Geology", "geology.json", null, ex.Message));
            }
        }

        private void LoadZones(string path)
        {
            if (!File.Exists(path))
            {
                _errors.Add(new ContentError("Zones", "zones.json", null, "File not found"));
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var root = JObject.Parse(json);
                var zonesArray = root["zones"] as JArray;

                if (zonesArray != null)
                {
                    foreach (var zoneToken in zonesArray)
                    {
                        var zone = zoneToken.ToObject<ZoneDefinitionData>();
                        if (zone == null)
                        {
                            _errors.Add(new ContentError("Zones", "zones.json", null, "Failed to parse zone definition"));
                            continue;
                        }

                        if (string.IsNullOrEmpty(zone.Id))
                        {
                            _errors.Add(new ContentError("Zones", "zones.json", null, "Zone missing ID"));
                            continue;
                        }

                        if (_zones.ContainsKey(zone.Id))
                        {
                            _errors.Add(new ContentError("Zones", "zones.json", zone.Id,
                                $"Duplicate zone ID: {zone.Id}"));
                            continue;
                        }

                        _zones[zone.Id] = zone;
                    }
                }
            }
            catch (Exception ex)
            {
                _errors.Add(new ContentError("Zones", "zones.json", null, ex.Message));
            }
        }

        private void AssignHandles()
        {
            // Assign material handles (sorted for determinism)
            ushort handle = 0;
            foreach (var id in _materials.Keys.OrderBy(k => k))
            {
                _materialHandles[id] = handle;
                _handleToMaterialId[handle] = id;
                handle++;
            }

            // Assign geology handles
            handle = 0;
            foreach (var id in _geology.Keys.OrderBy(k => k))
            {
                _geologyHandles[id] = handle;
                _handleToGeologyId[handle] = id;
                handle++;
            }
        }

        private void ValidateCrossReferences()
        {
            // Validate geology references to materials
            foreach (var geo in _geology.Values)
            {
                if (!string.IsNullOrEmpty(geo.Material) && !_materials.ContainsKey(geo.Material))
                {
                    _errors.Add(new ContentError("Geology", "geology.json", geo.Id,
                        $"References unknown material: {geo.Material}"));
                }
            }
        }

        // Access methods
        public MaterialData? GetMaterial(string id)
        {
            return _materials.TryGetValue(id, out var mat) ? mat : null;
        }

        public GeologyData? GetGeology(string id)
        {
            return _geology.TryGetValue(id, out var geo) ? geo : null;
        }

        public MaterialData? GetMaterialByHandle(ushort handle)
        {
            if (_handleToMaterialId.TryGetValue(handle, out var id))
            {
                return GetMaterial(id);
            }
            return null;
        }

        public GeologyData? GetGeologyByHandle(ushort handle)
        {
            if (_handleToGeologyId.TryGetValue(handle, out var id))
            {
                return GetGeology(id);
            }
            return null;
        }

        public ushort GetMaterialHandle(string id)
        {
            return _materialHandles.TryGetValue(id, out var handle) ? handle : (ushort)0;
        }

        public ushort GetGeologyHandle(string id)
        {
            return _geologyHandles.TryGetValue(id, out var handle) ? handle : (ushort)0;
        }

        public T? GetTuning<T>(string file, string path) where T : class
        {
            if (_tuning.TryGetValue(file, out var obj))
            {
                var token = obj.SelectToken(path);
                if (token != null)
                {
                    return token.ToObject<T>();
                }
            }
            return default;
        }
    }

    /// <summary>
    /// Material data from JSON per MATERIALS_SPEC.md.
    /// </summary>
    public class MaterialData
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("density_solid")]
        public float DensitySolid { get; set; }

        [JsonProperty("resists")]
        public ResistsData Resists { get; set; } = new();

        [JsonProperty("env")]
        public EnvData Env { get; set; } = new();

        [JsonProperty("struct")]
        public StructData Struct { get; set; } = new();

        [JsonProperty("work")]
        public WorkData? Work { get; set; }

        [JsonProperty("mana_conductivity")]
        public float ManaConductivity { get; set; }

        [JsonProperty("mana_capacity")]
        public float? ManaCapacity { get; set; }

        [JsonProperty("beautifulness")]
        public int Beautifulness { get; set; }

        [JsonProperty("valuebaleness")]
        public float Valuebaleness { get; set; }

        [JsonProperty("thermo")]
        public ThermoData? Thermo { get; set; }
    }

    public class ResistsData
    {
        [JsonProperty("bash")] public float Bash { get; set; }
        [JsonProperty("cut")] public float Cut { get; set; }
        [JsonProperty("stab")] public float Stab { get; set; }
        [JsonProperty("acid")] public float Acid { get; set; }
        [JsonProperty("fire")] public float Fire { get; set; }
        [JsonProperty("cold")] public float Cold { get; set; }
        [JsonProperty("electric")] public float Electric { get; set; }
        [JsonProperty("arcane")] public float Arcane { get; set; }
    }

    public class EnvData
    {
        [JsonProperty("insulation")] public float Insulation { get; set; }
        [JsonProperty("waterproof")] public float Waterproof { get; set; }
        [JsonProperty("breathability")] public float Breathability { get; set; }
        [JsonProperty("chem_protect")] public float ChemProtect { get; set; }
    }

    public class StructData
    {
        [JsonProperty("durability")] public float Durability { get; set; }
        [JsonProperty("rigidity")] public float Rigidity { get; set; }
    }

    public class WorkData
    {
        [JsonProperty("forgeable")] public bool Forgeable { get; set; }
        [JsonProperty("weldable")] public bool Weldable { get; set; }
        [JsonProperty("carveable")] public bool Carveable { get; set; }
    }

    public class ThermoData
    {
        [JsonProperty("ignition_point_c")] public float? IgnitionPointC { get; set; }
        [JsonProperty("melt_point_c")] public float? MeltPointC { get; set; }
    }

    /// <summary>
    /// Geology/terrain data from JSON.
    /// </summary>
    public class GeologyData
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("material")]
        public string Material { get; set; } = "";

        [JsonProperty("terrain_bits")]
        public TerrainBitsData TerrainBits { get; set; } = new();

        [JsonProperty("display")]
        public DisplayData Display { get; set; } = new();

        [JsonProperty("properties")]
        public PropertiesData Properties { get; set; } = new();
    }

    public class TerrainBitsData
    {
        [JsonProperty("kind")]
        public string Kind { get; set; } = "OpenNoFloor";

        [JsonProperty("natural")]
        public bool Natural { get; set; } = true;

        [JsonProperty("support_flag")]
        public bool SupportFlag { get; set; }

        [JsonProperty("ramp_dir")]
        public string? RampDir { get; set; }

        // Accept runtime geology.json that encodes direction as an integer 0-7
        [JsonProperty("rampDirection")]
        public int? RampDirection { get; set; }
    }

    public class DisplayData
    {
        [JsonProperty("glyph")]
        public int Glyph { get; set; }

        [JsonProperty("foreground")]
        public ColorData Foreground { get; set; } = new();

        [JsonProperty("background")]
        public ColorData Background { get; set; } = new();

        [JsonProperty("autotile")]
        public AutotileData? Autotile { get; set; }
    }

    public class ColorData
    {
        [JsonProperty("r")] public int R { get; set; }
        [JsonProperty("g")] public int G { get; set; }
        [JsonProperty("b")] public int B { get; set; }
    }

    public class AutotileData
    {
        [JsonProperty("connect_groups")]
        public IList<string>? ConnectGroups { get; set; }

        [JsonProperty("connects_to")]
        public IList<string>? ConnectsTo { get; set; }

        [JsonProperty("variants")]
        public IDictionary<string, int>? Variants { get; set; }
    }

    public class PropertiesData
    {
        [JsonProperty("mineable")]
        public bool Mineable { get; set; }

        [JsonProperty("buildable")]
        public bool Buildable { get; set; }

        [JsonProperty("smoothable")]
        public bool Smoothable { get; set; }

        [JsonProperty("nav_cost_base")]
        public int NavCostBase { get; set; } = 10;

        [JsonProperty("opacity")]
        public int Opacity { get; set; }

        [JsonProperty("flammable")]
        public bool Flammable { get; set; }

        [JsonProperty("layer_depth")]
        public int? LayerDepth { get; set; }

        [JsonProperty("ore_chance")]
        public float? OreChance { get; set; }
    }

    public class ContentError
    {
        public string Pack { get; }
        public string File { get; }
        public string? Id { get; }
        public string Reason { get; }

        public ContentError(string pack, string file, string? id, string reason)
        {
            Pack = pack;
            File = file;
            Id = id;
            Reason = reason;
        }

        public override string ToString()
        {
            return Id != null
                ? $"[{Pack}/{File}] {Id}: {Reason}"
                : $"[{Pack}/{File}] {Reason}";
        }
    }
}
