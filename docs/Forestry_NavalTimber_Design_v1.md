# Forestry & Naval Timber — Design Doc (C/M/R)  
**Version:** 1.0 • **Status:** Ready for implementation • **Authoring:** ChatGPT (design consolidation)  
**Scope:** Forestry fuel chain (charcoal), structural timber grading, and naval timber standards.  
**Out-of-scope (explicitly deferred):** land tenure/policies, coppice cycles, pastoral routes, taxation, Columbian crops.  
**New Stub (deferred):** Water‑Powered Sawmill / Water Saw (spec included, implementation later).

---

## 1) Boundaries & Goals
- Keep systems **lightweight** and **data‑driven**: items → recipes → buildings → hooks.
- **No policy/event mechanics**. Focus on materials, throughput, and unlocks.
- Ensure **clean docking** with Metallurgy/Glass via fuel quality and ash by‑products.
- Introduce **Naval Timber grades** as *materials only* (no shipbuilding yet).

---

## 2) Era → Process Chain → Inputs/Outputs → Unlocks

### Classical (C)
**Forestry — Charcoal (basic)**
- Chain: Felling → Gathering → **Charcoal Clamp (earth pit)**  
- IO: Wood (generic) ×10 → **Charcoal** ×8 + Wood Ash ×1  
- Unlocks: Logging Site (C), **Charcoal Clamp (C)**, Ash Pile (C)

**Forestry — Select Felling (proto‑grading)**
- Chain: **Select Felling** (no forest types)  
- IO: Oak Log ×1 → **Structural Timber · Oak (normal)** ×1  
      Pine Log ×1 → **Structural Timber · Pine (normal)** ×1  
- Unlocks: **Selection Bench (C)** (normal grade only)

---

### Medieval (M)
**Charcoal Upgrade**
- Chain: Felling → Gathering → **Beehive/Dome Kiln**  
- IO: Wood (generic/rod) ×10 → **Charcoal** ×10 + Wood Ash ×1  
- Unlocks: **Charcoal Kiln (M)**, Charcoal Tools (M)

**Seasoning & Sawing (pre‑treatment)**
- Chain: **Seasoning Rack** → **Hand Saw Bench**  
- IO: Structural Timber ×1 → **Seasoned Structural Timber** ×1 (long time; +quality flag)  
      Seasoned Structural Timber ×1 → **Boards** ×3  
- Unlocks: **Seasoning Rack (M)**, Hand Saw Bench (M)

> Note: No powered saw here to keep complexity low; boards remain optional.

---

### Renaissance (R)
**Naval Timber — Grading (materials only)**
- Chain: **Species ID → Seasoning flag → Visual/Grain grading**  
- IO (probabilistic):  
  - Oak (normal/seasoned) ×N → **Naval‑Grade Structural · Oak (Keel/Frame)** ×1 (10–20%) + normal oak ×(N−1)  
  - Pine (normal/seasoned) ×N → **Mast‑Grade Structural · Pine** ×1 (10–20%) + normal pine ×(N−1)  
- Modifiers: “Seasoned” +5% chance; Long‑length input +5% (cap 20%)  
- Unlocks: **Naval Timber Gauge (R)**, **Long‑Stock Yard (R)**

**Charcoal — High Purity (metallurgy hook)**
- IO: Rodwood ×12 → **High‑Purity Charcoal** ×10 + Wood Ash ×1  
- Unlocks: **Charcoal Kiln (R mode)**  
- Hook: Metallurgy recipes may grant **+10% batch** or **−1 fuel** or **−5% impurity** when using High‑Purity Charcoal (pick ONE per recipe to keep balance simple).

---

## 3) Items (canonical IDs)
- Logs: `log_oak`, `log_pine`, `log_mixed`
- Structural (normal): `timber_oak`, `timber_pine`, `timber_mixed`
- Structural (seasoned): `timber_oak_seasoned`, `timber_pine_seasoned`
- Naval grades (R, materials only): `naval_oak_keelframe`, `naval_pine_mast`
- Boards (optional): `boards_oak`, `boards_pine`, `boards_mixed`
- Fuel: `charcoal_std`, `charcoal_hp`
- By‑product: `wood_ash`

