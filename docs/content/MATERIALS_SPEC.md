MATERIALS_SPEC.md — v3 (Simplified, CDDA-style, with Arcane & Thermal)
id: material.v3.simplified
status: proposal (normative-on-accept)
owner: content/registry
last_updated: 2025-09-14
compat: auto-migrate from v1/v2

Current implementation note (2026-06-12):

- Current runtime data is loaded from `content/registries/materials.authoring.json` and `content/registries/materials.registry.json`.
- The active legality boundary is documented in [TILES_MATERIALS_ARCHITECTURE.md](TILES_MATERIALS_ARCHITECTURE.md): TerrainKind owns navigation legality; materials provide numeric modifiers only.
- `content/schemas/materials.registry.schema.json` is the machine-readable schema authority for the current registry format.
- Treat the detailed gameplay fields below as material-model design/reference unless confirmed against the current JSON schema and loaded registry.

0) Scope

A compact, intuitive material model for crafting/armor/UI:

Resists on a 0–100 scale (bash/cut/stab/acid/fire/cold/electric/arcane).

Environment comfort/protection (insulation/waterproof/breathability/chem_protect).

Structure (durability/rigidity) for wear, buildables, and encumbrance.

Mana: mana_conductivity (0–100) and optional mana_capacity.

Density-driven mass → used by encumbrance & movement.

Thermal kept but minimal: only ignition_point_c and melt_point_c thresholds for world hazards; workshops ignore temperature (they craft directly).

No continuous physics; all damage/coverage/penetration constants live in a separate /content/registries/tuning.damage.json (included below).

1) Field Definitions (Normative)

id: string — lowercase snake_case, unique.

tags: string[] — classification (e.g., metal, alloy, cloth, wood).

density_solid: number — kg/m³, used to derive item mass.

resists: object — numbers 0..100 (values >100 allowed as “extraordinary”):

bash, cut, stab, acid, fire, cold, electric, arcane

env: object

insulation (0..100), waterproof (0..100), breathability (0..100), chem_protect (0..100)

struct: object

durability (0..100), rigidity (0..100)

work: object (pure metadata for content gating; workshops do not require temperatures)

forgeable: bool, weldable: bool, carveable: bool

mana_conductivity: number (0..100), mana_capacity?: number (0..100)

beautifulness: int (0..100), valuebaleness: number (≥0)

thermo: object (simplified thresholds; optional)

ignition_point_c?: number — catches fire at/above this ambient °C

melt_point_c?: number — softens/melts at/above this °C

Determinism: All numbers are constants; no randomization at load. Invalid entries are skipped with structured errors; the game never crashes due to content.

2) Mass & Encumbrance (Where weight comes from)

Mass is derived per item from density_solid × volume (and an item’s thickness_factor), or overridden by the item’s fixed_mass_g when present.

Encumbrance is computed in gameplay using the item’s mass, material rigidity, and breathability (formula and weights come from tuning.damage.json → encumbrance block). Materials do not store encumbrance directly.

3) Damage Binding (Where the math lives)

Damage reduction uses the material’s resists[type] plus any item bonuses on the equipment prototype:

R_type = k_mat * material.resists[type] + k_item * item_bonus[type]
d_final = max(0, d_raw - clamp(0, cap_per_hit, R_type * penetration_modifier))


All constants (k_mat, k_item, caps, coverage and penetration curves, multi-layer falloff) are in /content/registries/tuning.damage.json (see concrete JSON below). Nothing is hardcoded.

4) Thermal (Ignite/Melt) — Very Simple

World hazards (lava, fire fields, furnaces) may set a tile ambient temperature.

If ambient_c ≥ ignition_point_c for a minimum duration, the carrier Ignites (burning DoT governed by damage tuning).

If ambient_c ≥ melt_point_c for a minimum duration, the item/material Melts/Softens (apply item state tags; no fluid sim).

Workshops do not check temperatures; recipes craft directly.

Durations are defined in tuning.damage.json → thermal.

