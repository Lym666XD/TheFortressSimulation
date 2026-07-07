using System;
using System.Collections.Generic;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Material definition per MATERIALS_SPEC v4-min (Fixed-Point Edition).
/// This is the RUNTIME representation with FX integers.
/// Authoring uses human-readable values; compiler scales to FX.
/// </summary>
public class MaterialDefinition
{
    /// <summary>
    /// Runtime numeric ID (assigned by MaterialRegistry on load)
    /// Used for fast lookups and compact storage in tiles/items
    /// </summary>
    public ushort Id { get; set; }

    /// <summary>
    /// Authoring string ID (from JSON, e.g. "core_mat_metal_iron")
    /// Used for content references and human readability
    /// </summary>
    public string StringId { get; set; } = "";

    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public string Category { get; set; } = ""; // Inferred from tags, for compatibility
    public HashSet<string> Tags { get; set; } = new();

    // Display properties (UI only, not part of v4 mechanics)
    public DisplayProperties Display { get; set; } = new();

    // === CORE v4-min FIELDS (all FX integers except density) ===

    /// <summary>
    /// Mass source: kg/m³ (== mg/mL numerically)
    /// This is the ONLY field stored in physical units (not FX)
    /// </summary>
    public int DensitySolid { get; set; } = 1000; // kg/m³

    /// <summary>
    /// Core mechanics trio (FX integers, 0..100 authoring to 0..FX runtime)
    /// </summary>
    public MaterialMechanics Mechanics { get; set; } = new();

    /// <summary>
    /// Electricity and magic behavior (FX integers)
    /// </summary>
    public MaterialElectricMagic ElectricMagic { get; set; } = new();

    /// <summary>
    /// Economy and aesthetics (FX multipliers)
    /// </summary>
    public MaterialEconomy Economy { get; set; } = new();

    /// <summary>
    /// Workability and processing difficulty (FX integers)
    /// </summary>
    public MaterialWork Work { get; set; } = new();

    /// <summary>
    /// Tech tree metadata (not used in runtime calculations)
    /// </summary>
    public string? Phase { get; set; } // pig, cast, wrought, steel, refined, mythic

    // === NAVIGATION (per MATERIALS_DATA_CONTRACT) ===
    // Materials provide ONLY numeric modifiers, never legality
    public MaterialNavigation Navigation { get; set; } = new();

    // Validation
    public void Validate()
    {
        if (Id == 0 && StringId != "none")
            throw new InvalidOperationException($"Material '{StringId}' has invalid ID 0 (reserved for 'none')");

        if (string.IsNullOrWhiteSpace(StringId))
            throw new InvalidOperationException("Material must have a string ID");

        if (!System.Text.RegularExpressions.Regex.IsMatch(StringId, "^[a-z][a-z0-9_]*$"))
            throw new InvalidOperationException($"Material ID '{StringId}' must be lowercase alphanumeric with underscores");

        if (DensitySolid < 1)
            throw new InvalidOperationException($"Material '{Name}' has invalid density {DensitySolid}");

        // Validate mechanics are in reasonable ranges
        Mechanics.Validate(Name);
    }
}

/// <summary>
/// Core mechanical properties (all FX integers)
/// Per SPEC section 2: hardness_edge, toughness_frac, rigidity
/// </summary>
public class MaterialMechanics
{
    /// <summary>
    /// Edge hardness (edgeability and edge retention proxy)
    /// Authoring: 0..100 (can go greater than 100 for exotic materials)
    /// Runtime: FX integer (0..20000 typical, 10000 = 1.0 = 100 percent)
    /// </summary>
    public int HardnessEdgeFx { get; set; } = 5000; // default 50

    /// <summary>
    /// Fracture toughness (anti-chipping proxy)
    /// Authoring: 0..100 (can go greater than 100)
    /// Runtime: FX integer
    /// </summary>
    public int ToughnessFracFx { get; set; } = 5000; // default 50

    /// <summary>
    /// Rigidity/stiffness (modulus proxy, handling, deflection)
    /// Authoring: 0..100 (can go greater than 100)
    /// Runtime: FX integer
    /// </summary>
    public int RigidityFx { get; set; } = 5000; // default 50

    public void Validate(string materialName)
    {
        // Allow exotic values up to 200% but warn if extreme
        if (HardnessEdgeFx < 0 || HardnessEdgeFx > FixedPoint.FX * 3)
            throw new InvalidOperationException($"Material '{materialName}' hardness_edge out of range: {HardnessEdgeFx}");
        if (ToughnessFracFx < 0 || ToughnessFracFx > FixedPoint.FX * 3)
            throw new InvalidOperationException($"Material '{materialName}' toughness_frac out of range: {ToughnessFracFx}");
        if (RigidityFx < 0 || RigidityFx > FixedPoint.FX * 3)
            throw new InvalidOperationException($"Material '{materialName}' rigidity out of range: {RigidityFx}");
    }

    /// <summary>
    /// Get centered signals for damage calculations (per SPEC section 6)
    /// Returns values in range where 0 is neutral (50 percent)
    /// </summary>
    public (int Hc, int Tc, int Rc) GetCenteredSignals()
    {
        return (
            FixedPoint.Dev(HardnessEdgeFx),
            FixedPoint.Dev(ToughnessFracFx),
            FixedPoint.Dev(RigidityFx)
        );
    }
}