---

## 4) Recipes (human‑readable)
**C — Charcoal Clamp**
- Inputs: `log_*` ×10 → Outputs: `charcoal_std` ×8, `wood_ash` ×1  
- Time: long • Hazard: smoke_low • Building: `charcoal_clamp_C`

**C — Selection Bench**
- Inputs: `log_oak` ×1 → `timber_oak` ×1 • `log_pine` ×1 → `timber_pine` ×1  
- Time: short • Building: `selection_bench_C`

**M — Charcoal Kiln**
- Inputs: `log_*` or rods ×10 → Outputs: `charcoal_std` ×10, `wood_ash` ×1  
- Time: medium • Building: `charcoal_kiln_M`

**M — Seasoning**
- Inputs: `timber_oak` → `timber_oak_seasoned` (very_long)  
- Inputs: `timber_pine` → `timber_pine_seasoned` (very_long)  
- Building: `seasoning_rack_M`

**M — Hand Saw (optional)**
- Inputs: `timber_*` ×1 → Outputs: `boards_*` ×3 • Time: medium • Building: `handsaw_M`

**R — Naval Grading (Oak/Pine)**
- Inputs: `timber_oak`×5 OR `timber_oak_seasoned`×4 →  
  Outputs: `naval_oak_keelframe` ×1 (10–20% base; +5% if seasoned; +5% if long‑stock), `timber_oak` (return remainder)  
- Inputs: `timber_pine`×5 OR `timber_pine_seasoned`×4 →  
  Outputs: `naval_pine_mast` ×1 (same probability rules), returns remainder  
- Time: medium • Building: `naval_gauge_R`

**R — High‑Purity Charcoal**
- Inputs: rods ×12 → `charcoal_hp` ×10 + `wood_ash` ×1 • Time: medium • Building: `charcoal_kiln_R`

---

## 5) Buildings (with Era)
- `logging_site_C` (C): basic logging point  
- `charcoal_clamp_C` (C): low‑efficiency clamp  
- `selection_bench_C` (C): normal‑grade structural conversion  
- `charcoal_kiln_M` (M/R): efficient kiln; R mode for high‑purity  
- `seasoning_rack_M` (M): long‑term seasoning storage  
- `handsaw_M` (M): boards (optional)  
- `naval_gauge_R` (R): grading station (probabilistic)  
- `long_stock_yard_R` (R): allows long‑length stock classification

**Deferred (spec only, not enabled):**  
- `watersaw_R` (R, **Deferred**): Water‑Powered Sawmill.  
  - **Intended Effects:** `timber_*` → +throughput; `boards_*` yield +1 over hand‑saw; consumes water‑power node (X power units).  
  - **Unlock Gate:** must be downstream of water‑wheel tech; requires stone foundation + iron fittings.  
  - **Balance Knobs:** maintenance cost; winter freeze penalty (map‑dependent).  
  - **Current Status:** *DO NOT IMPLEMENT YET* (flag `enabled=false`).

---

## 6) Hooks to Metallurgy / Glass
- **Metallurgy:** if recipe fuel tag == `charcoal_hp`, grant one of: +10% batch OR −1 fuel OR −5% impurity.  
- **Glass:** if fuel tag == `charcoal_hp`, grant +5% yield OR reduce colour cast.  
- **Ash:** `wood_ash` → lye/potash route (you already have this).

---

## 7) Data Schemas (CSV)
- **forestry_items.csv** — `id,name,era,grade,stack,weight,notes`  
- **forestry_recipes.csv** — `id,era,inputs,outputs,time,building,hazard,notes`  
- **forestry_buildings.csv** — `id,era,power,throughput,is_enabled,notes`  
- **industry_hooks.csv** — `consumer,input_tag,bonus_type,bonus_value,era`

---

## 8) Balance Defaults
- Naval‑grade/mast‑grade base chance: **10%**; +seasoned **+5%**; +long‑stock **+5%** (cap **20%**).  
- Seasoning time: **very_long** (multi‑season).  
- High‑purity charcoal: target metallurgy **small uplift only** (avoid runaway).

---

## 9) Changelog
- v1.0 — Initial release, with stub for Water‑Powered Sawmill (deferred).