5) JSON Schema (material.v3.schema.json)
{
  "$id": "material.v3.schema.json",
  "type": "object",
  "required": ["id", "tags", "density_solid", "resists", "env", "struct"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "density_solid": { "type": "number", "minimum": 1 },
    "resists": {
      "type": "object",
      "required": ["bash","cut","stab","acid","fire","cold","electric","arcane"],
      "properties": {
        "bash": { "type": "number", "minimum": 0, "maximum": 200 },
        "cut": { "type": "number", "minimum": 0, "maximum": 200 },
        "stab": { "type": "number", "minimum": 0, "maximum": 200 },
        "acid": { "type": "number", "minimum": 0, "maximum": 200 },
        "fire": { "type": "number", "minimum": 0, "maximum": 200 },
        "cold": { "type": "number", "minimum": 0, "maximum": 200 },
        "electric": { "type": "number", "minimum": 0, "maximum": 200 },
        "arcane": { "type": "number", "minimum": 0, "maximum": 200 }
      },
      "additionalProperties": false
    },
    "env": {
      "type": "object",
      "required": ["insulation","waterproof","breathability","chem_protect"],
      "properties": {
        "insulation": { "type": "number", "minimum": 0, "maximum": 100 },
        "waterproof": { "type": "number", "minimum": 0, "maximum": 100 },
        "breathability": { "type": "number", "minimum": 0, "maximum": 100 },
        "chem_protect": { "type": "number", "minimum": 0, "maximum": 100 }
      },
      "additionalProperties": false
    },
    "struct": {
      "type": "object",
      "required": ["durability","rigidity"],
      "properties": {
        "durability": { "type": "number", "minimum": 0, "maximum": 100 },
        "rigidity": { "type": "number", "minimum": 0, "maximum": 100 }
      },
      "additionalProperties": false
    },
    "work": {
      "type": "object",
      "properties": {
        "forgeable": { "type": "boolean" },
        "weldable": { "type": "boolean" },
        "carveable": { "type": "boolean" }
      },
      "additionalProperties": false
    },
    "mana_conductivity": { "type": "number", "minimum": 0, "maximum": 100 },
    "mana_capacity": { "type": "number", "minimum": 0, "maximum": 100 },
    "beautifulness": { "type": "integer", "minimum": 0, "maximum": 100 },
    "valuebaleness": { "type": "number", "minimum": 0 },
    "thermo": {
      "type": "object",
      "properties": {
        "ignition_point_c": { "type": "number", "minimum": 0 },
        "melt_point_c": { "type": "number", "minimum": 0 }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}

6) Example Materials (JSON, no comments)

Steel

{
  "id": "core_mat_metal_steel",
  "tags": ["metal","alloy"],
  "density_solid": 7800,
  "resists": { "bash": 65, "cut": 80, "stab": 75, "acid": 30, "fire": 70, "cold": 80, "electric": 30, "arcane": 20 },
  "env": { "insulation": 10, "waterproof": 50, "breathability": 0, "chem_protect": 25 },
  "struct": { "durability": 85, "rigidity": 85 },
  "work": { "forgeable": true, "weldable": true, "carveable": false },
  "mana_conductivity": 35,
  "mana_capacity": 10,
  "beautifulness": 55,
  "valuebaleness": 1.1,
  "thermo": { "ignition_point_c": null, "melt_point_c": 1450 }
}


Cotton

{
  "id": "core_mat_fiber_cotton",
  "tags": ["fiber","organic","cloth"],
  "density_solid": 1550,
  "resists": { "bash": 15, "cut": 10, "stab": 8, "acid": 10, "fire": 5, "cold": 25, "electric": 5, "arcane": 5 },
  "env": { "insulation": 35, "waterproof": 5, "breathability": 85, "chem_protect": 5 },
  "struct": { "durability": 25, "rigidity": 5 },
  "work": { "forgeable": false, "weldable": false, "carveable": true },
  "mana_conductivity": 5,
  "beautifulness": 35,
  "valuebaleness": 0.8,
  "thermo": { "ignition_point_c": 410, "melt_point_c": null }
}

7) Global Damage/Armor Tuning (JSON, no comments)

Place at: /content/registries/tuning.damage.json
(You can tweak values per pack/DLC/mod; engine reads these at boot.)

{
  "version": 1,
  "rng_jitter": 0,
  "k": { "mat": 0.1, "item": 1.0 },
  "caps": { "per_hit": 90, "min_damage": 0 },
  "coverage": { "curve": "step", "threshold_pct": 80 },
  "penetration": { "curve": "linear", "a": 1.0, "b": 0.0 },
  "layering": { "falloff": 0.6 },
  "arcane": { "k_mat": 0.1, "k_item": 1.0 },
  "encumbrance": { "w_mass": 0.6, "w_rigidity": 0.25, "w_breath": 0.15, "mass_ref_g": 4000, "thickness_mult": 0.5 },
  "thermal": { "ignite_min_seconds": 2, "melt_min_seconds": 5 }
}


Interpreting the tuning file (engine rules):

k.mat/k.item feed the resist→reduction formula; caps.per_hit clamps per-hit reduction.

coverage.curve="step" with threshold_pct uses a simple single-roll coverage gate (e.g., 80%).

penetration.curve="linear" can be swapped for "exp"/"poly" if you add parameters later.

layering.falloff scales the 2nd/3rd… armor layer effectiveness.

encumbrance block is used by the gameplay system when turning mass/rigidity/breathability into an item’s encumbrance.

thermal sets minimum exposure time above thresholds before Ignite or Melt states are applied.


MATERIALS_SPEC v4-min (Fixed-Point Edition)

Status: final (on accept)
Owner: content/registry
Last updated: 2025-10-03 (Australia/Sydney)
Scope: Materials authoring → compiled registry → runtime combat/crafting/valuation
Compatibility: Works with the existing Items v3 interface (“shape baseline × material multipliers × quality/skill/state”). The registry is generated by a compiler; do not hand-edit physical.* there.

0) Goals & Non-Goals

