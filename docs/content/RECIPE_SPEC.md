Scope
This spec defines the data model, validation rules, and runtime semantics for recipes: craft/smelting/assembly processes that convert inputs into outputs at a workshop (or domain). It unifies the fields used in your current JSON (metallurgy, metalworks, firearms, crafts) under a single, stable contract, and documents backwards-compatible aliases and loader behavior. Examples in your content show requires_enablers, attachments_required, inputs_or, inherit_material_from, conditional_byproducts, domain, and affinity, all of which are accounted for here. 

core_workshop_metallurgy_smelte…

 

core_workshop_metalworks

 

core_workshop_metalworks

 

recipes

 

core_workshop_crafts

1) Core Concepts

Recipe = deterministic transformation executed by a workshop (explicit) or a domain (abstract skill area that any compatible workshop can host). Your current data shows both styles (workshop_id in metallurgy/metalworks; workshop in firearms; domain in generic recipes). Exactly one of {workshop_id, domain} must be present after normalization. 

core_workshop_metallurgy_smelte…

 

core_workshop_metalworks

 

core_workshop_firearms

 

recipes

Enablers / Attachments = station modules required to run a recipe; metal and chem sets use requires_enablers, while firearms/crafts use attachments_required. The loader normalizes both into requires_enablers (all-of). 

core_workshop_metallurgy_smelte…

 

core_workshop_firearms

 

core_workshop_crafts

Inputs support fixed lists and alternatives: inputs (AND) plus inputs_or (choose-one sets), as seen in metalworks. Ingredient pools by tag/category are supported via ingredient_pools in crafts. 

core_workshop_metalworks

 

core_workshop_metalworks

 

core_workshop_crafts

Outputs can inherit material from the primary input and optionally add quality tags; both appear in metalworks and crafts. 

core_workshop_metalworks

 

core_workshop_crafts

Byproducts / Conditional byproducts depend on extra enablers (e.g., gas scrubbers) and are present across metallurgy recipes. 

core_workshop_metallurgy_smelte…

 

core_workshop_metallurgy_smelte…

 

core_workshop_metallurgy_smelte…

Era Gating via era across families: CLASSIC → MEDIEVAL → RENAISSANCE. 

core_workshop_metallurgy_smelte…

Flags such as requires_heat exist on forge-like metal recipes. 

core_workshop_metalworks

Affinity is a recipe-level tuning (fuel/time multipliers), visible in generic recipes.json. 

recipes

2) Deterministic Execution & Selection

Variant Resolution

If inputs_or has multiple viable choices, pick the first viable set by stable sort of material IDs (ascending) unless a tagged pool is in use; then use the pool’s internal stable order (IDs ascending). This mirrors crafts’ ingredient_pools usage and keeps ticks deterministic across threads. 

core_workshop_crafts

Conditional Byproducts

Evaluate conditional_byproducts after outputs; include only those whose requires_enablers ⊆ station attachments (normalized enablers). This matches your smeltery patterns. 

core_workshop_metallurgy_smelte…

 

core_workshop_metallurgy_smelte…

Material Inheritance

When inherit_material_from: "primary_input" is set, the first material-bearing item in the resolved inputs_or (or inputs if none) defines the output’s material. If multiple items in that set are present, use the first by ID order. 

core_workshop_metalworks

Era & Enabler Checks

A recipe is runnable iff era ≤ current tech era and all required enablers exist (after normalization). Metallurgy examples rely on this. 

core_workshop_metallurgy_smelte…

Flags

requires_heat: the job requests heat from the station; if heat unavailable, the job fails fast (no partial input consumption). Seen in metalworks. 

core_workshop_metalworks

3) Validation Rules (Loader)

ID: non-empty, globally unique.

Anchoring: exactly one of workshop_id or domain.

Era: enum CLASSIC|MEDIEVAL|RENAISSANCE.

Duration: duration_s > 0.

Enablers: arrays of string IDs; normalized field is requires_enablers (all-of), with optional any_enablers (any-of).

Inputs/Outputs: at least one output; inputs use count (items), charges_g, or charges_ml.

