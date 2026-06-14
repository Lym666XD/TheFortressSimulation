# Agri-Brew Works 农业与酿造工坊

**对应 JSON**: `data/core/workshops/core_workshop_agri_brew_works.json`（23 attachments）
**对应 industry md**: [Agriculture_Design_v1.md](../industries/Agriculture_Design_v1.md) + [Brewing_Industry_Design_Bilingual.md](../industries/Brewing_Industry_Design_Bilingual.md)
**era**: C → R
**主要 tags**: workshop, agriculture, brewing, food

> 合并来源:
> - `../industries/CHATGPT_PROCESS_CHAIN_SOURCE.md §农业与酿造工坊 v1.0`
> - Agriculture + Brewing 两份 industry md
> - `core_workshop_agri_brew_works.json`

---

## 1) 用途与定位

农业后处理（磨粉、压榨、植物处理）+ 酿造全流程（制麦芽、糖化、煮沸、冷却、发酵、蒸馏、醋化）的统一工坊。

**容器约定**:
- 布袋: 粉 / 糖 / 染料
- 木桶 / 岩罐 rock_pot: 麦汁 / 酒 / 果汁
- 大罐 Jug: 油 / 蜂蜜

---

## 2) Attachment Slots（含 C/M/R 升级链）— JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `milling` | 磨制 | Quern & Hand Mill | Powered Millstone (水/风) | Fine Mill & Sifter Line |
| `press` | 压榨 | Tread Vat & Lever Beam Press | Improved Screw/Lever Press | Helical Continuous Press |
| `malting` | 制麦芽 | Malting Floor | Malt Kiln | Improved Malt Kiln |
| `mash_boil` | 糖化/煮沸 | Mash Kettle | Lauter Tun & Great Boiler | Insulated Mash & Great Boiler |
| `cooling` | 冷却 | Ambient Cooling Stand | Coolship & Cooler | Cold Cellar Cooling |
| `ferment` | 发酵 | Fermentation Vat | Cellar | Cold Cellar |
| `distill` | 蒸馏 | — | — | Still & Condenser |
| `acetify` | 醋化 | — | — | Acetification Vat & Mother |
| `preprocess` | 预处理/仓储 | Drying Ground | Ventilated Granary | Dry Granary & Winnow Stand |

---

## 3) 配方索引（按 era + 按链）

### 3.1 磨制线

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 石臼碾磨 | C | grain ×1 + sack ×1 | meal_coarse ×1 + food_residue ×2 |
| 动力磨石 | M | grain ×1 + sack ×1 | flour ×1 + food_residue ×2（吞吐≈C×2.5）|
| 精磨筛分 | R | grain ×1 + sack ×1 | flour_fine ×1（品质+1）+ food_residue ×2 |
| 甜荚磨糖 | C/M/R | sweetpod ×1 + sack ×1 | sugar_powder ×1 |

### 3.2 压榨线

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 橄榄压榨 | C | olive_crate ×1 + jug ×1 | olive_oil ×0.80 + presscake ×0.2 |
| 橄榄压榨 | M | 同上 | olive_oil ×0.88 + presscake ×0.12 |
| 橄榄压榨 | R | 同上 | olive_oil ×0.96 + presscake ×0.04 |
| 种子糊（磨制）| C→R | oil_seed ×1 + sack ×1 | seed_paste ×1 |
| 种子→植物油 | C→R | seed_paste ×1 + jug ×1 | vegetable_oil（按 0.80/0.88/0.96）+ presscake |
| 蜂巢脾压榨 | C→R | honeycomb ×1 + jug ×1 | honey ×1 + beeswax ×1 |
| 葡萄压榨 | C→R | grape_crate ×1 + barrel ×1 | must（葡萄汁）×1 + food_residue ×2 |
| 苹果/梨压榨 | M+ | apple/pear_crate ×1 + barrel ×1 | juice ×1 + food_residue ×2 |