Goals

Minimal yet physical: one density + three mechanical knobs (edge-hardness, fracture-toughness, rigidity) + simple electric/magic behavior.

Unified math: weapons and armor both use the same material-→-multiplier pipeline.

Performance: integer-only, fixed-point math (no floats at runtime).

Predictable authoring: authors fill human-readable values; the compiler scales and validates.

Non-Goals

No calorimetry or thermal process simulation at the materials layer (no heat capacity loops).

No burn products (burned items are simply destroyed) to avoid JSON graph cycles.

No environment protection values here; coverage/lining/shape live on the item side.

1) Fixed-Point Policy (Performance-Critical)

Global fixed-point scale: FX = 10_000.
Example: 1.0000 → 10000, 0.25 → 2500, +30% → 13000.

All dimensionless inputs/outputs (0..100 scales, multipliers, resist multipliers, tuning constants) are represented as integers scaled by FX.
Only density remains in physical units.

Author inputs remain human-readable (e.g., 0..100, 1.0). The compiler scales to FX and writes cache/runtime buffers.

Density units: store as kg/m³ and mirror it as mg/mL for exact integer mass math:
1 kg/m³ == 1 mg/mL (numerically identical).

1.1 Suggested helper macros (C-like pseudocode)
const int FX = 10000;                          // 1.0000

inline int fx_one() { return FX; }
inline int fx_from_float(double x){ return (int) llround(x * FX); }
inline int fx_from_pct(double p){ return (int) llround((p / 100.0) * FX); } // 0..100 → 0..FX

inline int fx_mul(int a, int b){ return (int)(((long long)a * b + FX/2) / FX); } // rounded
inline int fx_div(int a, int b){ return (int)(((long long)a * FX + b/2) / b); }   // rounded

// Center a 0..FX value around 0: [-FX/2, +FX/2]
inline int fx_dev(int g_fx){ return g_fx - FX/2; }

// Normalize 0..FX to [-FX, +FX] for strong signals (optional):
inline int fx_norm_full(int g_fx){ return (int)((((long long)g_fx - FX/2) * 2)); }


