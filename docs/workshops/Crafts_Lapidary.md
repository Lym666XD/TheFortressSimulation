# Crafts & Lapidary 工艺品/宝石工坊（含蜡烛与油灯）

**对应 JSON**: `data/core/workshops/core_workshop_crafts.json`（15 attachments）
**对应 industry md**: [Oil_Soap_Candle_Industry_Design.md](../Oil_Soap_Candle_Industry_Design.md)（蜡烛/油灯部分） + PROCESS_CHAIN §工艺品+宝石
**era**: C → R
**主要 tags**: workshop, crafts, carving, lathe, casting, inlay, jewel, candle, art

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §工艺品+宝石工坊（合并版）`（DF Craftsdwarf + Jeweler 整合）
> - `Oil_Soap_Candle_Industry_Design.md`（蜡烛/油灯并入此工坊，按用户决策）
> - `core_workshop_crafts.json`

---

## 1) 用途与定位

DF 的 Craftsdwarf + Jeweler 合并版，统一负责**装饰类产品 + 宝石切磨 + 镶嵌/绘画 + 蜡烛/油灯/兽脂浸蘸**。

**统一规则**:
- 本工坊所有新成品 "无美观、只有质量 Q（Q0 普通 / Q1 精良 / Q2 大师 / Q3 典藏）"
- "装饰/镶嵌"动作**只提升 Q + 售价/声望标签**，不开"美观"数值
- 杯/盘等用于进食饮酒给心情加成（强度随 Q 浮动）

**为何在这里加蜡烛**: 用户决策 — 不单独建 Chandlery 工坊。Crafts 已有 cast_station（pewter 铸造模具）+ polish_station，加一个 wax_dipping slot 即可承担蜡烛业。兽脂熬炼留在 Butchery；皂化留在 Chemistry_Lab。

---

## 2) Attachment Slots（含 C/M/R 升级链）— JSON 对齐 + 新增 wax/candle slot

> JSON 中 `attachment_slots: []`（顶层未明确），按 attachment 的 slot 字段反推:

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `carve_station` | 雕刻 | Carving Bench & Bow Drill | Fine Carving Bench & Vise | Precision Vise & Micro Chisels |
| `lathe_station` | 车削 | Bow Lathe | Spring-Pole Lathe | Treadle Lathe & Gauges |
| `polish_station` | 抛光 | Polishing Pads & Abrasives | Sand Barrel & Buffing Drum | Polishing Wheel |
| `cast_station` | 铸造（pewter 等）| — | Pewter Moulds & Ladle | Casting Stove & Sprue Cutter |
| `inlay_station` | 镶嵌/錾刻 | — | — | Inlay & Engraving Bench |
| `jewel_station` | 宝石切磨 | Lapidary Plate & Abrasive Slurry | Lapidary Wheel | Water-Drip Lapidary & Faceting Gauges |
| `wax_station` ⭐新 | 蜡烛浸蘸 / 模铸 | Wax Dip Bench | Tallow Dip Bench + Mould Set | Chandler's Dip Tower |
| `paint_easel` | 绘画 | — | Painting Easel (panel) | Oil Painting Easel (canvas) |
| `enamel_kiln` | 珐琅小窑 | — | — | Enamel Mini-Kiln |
| `mosaic_bench` | 马赛克 | Mosaic Bench (C 简易) | Mosaic Bench (improved) | (升级) |

**全局加成**（按附件套档）: C 基线；M 产能 ×1.15 / 失败 −10%；R 产能 ×1.25 / 失败 −20%；R 解锁"高级镶嵌（复合材：宝石+玻璃+珐琅+金银丝）"。

---

## 3) 配方索引（按 era）

### 3.1 宝石切磨（jewel_station）

| era | 配方 | 输出 |
|---|---|---|
| C | raw_gem ×1 + abrasive_sand ×1 | cut_gem_C ×1（cabochon/雕刻为主）|
| M | raw_gem ×1 + abrasive_sand ×1 | cut_gem_M ×1（玫瑰切/桌面切，成功率↑）|
| R | raw_gem ×1 + abrasive_sand ×1 | cut_gem_R ×1（分度刻面高抛，最高 Q）|

> abrasive_sand 来自 Stoneworks 或 Chemistry（刚玉砂/金刚砂/打磨粉）

### 3.2 冠冕与首饰（可被通用镶嵌再加工）

| 物品 | 配方 | 备注 |
|---|---|---|
| 王冠 crown | metal_bar ×3 + small_part ×1 → crown ×1 (Q) | C→R |
| 项链 necklace | metal_bar ×1 + chain ×1 → necklace ×1 (Q) | C→R |
| 手链 bracelet | metal_bar ×1 + chain ×0.5 → bracelet ×1 (Q) | C→R |
| 戒指胎 ring_blank | metal_bar ×0.5 → ring_blank ×1 (Q) | C→R；后续镶嵌成"戒指" |
| 手杖 staff | wood_rod ×1 + metal_fitting ×1 → staff ×1 (Q) | M→R；R 可加"权杖头" |

### 3.3 器皿/礼器（用餐心情+）

| 物品 | 配方 |
|---|---|
| 高脚杯 goblet | metal_bar ×2 + small_part ×1 → goblet ×1 (Q)（用餐心情+）|
| 盘 plate | metal_plate ×2 → plate ×2 (Q)（用餐心情+）|
| 宗教礼器套 | goblet + plate + heraldic_plaque → ritual_set ×1 (Q，仅标签) |

### 3.4 纹章与徽牌

| 物品 | 配方 |
|---|---|
| 纹章徽牌 heraldic_plaque | metal_plate ×2 + enamel/glaze ×1 → heraldic_plaque ×1 (Q) | R 可加铭刻 / 细丝 |

### 3.5 镜子（与化学工坊联动）

| 物品 | era | 配方 |
|---|---|---|
| 抛光青铜镜 | C | bronze_plate ×1 → bronze_mirror ×1 (Q) |
| 玻坯镜 | R | glass_pane_crystal ×1 + silvering_compound ×1 → glass_mirror ×1 (Q) |

> `silvering_compound` 由 Chemistry_Lab 供给（R 期）

### 3.6 绘画

| 物品 | era | 配方 |
|---|---|---|
| 板绘 panel painting | C→M | board ×1 + pigment ×1 + glue/oil ×0.2 → painting_panel ×1 (Q；陈列心情+) |
| 布面油画 oil painting | R | canvas ×1 + pigment ×1 + linseed_oil ×0.2 → painting_oil ×1 (Q；陈列心情+) |

### 3.7 通用镶嵌 / 装饰（"encrust" 动作）

**机制**: 对家具/装备/武器/工艺品/书籍封面等成品执行"装饰"动作

- **材料包**（任选其一或复合）:
  - cut_gem ×1
  - colored_glass_inlay ×1
  - enamel_powder ×1
  - silver/gold_filigree ×1
  - metal_engrave_piece ×1
- **结果**: 目标 (Q) → 目标 (Q+1)
- **R 附件**: 可一次 +2，但失败率极低 & 高材料消耗

附注: 装饰后物品打上 `gem_inlay / glass_inlay / enamel / filigree / engraved` 标签（仅用于售价/成就）

### 3.8 其他经典工艺（C/M/R）

| 物品 | era | 配方 |
|---|---|---|
| 马赛克面板 | C→M | stone/glass_tessera ×N → mosaic_panel ×1 (Q)（建材皮肤）|
| 细木镶嵌 intarsia | M→R | wood_pieces ×N → intarsia_panel ×1 (Q)（也可在 Woodworking R）|
| 金银丝细工 filigree | M→R | silver/gold_filigree ×1 → filigree_ornament ×1 (Q) |
| 黑银 niello | M→R | silver_blank ×1 + sulfide_paste ×1 → niello_piece ×1 (Q) |

### 3.9 ⭐ 蜡烛 / 油灯线（new — 从 Oil_Soap_Candle 合并）

> 用户决策: 不单建 Chandlery；并入此工坊（cast_station 已有 pewter mould 经验 + 新增 wax_station）。

| 物品 | era | 配方 |
|---|---|---|
| **油灯 oil_lamp**（陶身在 Pottery）| C | pottery_lamp_chamber ×1 + wick_string ×0.1 + (olive_oil 燃料) → oil_lamp_unit ×1 |
| **罗马软皂**（如不在 Chemistry 做）| C | tallow ×1 + lye_solution ×1 → soft_soap ×1 | (Chemistry 也可，二选一) |
| **兽脂烛 tallow_candle** | M | tallow ×2 + wick_string ×0.1 → tallow_candle ×8 | wax_station M |
| **蜂蜡烛 beeswax_candle** | M | beeswax ×1 + wick_string ×0.1 → beeswax_candle ×4 | wax_station M（共用）|
| **灯心草烛 rushlight** | M | rush_stem ×10 + tallow ×0.5 → rushlight ×10 | wax_station C 或 M |
| **R 香皂 / 玫瑰皂** | R | soap_castile ×1 + perfume ×0.1 → soap_scented ×1 | (在 Chemistry R 完成更自然，本工坊提供模具/装饰)|
| **R 浸蘸塔批量**（chandler tower）| R | wax_station R 满级 → tallow_candle 吞吐 +60% | wax_station R |

**与 industry md 边界**: 详细历史脉络（Castile / Marseille / Aleppo 皂 + chandler guild 社会标签）见 [Oil_Soap_Candle_Industry_Design.md](../Oil_Soap_Candle_Industry_Design.md)。

---

## 4) 上下游

```
[ Mine_Site / Stoneworks ]
   ├─ raw_gem → Crafts.jewel_station
   └─ abrasive_sand → 切磨