### 3.3 制麦芽 → 糖化 → 煮沸 → 冷却 → 发酵（艾尔/啤酒）

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 制麦芽 | C | grain ×10 | malt ×8 |
| 麦芽烘干（稳定）| M/R | wet_malt ×8 | malt ×8（R 用时↓）|
| 糖化/过滤 | C | malt ×8 + water | wort ×8 + food_residue ×2 |
| 煮沸（无酒花艾尔，gruit）| C | wort ×8 + gruit_herbs ×1 | hot_wort ×8 |
| 煮沸（投酒花）| M/R | wort ×8 + hops ×1 | hot_wort ×8（R 用时更短）|
| 冷却 | C/M/R | hot_wort ×8 | cold_wort ×8 |
| 发酵—无酒花艾尔 | C | cold_wort ×8 | ale ×8 + yeast_cake ×0.2 |
| 发酵—啤酒（投酒花）| M | cold_wort ×8 | beer ×8 + yeast_cake ×0.2（保质↑标签）|
| 发酵—纯净啤酒 | R | quality_malt ×10 全流程 | beer_purity ×8 + yeast_cake ×0.2 |

### 3.4 葡萄酒 / 苹果酒 / 蜂蜜酒

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 葡萄酒 | C→R | must（桶）×1 | wine ×0.75 + lees ×0.25 |
| 苹果/梨酒 | M→R | juice（桶）×1 | cider/perry ×0.75 + pomace ×0.25 |
| 蜂蜜酒 | C→R | honey ×5 + water ×5 | mead ×6 + beeswax_residue ×0.2 |

### 3.5 蒸馏 / 醋化（R）

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 白兰地蒸馏 | R | wine ×8 | brandy ×3 + vinasse ×1 |
| 醋化 | R | low_abv_liquid ×8 | vinegar ×6 |

### 3.6 回收 / 循环

| 配方 | 输入 | 输出 |
|---|---|---|
| 任意渣 → 堆肥 | scrap ×1 | compost ×1 (→ Compost 工坊 / Agriculture) |
| 酵母渣复用 | yeast_cake ×1 | 下批发酵加速标签（仅加速）|

---

## 4) 上下游

```
[ 农业地块 / Field ]
   ├─ grain / sweetpod / grape / apple / olive / flax / hops → Agri_Brew_Works
   └─ flowers → Chemistry (rosewater)

[ Pasture_Shed / Apiary ]
   └─ honey / honeycomb → Agri_Brew_Works (蜂蜜酒)

[ Woodworking ]
   ├─ barrel / barrel_wine / barrel_aging_oak / jug → Agri_Brew_Works (容器)
   └─ wood_part 维修

[ Stoneworks ]
   └─ quern / millstone → Agri_Brew_Works.milling

[ Fuel_Alkali_Works ]
   └─ charcoal → Agri_Brew_Works (麦芽烘干 / 煮沸 / 蒸馏燃料)

[ Mechanical (未单独成 spec) ]
   └─ water_power / wind_power → Powered Millstone

[ Agri_Brew_Works 输出 ]
   ├─ meal_coarse / flour / flour_fine → Kitchen (主食)
   ├─ olive_oil / vegetable_oil → Kitchen / Crafts (油灯) / Chemistry (印刷油墨)
   ├─ honey → Chemistry (mead, dessert)
   ├─ beeswax → Crafts (蜂蜡烛) / Alchemy (礼仪)
   ├─ wine / beer / mead / cider / brandy / vinegar → 餐厅 / 贸易 / Chemistry (Spirit of wine)
   ├─ spent_grain / yeast_cake → Pasture_Shed (feed_mix R) / 烘焙
   ├─ pomace / lees / vinasse → Compost
   └─ presscake → Pasture_Shed / Fuel
```

---

## 5) 危害与特殊

- **保质期**: 啤酒（投酒花/纯净）保质 long；艾尔保质 short
- **品质标签**: 精磨面粉 "品质+1"；纯净啤酒 "纯净"；冷窖产线 "稳定性+"
- **吞吐**: 磨石 M ≈ 石臼 ×2.5；R 普遍用时更短/产量略增
- **环境**: workshop beauty 0；与 Kitchen 接邻可减少搬运

---

## 6) 与 industry md 的对应

- 农业完整链 + 时代台阶: [Agriculture_Design_v1.md](../industries/Agriculture_Design_v1.md)
- 酿造完整链 + 历史锚点: [Brewing_Industry_Design_Bilingual.md](../industries/Brewing_Industry_Design_Bilingual.md)

（完）
