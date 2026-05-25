# Smeltery 冶炼工坊

**对应 JSON**: `data/core/workshops/core_workshop_metallurgy.json`
**对应 industry md**: [Smelting_and_Metallurgy_Design.md](../Smelting_and_Metallurgy_Design.md)
**era**: C → R
**主要 tags**: metallurgy, smelting, refining

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §冶炼工坊 v2.0`（配方主体、批次基准）
> - `Smelting_and_Metallurgy_Design.md`（历史锚点、设计原理）
> - `core_workshop_metallurgy.json`（attachment 真理来源）

---

## 1) 用途与定位

ore → ingot → alloy 的完整链条。
**批次基准**: 每批 ×10 原料；**固体副产**: 一律记为 `slag`（炉渣）；**动力依赖**: 标"水力鼓风/水力锻锤"无动力则配方不可执行。

**矿种修正**（影响金属量）:
- 铁: 赤铁矿 +15%、磁铁矿 +10%、褐/纤铁 −10%
- 铜: 孔雀石/蓝铜矿 +10%、黄铜矿 −10%、辉铜矿 −5%

下游主要为 **Metalworks**（成品锻造）、**Firearms**（炮筒铸造）、**Crafts_Lapidary**（贵金属饰品/字模合金）、**Glasshouse**（litharge 铅玻璃）、**Chemistry_Lab**（金属/酸前体）。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `furnace` (iron line) | 主铁炉 | Pit Bloomery | Shaft Furnace | Blast Furnace Stack |
| `hearth` | 钢用火床 | — | Finery Hearth | Improved Finery Hearth |
| `bench` (cementation) | 渗碳箱 → 大型钢炉 | — | Cementation Chest | Large Sealed Steel Furnace |
| `bench` (quench/temper) | 淬火/回火 | Quench Tub | Temper Oven | — |
| `power_bellows` | 鼓风 | Hand/Foot Bellows | Water-Powered Blast | (同 M) |
| `power_hammer` | 锻打 | Hand Forging Set | Water Trip Hammer | (同 M) |
| `scrubber` | SO₂ 洗涤 → 稀硫酸水 | — | Wash Pit Scrubber | Leaching Tower Scrubber |
| `furnace` (copper) | 铜专线 | Copper Crucible Furnace | Reverberatory Furnace | Large Reverberatory |
| `roaster` (cu) | 铜矿焙烧 | — | Copper Roasting Bed | Tall-Draft Copper Roaster |
| `bench` (brass) | 黄铜水泥法 | Sealed Crucible Bench (Brass) | (同 C) | (同 C) |
| `furnace` (lead) | 铅熔 | Lead Melting Furnace | (同 C) | (同 C) |
| `cupel` | 杯灰分银 | Cupellation Hearth & Cupels | Cupel Reclaim Bench | (同 M) |
| `liquation` | Seiger 析银 | — | — | Liquation/Seiger Furnace |
| `roaster` (cinnabar) | 朱砂焙烧 | Pan Roaster (Cinnabar) | Brick Roaster (Cinnabar) | (与 still 联动) |
| `still` (mercury) | 闭式汞蒸馏 | — | — | Closed Mercury Still |

---

## 3) 配方索引（按 era）— 直接从 PROCESS_CHAIN v2.0 整合

### 3.1 铁/钢线（产 `iron_wrought` / `iron_pig` / `steel_ingot` / `steel_fine_ingot`）

**C — 块炼直还**（需 Pit Bloomery + Hand Bellows + Hand Forging）
- 还原: `iron_ore ×10 + charcoal ×20 + limestone ×10 → sponge_iron ×10 + slag ×10`
- 整块: `sponge_iron ×10 → iron_wrought ×12`（热锻）
- 矿修: 赤铁 +15% / 磁铁 +10% / 褐铁 −10%

**M — 竖炉/早高炉**（需 Shaft Furnace + Water Bellows）
- 主炉（替换 C）: `iron_ore ×10 + charcoal ×30 + limestone ×10 → iron_pig ×22 + slag ×10`
- 细炼: `iron_pig ×20 + charcoal ×10 → iron_wrought ×15 + slag ×5`
- 副产: Wash Pit Scrubber 启用 → `dilute_sulfuric_water ×2`（送 Chemistry）

**R — 高炉堆**（需 Blast Furnace + Water Bellows + Water Hammer）
- 主炉（替换 M）: `iron_ore ×10 + charcoal ×28 + limestone ×10 → iron_pig ×24 + slag ×10`
- 细炼: `iron_pig ×20 + charcoal ×8 → iron_wrought ×16 + slag ×4`
- 副产: Leaching Tower 启用 → `dilute_sulfuric_water ×3`

**钢路线 A — 渗碳法**（熟铁 → 钢）
- M 钢盒: `iron_wrought ×10 + charcoal_powder ×10 + sealing_clay ×3 (long_heat) → steel_ingot ×8`
- R 大型钢炉: `iron_wrought ×10 + charcoal_powder ×9 + sealing_clay ×3 → steel_fine_ingot ×9`

**钢路线 B — 火床法**（生铁 → 钢）
- M 炼钢火床（需 Water Bellows）: `iron_pig ×20 + limestone ×5 → steel_ingot ×10 + slag ×6`
- R 改良炼钢火床（需 Water Bellows）: `iron_pig ×20 + limestone ×5 → steel_fine_ingot ×12 + slag ×5`

**热处理（品质标签，数量不变）**
- 淬火: `steel_ingot/steel_fine_ingot ×N → (quenched)_ ×N`
- 回火: `steel_ingot/steel_fine_ingot ×N → (tempered)_ ×N`
- 仅影响装备 durability/sharpness，不改变数量

### 3.2 铜线 + 青铜 + 黄铜

**碳酸盐铜（C，坩埚直熔）**
- `malachite/azurite ×10 + charcoal ×15 + limestone ×5 → copper_ingot ×10 + slag ×5`
- 矿修: 孔雀石/蓝铜矿 +10%

**硫化铜（M/R，焙烧 + 反射炉）**
- 焙烧: `chalcopyrite/bornite ×10 → roasted_cu ×10 + SO₂`（M=洗涤坑 → dilute_sulfuric_water ×2；R=淋洗塔 ×3）
- 反射: `roasted_cu ×10 + charcoal ×12 + limestone ×6 → crude_copper ×10 + slag ×6 → 火法精炼 → copper_ingot ×10`
- 矿修: 黄铜矿 −10%、辉铜矿 −5%

**青铜（Cu-Sn 合金）**
- C 坩埚: `copper_ingot ×9 + cassiterite ×1 + charcoal ×2 → bronze ×10`（含约 8% 熔损）
- M/R 反射炉: `crude_copper ×9 + cassiterite ×1 + charcoal ×2 → bronze ×10`（约 5% 熔损）

**黄铜（卡拉铭水泥法，密封坩埚 C→R 通用）**
- `copper_ingot ×20 + calamine_ore ×10 (菱锌矿/异极矿) + charcoal ×10 → brass ×20 + slag ×5`

**特殊合金（R 工业 + 工艺）**
- 字模合金（印刷术核心）: `lead ×7 + tin ×2 + antimony_ore ×1 → type_alloy ×10`
- 铸钟青铜（高锡）: `copper_ingot ×4 + tin ×1 + flux_quartz ×0.5 → bell_bronze ×5`
- 火炮青铜（gun metal）: `copper_ingot ×9 + tin ×1 → gun_metal ×10`

### 3.3 铅 / 银线

**铅冶（C→R 通用）**
- `galena ×10 + charcoal ×15 + limestone ×5 → crude_lead ×12 + slag ×6 + SO₂`
  - M Wash Pit → dilute_sulfuric_water ×2；R Leaching → ×3

**杯灰分银（C→R 通用）**
- `crude_lead ×10 (含银) → silver_grain ×0.3 + litharge ×9.5`
- 灰铅回收: `litharge ×9.5 + charcoal ×3 → lead_ingot ×9 + slag ×1`

**Seiger 析银（R 可选，更高回收率）**
- `silver_bearing_copper ×10 + lead ×10 → silver_rich_lead ×10 + desilvered_copper ×10`
- 后续: silver_rich_lead → 杯灰 → 银 +10–20%

### 3.4 朱砂线（Hg 汞）

- C: `cinnabar ×10 → mercury ×10 + SO₂`（仅排放，污染事件↑）
- M (砖砌焙烧 + 串联冷凝): `cinnabar ×10 → mercury ×11 + SO₂` (Wash → dilute_sulfuric_water ×3)
- R (闭式汞蒸馏器): `cinnabar ×10 → mercury ×12 + SO₂` (Leaching → ×4)
- 风险: 汞蒸气 → mercury_fume 场域；R 型风险最低

### 3.5 金线（沿用铜坩埚 + 助熔）

- `gold_ore/placer_gold ×10 + charcoal ×5 + flux (硼砂/石灰) ×5 → gold_ingot ×10 + slag ×5`
- 含硫/砷夹杂时，M/R 可先用焙烧床预焙

### 3.6 汞齐金（R，与 Chemistry 协作）

- `low_grade_gold_ore ×10 + mercury ×1 → gold_ingot ×2 + mercury ×0.9` （汞 90% 回收）
- 危害: mercury_fume

---

## 4) 上下游

```
[ Mine_Site ]
   └─ ore_iron/copper/tin/lead/silver/gold/cinnabar/antimony/calamine → Smeltery