[ Smeltery ]
   ├─ metal_bar / metal_plate / chain / small_part / bronze_plate / silver / gold → Crafts
   └─ silver_blank → niello

[ Woodworking ]
   ├─ wood_rod / wood_pieces / boards / board → Crafts (staff, marquetry, painting board)
   └─ canvas frame → 绘画

[ Chemistry_Lab ]
   ├─ pigment / lampblack / glue / linseed_oil → Crafts (painting)
   ├─ silvering_compound → Crafts (mirror)
   ├─ enamel_powder / sulfide_paste → Crafts (enamel/niello)
   └─ wick_string supply → 蜡烛

[ Butchery ]
   ├─ tallow / lard → Crafts (蜡烛 / 油灯)
   └─ raw_fat → (先送 Butchery 熬炼)

[ Pasture_Shed / Apiary ]
   └─ beeswax → Crafts (蜂蜡烛 / 礼仪)

[ Glasshouse ]
   ├─ glass_pane_crystal → Crafts (镜)
   ├─ colored_glass_inlay → Crafts (镶嵌)
   └─ mosaic_tessera → Crafts (马赛克)

[ Pottery ]
   ├─ pottery_lamp_chamber → Crafts (油灯)
   └─ enamel raw materials → Crafts

[ Tailor ]
   ├─ canvas → Crafts (油画)
   └─ wick_string → Crafts (蜡烛)