Exponentials/roots: prefer LUTs + linear interpolation. If you need a 4th-root (x^0.25), two integer square-roots (isqrt(isqrt(...))) also work.

2) Authoring Data Model (Single Source of Truth)
// materials.authoring.json (v4-min)
MaterialCore {
  id: string;                         // unique id, snake_case
  tags: string[];                     // e.g., ["metal","wood","fabric","stone","mythic"]

  // Mass source (physical unit, NOT scaled):
  density_solid: number;              // kg/m³  (== mg/mL numerically)

  // Mechanics trio (author enters 0..100):
  hardness_edge: number;              // edgeability & edge retention proxy
  toughness_frac: number;             // anti-chipping / fracture toughness proxy
  rigidity: number;                   // stiffness/modulus proxy (handling, deflection)

  // Electricity & magic:
  electric_category: "conductor" | "insulator" | "semi";
  // Metal defaults to "conductor" → zero electric resistance on gear.

  mana_conductivity: number;          // 100 = neutral; >100 amplifies magic (no resist);
                                       // <100 attenuates magic and grants arcane resistance

  // Economy/Aesthetics (multipliers, author-friendly):
  value_mul?: number;                 // default 1.0
  beauty_mul?: number;                // default 1.0

  // Workability & processing difficulty (time/skill impacts live in crafting/mining systems):
  work?: {
    forgeable?: boolean;
    weldable?: boolean;
    carveable?: boolean;
    process_difficulty_mul?: number;  // default 1.0; >1 = slower/harder/higher skill
  };

  // Metadata only (tech tree / recipes / loot bands / UI filters):
  phase?: "pig" | "cast" | "wrought" | "steel" | "refined" | "mythic";
}


Removed vs legacy: no resists.{bash,cut,stab,acid,fire,cold}; no env.*; no waterproof here (handled by containers/items).

3) Compiler Output & Scaling Rules

For each authoring record:

hardness_edge_fx = fx_from_pct( clamp(hardness_edge, 0..200) )

toughness_frac_fx = fx_from_pct( clamp(toughness_frac, 0..200) )

rigidity_fx = fx_from_pct( clamp(rigidity, 0..200) )

mana_conductivity_fx = fx_from_pct( clamp(mana_conductivity, 0..200) )

value_mul_fx = fx_from_float( value_mul ?? 1.0 )

beauty_mul_fx = fx_from_float( beauty_mul ?? 1.0 )

process_difficulty_mul_fx = fx_from_float( work?.process_difficulty_mul ?? 1.0 )

density_mg_per_ml = round(density_solid) // exact integer mass math

Registry generation:

physical.density ← density_solid (or mirror as density_mg_per_ml).

Do not hand-write any other physical.* fields in the registry; everything else is computed at runtime or cached buffers.

4) Mass & Encumbrance (Integer)

Mass (grams)

mass_g = (density_mg_per_ml * base_volume_ml + 500) / 1000;  // rounded integer


Encumbrance multiplier M_enc_fx

Concept: M_enc = (mass / m_ref)^γ, defaults: m_ref = 1000 g, γ ≈ 0.30.

Runtime integer strategy: LUT + linear interpolation (recommended), or integer root/log approximations.

Example LUT (FX integers)

{
  "ratio_points_fx": [ 5000,  7500, 10000, 15000, 20000, 30000 ], // 0.5×..3.0×
  "value_points_fx": [ 8700,  9380, 10000, 11100, 11890, 12950 ]  // M_enc_fx
}


At runtime compute ratio_fx = fx_div(mass_g, m_ref_g), then interpolate to get M_enc_fx.

5) Electricity & Magic (Integer)

Electric resistance (FX)

if electric_category == "conductor" → R_electric_fx = 0
if electric_category == "insulator" → R_electric_fx = R_ele_max_fx
if electric_category == "semi"      → R_electric_fx = fx_mul(fx_from_float(0.5), R_ele_max_fx)


Magic amplification & arcane resistance