[ Fuel_Alkali_Works ]
   └─ charcoal_std / charcoal_hp → Smeltery (主燃料)

[ Stoneworks ]
   └─ limestone / flux_quartz → Smeltery

[ Chemistry_Lab ]
   ├─ 反向接收: dilute_sulfuric_water (Wash Pit/Leaching Tower 副产)
   └─ 输入: mercury (汞齐法) — 双向

[ Smeltery 输出 ]
   ├─ iron_wrought / steel_ingot / steel_fine_ingot → Metalworks (主要)
   ├─ copper_ingot / tin_ingot / bronze / brass → Metalworks / Crafts_Lapidary
   ├─ lead_ingot → Glasshouse (lead came / lead glaze) / Chemistry (颜料) / Firearms (弹丸)
   ├─ silver_grain / gold_ingot → Crafts_Lapidary / Mint section of Metalworks
   ├─ bell_bronze → Crafts_Lapidary (大钟) / 教堂建筑
   ├─ gun_metal → Firearms (炮筒铸造)
   ├─ type_alloy → Paper (字模)
   ├─ litharge → Glasshouse (铅玻璃) / 颜料
   └─ mercury → Chemistry / Alchemy / Glasshouse (镜) / 汞齐金
```

---

## 5) 危害与特殊

- **塌方/缺氧**: 不属于本工坊（属于 Mine_Site）
- **SO₂ 烟气**: M 起必装 scrubber，否则触发 fume 场域 + 矿工事件
- **mercury_fume**: 朱砂线唯一危害；C 期最危险；R 闭式 still 最低
- **slag 堆积**: 每批产 5–10 slag → 必须运到 Stoneworks.crushing 作骨料；否则触发 stench
- **燃料**: 与林业 charcoal 闭环；charcoal_hp 可给一次配方择一加成（+10% 批量 / −1 fuel / −5% slag）

---

## 6) 与 industry md 的对应

设计原理 + 历史锚点（Walloon process / Catalan forge / Bessemer 边界）：见 [Smelting_and_Metallurgy_Design.md](../Smelting_and_Metallurgy_Design.md)。

本工坊文档是**实现层 + JSON 对齐**；industry md 是**叙事 + 设计原理**。

（完）