Determinism: when both inputs and inputs_or exist, resolve inputs_or first; then merge inputs (AND).

Byproducts: byproducts and conditional_byproducts are arrays of the same OutputSpec.

Output Mods: optional inherit_material_from, add_quality_tag, or output_mods (copy_from_input, add_tags, name_suffix) from crafts. 

core_workshop_crafts

Back-Compat Aliases (normalized by loader)

workshop → workshop_id (firearms/crafts → unified) 

core_workshop_firearms

attachments_required → requires_enablers (all-of) 

core_workshop_crafts

enablers (in generic recipes) → requires_enablers 

recipes

4) JSON Schema (v3)
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "recipe.v3.schema.json",
  "title": "Recipe",
  "type": "object",
  "required": ["id", "duration_s", "outputs"],
  "properties": {
    "id": { "type": "string", "minLength": 1 },
    "workshop_id": { "type": "string" },
    "domain": { "type": "string" },
    "era": { "type": "string", "enum": ["CLASSIC", "MEDIEVAL", "RENAISSANCE"] },
    "duration_s": { "type": "integer", "minimum": 1 },

    "requires_enablers": { "type": "array", "items": { "type": "string" } },
    "any_enablers": { "type": "array", "items": { "type": "string" } },

    "flags": { "type": "array", "items": { "type": "string", "enum": ["requires_heat"] } },

    "inputs": {
      "type": "array",
      "items": { "$ref": "#/definitions/Ingredient" }
    },
    "inputs_or": {
      "type": "array",
      "items": {
        "type": "array",
        "items": { "$ref": "#/definitions/Ingredient" },
        "minItems": 1
      }
    },
    "ingredient_pools": {
      "type": "array",
      "items": { "$ref": "#/definitions/IngredientPool" }
    },

    "outputs": {
      "type": "array",
      "minItems": 1,
      "items": { "$ref": "#/definitions/OutputSpec" }
    },
    "byproducts": {
      "type": "array",
      "items": { "$ref": "#/definitions/OutputSpec" }
    },
    "conditional_byproducts": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["requires_enablers", "outputs"],
        "properties": {
          "requires_enablers": { "type": "array", "items": { "type": "string" } },
          "outputs": {
            "type": "array",
            "items": { "$ref": "#/definitions/OutputSpec" }
          }
        }
      }
    },

    "affinity": {
      "type": "object",
      "properties": {
        "fuel_cost": { "type": "number" },
        "time_cost": { "type": "number" }
      },
      "additionalProperties": false
    },

    "tags": { "type": "array", "items": { "type": "string" } },

    "notes": { "type": "string" }
  },

  "oneOf": [
    { "required": ["workshop_id"], "not": { "required": ["domain"] } },
    { "required": ["domain"], "not": { "required": ["workshop_id"] } }
  ],

  "definitions": {
    "Ingredient": {
      "type": "object",
      "required": ["id"],
      "properties": {
        "id": { "type": "string" },
        "count": { "type": "integer", "minimum": 1 },
        "charges_g": { "type": "integer", "minimum": 1 },
        "charges_ml": { "type": "integer", "minimum": 1 }
      },
      "oneOf": [
        { "required": ["count"] },
        { "required": ["charges_g"] },
        { "required": ["charges_ml"] }
      ]
    },

    "OutputSpec": {
      "type": "object",
      "required": ["id"],
      "properties": {
        "id": { "type": "string" },
        "count": { "type": "integer", "minimum": 1 },
        "charges_g": { "type": "integer", "minimum": 1 },
        "charges_ml": { "type": "integer", "minimum": 1 },

        "inherit_material_from": { "type": "string", "enum": ["primary_input"] },
        "add_quality_tag": { "type": "string" },
        "output_mods": {
          "type": "object",
          "properties": {
            "copy_from_input": { "type": "string" },
            "add_tags": { "type": "array", "items": { "type": "string" } },
            "name_suffix": { "type": "string" }
          },
          "additionalProperties": false
        }
      }
    },

    "IngredientPool": {
      "type": "object",
      "required": ["pool_id", "accepts_tags", "min"],
      "properties": {
        "pool_id": { "type": "string" },
        "accepts_tags": { "type": "array", "items": { "type": "string" } },
        "min": { "type": "integer", "minimum": 0 }
      }
    }
  },

  "additionalProperties": false
}