M_spell_fx = clamp(mana_conductivity_fx, FX*0.25, FX*2.0); // 0.25×..2×

if (mana_conductivity_fx < FX) {
  // portion below neutral turns into arcane resistance (capped):
  R_arcane_fx = fx_mul( fx_div(FX - mana_conductivity_fx, FX), R_arc_cap_fx );
} else {
  R_arcane_fx = 0;
}


Semantics:

100 (=FX) is neutral;

>100 amplifies magic but never grants resistance;

<100 attenuates and grants arcane resistance up to R_arc_cap_fx.

6) Weapons — Shape Baseline × Material Multipliers (All FX)

Inputs (from the item “shape baseline”):
D_base = { bash, cut, stab } (raw points), t_base_ms (attack time), hit_base (to-hit), plus reach/handling (shape only; materials do not change reach).

Material signals (centered around 50):

int H_fx = hardness_edge_fx;                 // 0..FX
int T_fx = toughness_frac_fx;                // 0..FX
int R_fx = rigidity_fx;                      // 0..FX

int Hc = fx_dev(H_fx); // [-FX/2, +FX/2]  (positive = above 50)
int Tc = fx_dev(T_fx);
int Rc = fx_dev(R_fx);

6.1 Attack-time multiplier M_t_fx

Concept: (mass/m_ref)^α * (1 - k_R * (rigidity-50)/50)

Integer A (preferred): LUT for pow(ratio, α) + linear interpolation, then multiply the rigidity term.

Integer B: if α = 0.25, inertia_fx = isqrt( isqrt(ratio_fx) ) in fixed-point.

M_t_fx = fx_mul(inertia_fx, (FX - fx_mul(k_R_fx, fx_div(Rc, FX/2))));
t_final_ms = (long long) t_base_ms * M_t_fx / FX;

6.2 Damage multipliers
// Bash: heavier + stiffer hits couple momentum better
M_bash_fx =
  FX
  + fx_mul(b_m_fx, fx_dev( fx_div(mass_g * FX, m_ref_g) ))  // mass deviation around 1.0
  + fx_mul(b_r_fx, fx_div(Rc, FX/2));

// Cut: edge hardness & stiffness help; brittleness hurts (only below 50)
int brittle_fx = max(0, FX/2 - T_fx); // shortfall below 50, in 0..FX/2
M_cut_fx =
  FX
  + fx_mul(c_h_fx, fx_div(Hc, FX/2))
  + fx_mul(c_r_fx, fx_div(Rc, FX/2))
  - fx_mul(c_b_fx, fx_div(brittle_fx, FX/2));

// Stab: edge and column stiffness
M_stab_fx =
  FX
  + fx_mul(s_h_fx, fx_div(Hc, FX/2))
  + fx_mul(s_r_fx, fx_div(Rc, FX/2));

6.3 To-hit multiplier (small effect from materials)
M_hit_fx =
  FX
  + fx_mul(h_r_fx, fx_div(Rc, FX/2))                   // stiffer = steadier
  - fx_mul(h_m_fx, fx_dev( fx_div(mass_g * FX, m_ref_g) )); // heavier = a bit harder to place

// Clamp to ±10% band in gameplay code if desired.

6.4 Final weapon stats
D_final.bash = (long long) D_base.bash * M_bash_fx / FX;
D_final.cut  = (long long) D_base.cut  * M_cut_fx  / FX;
D_final.stab = (long long) D_base.stab * M_stab_fx / FX;

t_final_ms   = (long long) t_base_ms   * M_t_fx    / FX;
hit_final    = (long long) hit_base    * M_hit_fx  / FX;


Design note: materials should not dominate to-hit; shape & skill do. The coefficients below keep M_hit within a ±10% envelope.

7) Armor — Material Resist Multipliers (FX) × Thickness × Shape

Item-side inputs:

thickness_factor (e.g., 0.85/1.00/1.25 → T_fx)

shape_bonus per type (e.g., Milanese curvature for stab, lamellar for cut) as flat points or FX multiplier depending on your combat integration.

Material-derived resist multipliers (FX):