[ Crafts 输出 ]
   ├─ jewelry (crown/necklace/bracelet/ring/staff) → 贵族/贸易/外交
   ├─ goblet/plate/heraldic_plaque/ritual_set → 餐厅/教堂/仪式
   ├─ mirror → 奢侈品/装饰
   ├─ painting_panel/oil → 装饰/陈列
   ├─ mosaic / intarsia / filigree / niello → 装饰板 (送建筑/家具)
   ├─ tallow_candle / beeswax_candle / rushlight → 照明/教堂/礼仪
   ├─ oil_lamp → 照明
   ├─ encrusted items (Q+1) → 二次加工 + 标签
   └─ small_automaton (轻量；与 Precision 重型自动机区分) → 装饰/触发
```

---

## 5) 危害与特殊

- **无重型危害**: workshop beauty +2（少数正向工坊）
- **细金工 / 失败浪费贵金属**: 装饰失败损耗材料但不降本体 Q
- **蜡烛失火**: rushlight 5% 短期燃烧 → minor fire 事件
- **质量 Q 阶**: 与 Precision 共用（Q0–Q3）
- **encrust 系统**: DF 风味动作，玩家可主动给已有物品"装饰升级"

---

## 6) 与其他工坊的边界

| 物品 | Crafts_Lapidary | Metalworks | Smeltery | Precision |
|---|---|---|---|---|
| 普通武器/护甲 | ❌ | ✅ | ❌ | ❌ |
| 装饰升级（武器/家具/书）| ✅ encrust | ❌ | ❌ | ❌ |
| 王冠/首饰/礼器/杯盘 | ✅ | ❌ | ❌ | ❌ |
| 镜（玻坯/青铜）| ✅ | ❌ | ❌ | ❌ |
| 绘画 | ✅ | ❌ | ❌ | ❌ |
| 蜡烛 / 油灯 | ✅ | ❌ | ❌ | ❌ |
| 兽脂熬炼 | ❌（Butchery）| ❌ | ❌ | ❌ |
| 皂化 | （二选一）| ❌ | ❌ | ❌ |
| 大型钟表/天文钟/差分机 | ❌ | ❌ | ❌ | ✅ |
| 小型自动机 / 饮水鸟 | ✅（轻量）| ❌ | ❌ | （Precision 也可） |

---

## 7) 与 industry md 的对应

- 蜡烛 / 油灯历史 + 平衡: [Oil_Soap_Candle_Industry_Design.md](../Oil_Soap_Candle_Industry_Design.md)
- 工艺品 / 宝石主要来源: `CHATGPT_PROCESS_CHAIN.md §工艺品+宝石工坊（合并版）`（已并入本文档）

（完）