5) Examples
5.1 Workshop-anchored with conditional byproducts (metallurgy)
{
  "id": "core_recipe_shaft_pig_iron_from_hematite_m",
  "workshop_id": "core_workshop_metallurgy_smeltery",
  "era": "MEDIEVAL",
  "duration_s": 2400,
  "requires_enablers": ["core_attach_furnace_shaft", "core_attach_power_bellows_water"],
  "inputs": [
    { "id": "core_item_ore_iron_hematite", "count": 10 },
    { "id": "core_item_powder_charcoal", "charges_g": 3000 },
    { "id": "core_item_rock_limestone_flux", "count": 10 }
  ],
  "outputs": [{ "id": "core_item_ingot_iron_pig", "count": 23 }],
  "byproducts": [{ "id": "core_item_waste_chem_slag", "count": 10 }],
  "conditional_byproducts": [
    {
      "requires_enablers": ["core_attach_gas_scrubber_pit"],
      "outputs": [{ "id": "core_item_liquid_acid_sulfuric_dilute", "charges_ml": 2000 }]
    }
  ]
}


(Reflects your smeltery files.) 

core_workshop_metallurgy_smelte…

5.2 Alternatives & material inheritance (metalworks)
{
  "id": "core_recipe_toolset_mining_c",
  "workshop_id": "core_workshop_metalworks",
  "era": "CLASSIC",
  "duration_s": 720,
  "requires_enablers": ["core_attach_metal_forge_hand_anvil"],
  "flags": ["requires_heat"],
  "inputs_or": [[
    { "id": "core_item_ingot_iron_wrought", "count": 2 },
    { "id": "core_item_ingot_bronze", "count": 2 },
    { "id": "core_item_ingot_steel", "count": 2 },
    { "id": "core_item_ingot_steel_refined", "count": 2 }
  ]],
  "inputs": [
    { "id": "core_item_powder_charcoal", "charges_g": 1000 },
    { "id": "core_item_liquid_water_pure", "charges_ml": 1000 }
  ],
  "outputs": [
    { "id": "core_item_toolset_mining_classic", "count": 1, "inherit_material_from": "primary_input" }
  ]
}


(As in your metalworks content.) 

core_workshop_metalworks

5.3 Domain-anchored with affinity (generic)
{
  "id": "core_recipe_glass_cullet_remelt",
  "domain": "glass",
  "duration_s": 1200,
  "inputs": [{ "id": "core_item_glass_cullet", "count": 10 }],
  "outputs": [{ "id": "core_item_glass_batch", "charges_g": 11000 }],
  "affinity": { "fuel_cost": -0.15, "time_cost": -0.10 }
}


(From your recipes.json.) 

recipes

5.4 Ingredient pools & output mods (crafts)
{
  "id": "core_recipe_crafts_decorate_item_gild_R",
  "workshop_id": "core_workshop_crafts",
  "era": "RENAISSANCE",
  "requires_enablers": ["core_attach_crafts_inlay_bench", "core_attach_crafts_polish_wheel"],
  "duration_s": 200,
  "inputs": [
    { "id": "core_item_target_decorable_basic", "count": 1 },
    { "id": "core_item_powder_gold", "count": 1 },
    { "id": "core_item_glue_animal", "count": 1 }
  ],
  "outputs": [{
    "id": "core_item_target_decorated_gilt",
    "count": 1,
    "output_mods": {
      "copy_from_input": "core_item_target_decorable_basic",
      "add_tags": ["gilt", "decorated", "unique_instance"],
      "name_suffix": " (Gilt Trim)"
    }
  }],
  "ingredient_pools": [{
    "pool_id": "core_item_target_decorable_basic",
    "accepts_tags": ["weapon", "armor", "clothing", "amulet", "goblet", "figurine", "tool"],
    "min": 0
  }]
}