R_cut_mat_fx  =
  FX
  + fx_mul(r_cut_hard_fx,  fx_div(Hc, FX/2))
  + fx_mul(r_cut_rigid_fx, fx_div(Rc, FX/2))
  - fx_mul(r_cut_brit_fx,  fx_div(max(0, FX/2 - T_fx), FX/2));

R_stab_mat_fx =
  FX
  + fx_mul(r_stab_hard_fx,  fx_div(Hc, FX/2))
  + fx_mul(r_stab_rigid_fx, fx_div(Rc, FX/2));

R_bash_mat_fx =
  FX
  + fx_mul(r_bash_tough_fx, fx_div(Tc, FX/2))
  + fx_mul(r_bash_rigid_fx, fx_div(Rc, FX/2))
  + fx_mul(r_bash_dense_fx, fx_div( fx_from_float(density_mg_per_ml) - fx_from_float(rho_ref_mg_per_ml),
                                    fx_from_float(rho_ref_mg_per_ml) ));


Combine with thickness & shape

R_type_points = (Base_R_type_points * R_type_mat_fx / FX);
R_type_points = (long long) R_type_points * T_fx / FX;
R_type_points = R_type_points + shape_bonus_points; // if you model shape as flat points


Then feed into your normal coverage / penetration (AP) / deflection curves (which should also use FX constants).

8) Workability & Processing Difficulty (FX)

Mining/Chopping/Quarrying time:
time = base_time * process_difficulty_mul_fx / FX;

Crafting time (multi-material):
craft_time = base_time * GM(process_difficulty_mul_fx[]) / FX;
(Use a geometric mean to keep one extreme material from fully dominating.)

Skill gates/checks:
DC += ((process_difficulty_mul_fx - FX) * skill_req_scale_fx) / FX;
(Or derive level requirements similarly; tune skill_req_scale_fx.)

forgeable/weldable/carveable are pure capability flags (recipe gates, tool reqs), not numeric multipliers.

9) Economy & Aesthetics (FX)
final_value  = (long long) base_value  * value_mul_fx  / FX;
final_beauty = (long long) base_beauty * beauty_mul_fx / FX;


Comfort/Rest effectiveness belongs to item shape baselines (beds, chairs), not materials. Beauty can be shown via beauty_mul_fx.

10) Optional Future Extensions (Documentation Only, No Data Now)
10.1 World/Fire system (thermals, still integer)

If someday needed at the world/fire layer, add thermal FX fields there (not in materials authoring):

specific_heat_solid_fx, specific_heat_liquid_fx, latent_heat_fx, heat_of_combustion_fx, flammability_class, etc.

No burn products. Items burned are removed outright to avoid JSON graph cycles.

10.2 Construction / Weathering / Deterioration

If needed later, handle at the construction & durability layers. You may feed process_difficulty_mul_fx as low-weight input; the main drivers should remain shape/structure.

11) Tuning Constants (All FX Integers)
{
  "FX": 10000,

  "mass_ref_g": 1000,
  "encumbrance_gamma_lut": {
    "ratio_points_fx": [ 5000,  7500, 10000, 15000, 20000, 30000 ],
    "value_points_fx": [ 8700,  9380, 10000, 11100, 11890, 12950 ]
  },

  "weapon": {
    "alpha_mass_time_fx":  2500,   // 0.25
    "k_rigidity_time_fx":  1000,   // 0.10

    "bash_mass_coeff_fx":  2500,   // 0.25
    "bash_rigid_coeff_fx": 1000,   // 0.10

    "cut_hard_coeff_fx":   2200,   // 0.22
    "cut_rigid_coeff_fx":  1000,   // 0.10
    "cut_brittle_pen_fx":   500,   // 0.05 (only when toughness<50)

    "stab_hard_coeff_fx":  1800,   // 0.18
    "stab_rigid_coeff_fx": 1400,   // 0.14

    "hit_rigid_coeff_fx":   500,   // 0.05
    "hit_mass_pen_fx":       500,  // 0.05

    "time_min_ms": 300,
    "time_max_ms": 3000
  },

  "armor": {
    "rho_ref_mg_per_ml": 7800,     // ≈ iron/steel density baseline
    "r_cut_hard_fx":     2500,     // 0.25
    "r_cut_rigid_fx":    1000,     // 0.10
    "r_cut_brit_fx":     1000,     // 0.10

    "r_stab_hard_fx":    1800,     // 0.18
    "r_stab_rigid_fx":   1400,     // 0.14

    "r_bash_tough_fx":   2200,     // 0.22
    "r_bash_rigid_fx":   1000,     // 0.10
    "r_bash_dense_fx":   1000      // 0.10
  },

  "electric": { "R_ele_max_fx": 10000 }, // 1.0
  "arcane":   { "R_arc_cap_fx":  3000 }, // +30% max arcane resistance
  "work":     { "skill_req_scale_fx": 3000 } // 0.3 → DC/level bump per difficulty
}

