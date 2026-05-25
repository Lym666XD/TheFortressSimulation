# Logging Camp 伐木营

**对应 JSON**: `data/core/workshops/core_workshop_logging.json`（15 attachments）
**对应 industry md**: [Forestry_NavalTimber_Design_v1.md](../Forestry_NavalTimber_Design_v1.md)
**era**: C → R
**主要 tags**: workshop, wood, logging, lumber

---

## 1) 用途与定位

伐木 → 集材 → 木材分级 → 烧炭（与 Fuel_Alkali_Works 联动）→ 海军木材分级（R）。

**与 Woodworking 的边界**: Logging 出**原木 / 木材 / 板材 / rodwood / bow_stave / 海军级 keel-frame / mast**；Woodworking 从这些原料起做**家具/桶/弓/箭杆/工具柄/工程件**。

**与 Fuel_Alkali_Works 的边界**: Logging 提供木材 → Fuel_Alkali 烧炭；wood_ash 副产由 Fuel_Alkali 出。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `felling` | 伐木方式 | Hand Axe | Two-Man Crosscut Saw | (升级) |
| `skidding` | 集材 | Manual Drag | Ox/Horse Skidding | Pulley Skid |
| `selection_bench` | 选材分级 | Selection Bench | Improved Selection | Selection + Tagging |
| `seasoning_rack` | 季节风干 | Seasoning Rack | Vented Rack | Low-Temp Drying Bay |
| `handsaw_bench` | 手锯（板）| Hand Saw Bench | (升级) | (升级) |
| `naval_gauge` | 海军木材分级（R 招牌）| — | — | Naval Timber Gauge |
| `long_stock_yard` | 长料堆场 | — | — | Long-Stock Yard |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）

| 配方 | 输入 | 输出 |
|---|---|---|
| 选择伐木 | tree (mapgen) | log_oak / log_pine / log_mixed ×N |
| 普通选材 | log_* ×1 | timber_* ×1（normal grade）|

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 |
|---|---|---|
| 季节风干 | timber_* ×1 | timber_*_seasoned ×1（very_long；+quality flag）|
| 手锯（可选板材）| timber_* ×1 | boards_* ×3 |

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 |
|---|---|---|
| **海军木材分级**（招牌）| timber_oak ×5 OR timber_oak_seasoned ×4 | naval_oak_keelframe ×1（10–20%）+ timber_oak 余料；seasoned +5% / long-stock +5%（cap 20%）|
| 海军桅杆级 | timber_pine ×5 OR timber_pine_seasoned ×4 | naval_pine_mast ×1（同概率公式）|
| 水力锯坊（deferred）| timber_* | boards_* +throughput（停用，目前 flag enabled=false）|

> 注: Naval grade 是材料分级（不是造船工坊）；造船业留待 SHIPBUILDING_SPEC。

---

## 4) 上下游

```
[ MAPGEN tree (生物群落) ]
   ├─ oak / pine / mixed → Logging.felling
   └─ 特殊树种（yew 长弓木）→ Logging（标签：bow_stave_yew）

[ Pasture_Shed ]
   └─ horse/ox → Logging.skidding (M 集材)

[ Logging 输出 ]
   ├─ log_oak/pine/mixed → Woodworking (大头消费者)
   ├─ timber_*（normal / seasoned）→ Woodworking
   ├─ boards_* → Woodworking
   ├─ rodwood → Woodworking (箭杆/工具柄/弩矢杆) + Fuel_Alkali (R 干馏炭)
   ├─ bow_stave / bow_stave_yew → Woodworking.bowyer_bench
   ├─ wood (普通烧炭料) → Fuel_Alkali_Works.carbonize
   ├─ naval_oak_keelframe / naval_pine_mast → 未来造船业
   ├─ thatch_straw 木边料 → 屋顶 / 垫料
   └─ wood_tar (R 干馏副产，与 Fuel_Alkali R 联动) → Forestry naval 防水
```

---

## 5) 危害与特殊

- **伐木事故**: 倒木砸人，3% / 季 / 工人
- **林业再生**: 与 ecology 系统挂接（未来）— 过度采伐 → 树木刷新慢
- **海军 grade 稀有性**: 10–20% 出率使"造船"成为长线规划
- **wood_tar (R)**: 林业 R 期可作木材防水 (船板, 桶)；与 Chemistry 联动

---

## 6) 与 industry md 的对应

详细演进 + 平衡 + 海军木材分级机制: [Forestry_NavalTimber_Design_v1.md](../Forestry_NavalTimber_Design_v1.md)

（完）