(From crafts set, normalized field names.) 

core_workshop_crafts

6) Runtime Semantics

Consumption/Production: All inputs are atomically reserved then consumed; outputs/byproducts are created together. If any step fails (heat/enabler/stock), the job aborts with no partial consumption (idempotent retry-safe).

Attachment Effects: Station attachments can expose throughput modifiers (e.g., throughput_mult), applied multiplicatively to duration_s after affinity. Firearms attachments show such effects. 

core_workshop_firearms

Scheduling: work systems should preserve deterministic recipe intake and `inputs_or` choice ordering (see WORK_AND_JOBS_SYSTEM.md).

7) Error Handling (Crash-Proofing)

Loader wraps each file in a try/catch:

Parse → normalize aliases → validate → register.

On failure: record a structured error (id, path, reason), skip the bad recipe, continue.

Runtime wraps each job tick in try/catch:

On exception: cancel the job, release reservations, push a user-visible log entry, and mark the recipe instance as FAILED_RETRIABLE unless validation indicates permanent issues.

8) Normalization & Migration
Legacy field	Normalized to	Notes
workshop	workshop_id	Firearms/crafts → unified. 

core_workshop_firearms


attachments_required	requires_enablers	All-of semantics. 

core_workshop_firearms


enablers	requires_enablers	From recipes.json. 

recipes

Unit rules: keep count (items), charges_g (mass), charges_ml (volume). No uses unit.

9) Deterministic Mod Interop

Load Order: base → DLCs → mods; later definitions may override by id.

Merging:

Replace-by-id for whole recipe unless "merge": "patch" is declared (future).

Patch must preserve determinism: arrays (inputs_or, conditional_byproducts) patched by stable keyed union (id or tuple) to avoid order drift.

10) Minimal Loader Pseudocode
foreach (var file in ContentFiles("**/recipes*.json"))
{
    try {
        var raw = Json.Parse<List<Recipe>>(file);
        foreach (var r in raw)
        {
            NormalizeAliases(r);       // workshop→workshop_id, attachments_required→requires_enablers, enablers→requires_enablers
            ValidateAgainstSchema(r);  // recipe.v3.schema.json
            Register(r);               // overwrite by id (base→DLC→mod)
        }
    } catch (Exception ex) {
        Log.ContentError(file, ex);
    }
}

11) Test Vectors (CI)

Alt choice determinism: two valid inputs_or sets → always pick lexicographically first by ID. 

core_workshop_metalworks

Conditional byproduct gating: add/remove scrubber → sulfuric acid byproduct flips accordingly. 

core_workshop_metallurgy_smelte…

Output inheritance: material of arrowheads/parts derived from selected ingot. 

core_workshop_metalworks

Crafts pools: any valid decorable target passes via pool tags → output mods copy base item + add tags/suffix. 

core_workshop_crafts

12) Examples in Your Content (for reference)

Smeltery recipes with requires_enablers, byproducts, conditional byproducts. 

core_workshop_metallurgy_smelte…

Metalworks with inputs_or, flags: ["requires_heat"], inherit_material_from. 

core_workshop_metalworks

Firearms with attachments_required & workshop alias. 

core_workshop_firearms

Crafts with ingredient_pools and output_mods. 

core_workshop_crafts

Appendix: Field Reference

id: unique string.

workshop_id or domain: anchor.

era: production tech tier.

duration_s: base time (pre-affinity, pre-attachments).

requires_enablers / any_enablers: station attachments/modules.

flags: special runtime conditions (requires_heat).

inputs: fixed ingredient list (AND).

inputs_or: list of alternative sets (XOR across sets, AND within a set).

ingredient_pools: tag-based selection envelopes.

outputs: produced items/fluids; may inherit material/quality or apply output_mods.

byproducts / conditional_byproducts: additional outputs, possibly gated by enablers.

affinity: recipe tuning (relative cost/time).

This spec makes Codex/Claude-friendly loaders trivial, preserves your existing content, and guarantees deterministic, crash-proof execution across threads.