12) Validation & Error Handling

Required authoring fields:
id, tags, density_solid, hardness_edge, toughness_frac, rigidity, electric_category, mana_conductivity.

Ranges: Authoring 0..100 recommended; values >100 allowed for exotic materials (curves should have diminishing returns / caps in tuning).

Compiler: authoring → scaled FX integers; writes physical.density (or density_mg_per_ml).
Never allow hand-edited physical.* duplicates downstream.

On failure: skip offending entries, log (file, id, reason); loading continues.

13) Phase Taxonomy (Metadata Only)

phase guides tech tree, recipes, spawn bands, and UI filters. It does not affect combat or crafting math directly.

Examples:

wrought unlocks carburizing;

steel/refined unlocks temper/weld recipes;

mythic gates magical content/loot tiers.

14) Compatibility Pointers (Documentation-only)

RimWorld “Stuff Stat Factors”:
Our derived multipliers map conceptually to RW’s:

M_cut / M_stab / M_bash ⇔ Sharp/Blunt damage factors

M_t ⇔ Melee cooldown factor

R_type_mat ⇔ Armor material factors
Difference: we derive from hardness/toughness/rigidity/density rather than authoring dozens of per-stat factors.

CDDA materials:
We align on density and conductive semantics (ours is a category).
CDDA’s resist.{bash,cut,heat,bullet} and thermal fields are not stored here; in our model, physical resistances are derived, and thermals—if ever needed—live in the world/fire layer (with “burn → destroy”).

15) Conformance Checklist

 Authoring uses human-readable values; compiler scales to FX.

 No resists.* in materials; resist multipliers are derived.

 Registry contains physical.density only; no duplicate physical fields.

 Runtime math uses only integers (FX); LUTs for powers/roots if needed.

 Burned items are destroyed; no burn products defined in materials.

 Electric resistance from electric_category; magic from mana_conductivity.

 Work flags present; process_difficulty_mul flows into crafting/mining systems.

 Phase used solely for tech/recipes/loot/UI filters.

16) Reference Example (Authoring → FX → Runtime)

Authoring (human readable)

{
  "id": "core_mat_metal_steel_tempered",
  "tags": ["metal","steel"],
  "density_solid": 7850,
  "hardness_edge": 80,
  "toughness_frac": 85,
  "rigidity": 85,
  "electric_category": "conductor",
  "mana_conductivity": 100,
  "value_mul": 1.15,
  "beauty_mul": 1.05,
  "work": { "forgeable": true, "weldable": true, "carveable": false, "process_difficulty_mul": 1.20 },
  "phase": "steel"
}


Compiler scaling (examples)

hardness_edge_fx = 8000

toughness_frac_fx = 8500

rigidity_fx = 8500

mana_conductivity_fx = 10000

value_mul_fx = 11500

beauty_mul_fx = 10500

process_difficulty_mul_fx = 12000

density_mg_per_ml = 7850

Runtime (mass for a 900 mL short sword)

mass_g = (7850 * 900 + 500) / 1000 = 7070 g


Then plug into §6/§7 multipliers with item baselines.