/// <summary>
/// Electric and magic behavior (all FX integers)
/// Per SPEC section 5
/// </summary>
public class MaterialElectricMagic
{
    /// <summary>
    /// Electric category (determines resistance)
    /// conductor = 0 resistance, insulator = max resistance, semi = half resistance
    /// </summary>
    public ElectricCategory ElectricCategory { get; set; } = ElectricCategory.Semi;

    /// <summary>
    /// Mana conductivity (FX integer)
    /// Authoring: 0..200 (100 = neutral)
    /// Runtime: FX (10000 = neutral, less than 10000 gives arcane resist, greater than 10000 amplifies magic)
    /// </summary>
    public int ManaConductivityFx { get; set; } = FixedPoint.FX; // default 100 (neutral)

    /// <summary>
    /// Compute electric resistance (FX) based on category
    /// Per SPEC section 5
    /// </summary>
    public int GetElectricResistFx(int rEleMaxFx)
    {
        return ElectricCategory switch
        {
            ElectricCategory.Conductor => 0,
            ElectricCategory.Insulator => rEleMaxFx,
            ElectricCategory.Semi => FixedPoint.Mul(FixedPoint.FromFloat(0.5), rEleMaxFx),
            _ => 0
        };
    }

    /// <summary>
    /// Compute spell amplification multiplier (FX)
    /// Per SPEC section 5: clamped between 0.25x and 2.0x
    /// </summary>
    public int GetSpellAmplificationFx()
    {
        return FixedPoint.Clamp(ManaConductivityFx, FixedPoint.FX / 4, FixedPoint.FX * 2);
    }

    /// <summary>
    /// Compute arcane resistance (FX)
    /// Per SPEC section 5: If mana less than FX, converts deficit into resistance
    /// </summary>
    public int GetArcaneResistFx(int rArcCapFx)
    {
        if (ManaConductivityFx >= FixedPoint.FX)
            return 0; // no resistance when amplifying

        // portion below neutral turns into arcane resistance (capped)
        int deficitFx = FixedPoint.FX - ManaConductivityFx;
        return FixedPoint.Mul(FixedPoint.Div(deficitFx, FixedPoint.FX), rArcCapFx);
    }
}

public enum ElectricCategory
{
    Conductor,
    Insulator,
    Semi
}

/// <summary>
/// Economy and aesthetics (all FX multipliers)
/// Per SPEC section 9
/// </summary>
public class MaterialEconomy
{
    /// <summary>
    /// Value multiplier (FX)
    /// Authoring: default 1.0
    /// Runtime: FX (10000 = 1.0×)
    /// </summary>
    public int ValueMulFx { get; set; } = FixedPoint.FX;

    /// <summary>
    /// Beauty multiplier (FX)
    /// Authoring: default 1.0
    /// Runtime: FX
    /// </summary>
    public int BeautyMulFx { get; set; } = FixedPoint.FX;
}

/// <summary>
/// Workability and processing difficulty (all FX)
/// Per SPEC section 8
/// </summary>
public class MaterialWork
{
    /// <summary>
    /// Capability flags (recipe gates, not numeric)
    /// </summary>
    public bool Forgeable { get; set; } = false;
    public bool Weldable { get; set; } = false;
    public bool Carveable { get; set; } = false;

    /// <summary>
    /// Processing difficulty multiplier (FX)
    /// Authoring: default 1.0; greater than 1 means slower/harder/higher skill
    /// Runtime: FX (10000 = 1.0x, 15000 = 1.5x harder)
    /// Affects mining/chopping/crafting time and skill DC
    /// </summary>
    public int ProcessDifficultyMulFx { get; set; } = FixedPoint.FX;
}

/// <summary>
/// Navigation modifiers (per MATERIALS_DATA_CONTRACT)
/// Materials provide ONLY numeric modifiers, NEVER legality booleans
/// </summary>
public class MaterialNavigation
{
    /// <summary>
    /// Additive move cost modifier (integer, not FX)
    /// Recommended range: -50..+50
    /// </summary>
    public int MoveCostAdd { get; set; } = 0;

    /// <summary>
    /// Friction multiplier (FX)
    /// Recommended range: ~8000..12000 (0.8× to 1.2×)
    /// </summary>
    public int FrictionMulFx { get; set; } = FixedPoint.FX;

    /// <summary>
    /// Hazard level (integer 0..10)
    /// </summary>
    public int HazardLevel { get; set; } = 0;

    /// <summary>
    /// Hazard type (optional if hazardLevel = 0)
    /// heat, cold, acid, shock, miasma, etc.
    /// </summary>
    public string HazardType { get; set; } = "none";
}

// === DISPLAY (UI only) ===

public class DisplayProperties
{
    public char Glyph { get; set; } = '?';
    public ConsoleColor Foreground { get; set; } = ConsoleColor.Gray;
    public ConsoleColor? Background { get; set; }
    public (byte R, byte G, byte B)? ParticleColor { get; set; }
}
