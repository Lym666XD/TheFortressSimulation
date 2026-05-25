# Pottery 陶瓷工坊

**对应 JSON**: `data/core/workshops/core_workshop_pottery.json`（19 attachments）
**对应 industry md**: [Ceramics_Pottery_Industry_Design.md](../Ceramics_Pottery_Industry_Design.md)
**era**: C → R
**主要 tags**: workshop, ceramic, pottery, building_materials

> 来源:
> - `Ceramics_Pottery_Industry_Design.md`（设计原理与历史锚点：terra sigillata / lustreware / stoneware / maiolica / soft porcelain）
> - `core_workshop_pottery.json`

---

## 1) 用途与定位

陶土准备 → 成型 → 烧制 → 釉色 → 装饰的完整陶瓷链条。

**分工边界**:
- **Pottery**: 餐具 / 容器 / 装饰瓷 / 化学陶器（alembic / retort）/ 蜂蜡封缄罐
- **Stoneworks**: 建筑砖瓦的烧制（kiln 共用建模，但配方所属工坊不重叠 — 建材类砖瓦在 Stoneworks，餐具陶器在 Pottery）
  - 注：本工坊也可以产 unfired_brick / unfired_roof_tile（半成品），由 Stoneworks.kiln 烧制
- **Glasshouse**: 玻璃；本工坊只做陶瓷
- **Crafts_Lapidary**: 真正的雕塑级陶瓷艺术品 → Crafts 提升 Q

**地中海 / 阿拉伯 / 北欧文化感**: terra sigillata 罗马量产、lustreware 阿拉伯虹彩、stoneware 莱茵盐釉、maiolica 意大利锡釉彩绘——四条不同流派。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

> JSON 中 `attachment_slots: []`（顶层未明确），由本文档定义虚拟 slot：

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `clay_prep` | 陶土池 | Settling Pit | Chambered Settling | Mechanical Levigation |
| `wheel` | 陶轮 | Slow Wheel | Fast Kick Wheel | Treadle Wheel |
| `mold` | 模印 / 滚印 | Mold Press Bench | Slip Casting Mold | Master Mold Bench |
| `kiln` | 烧窑 | Updraft Kiln | Improved Updraft Kiln | Reverberatory + Twin Chamber |
| `glaze` | 釉房 | — | Lead Glaze House | Tin Glaze House + Lustre Reduction |
| `paint` | 彩绘 | Slip Decoration Bench | Underglaze Bench | Maiolica Painting Bench |
| `salt_glaze` | 盐釉投料 | — | Salt Throw Port | (集成升级) |
| `soft_porcelain` | 软瓷实验（R 末稀有）| — | — | Experimental Soft Porcelain Kiln |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 陶土陈化 | raw_clay ×4 + water | clay_workable ×3 | clay_prep |
| 轮制基础陶器 | clay_workable ×1 | pot_unfired ×1 → 烧 → pottery_basic ×1 | wheel + kiln |
| **terra sigillata 红光泽** | clay_workable ×1 + iron_rich_slip ×0.3 | sigillata_unfired ×1 → 烧 → tableware_sigillata ×1 | mold + kiln |
| amphora 双耳瓶 | clay_workable ×3 | amphora ×1（贸易容器）| wheel + mold |
| oil_lamp / cooking_pot / jug / bowl | clay_workable ×1 | (per item) ×1 | wheel |
| mosaic_tessera | clay_workable ×0.5 + colorant ×0.5 | mosaic_tessera ×16 | mold + kiln |

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 铅釉陶 | pottery_basic ×1 + lead_glaze ×0.2 | glazed_pottery ×1 | glaze + kiln（二次烧）|
| **锡釉 + lustreware（阿拉伯）** | pottery_basic ×1 + tin_glaze ×0.3 + cobalt_blue ×0.1 | lustreware ×1（美观 +3）| glaze + paint + kiln（虹彩还原烧）|
| **盐釉炻器**（莱茵）| clay_stoneware ×1 + salt_coarse ×0.1 | stoneware_pot ×1（不渗水，容量 +30%）| salt_glaze + kiln（高温倒焰）|
| unfired_roof_tile (送 Stoneworks 烧)| clay ×1 | unfired_roof_tile ×2 | mold（坯件）|

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| **Maiolica 彩绘**（意大利招牌）| pottery_fine ×1 + tin_glaze ×0.3 + pigment_mix ×0.5 | maiolica_piece ×1（美观 +4）| paint + glaze + kiln |
| 软质瓷 Medici porcelain（试制）| clay_fine ×1 + glass_cullet ×0.2 + bone_ash ×0.1 | soft_porcelain_attempt ×1（成功率 30%，碎瓷回炉）| soft_porcelain |
| 陶 alembic / cucurbit（化学耗材）| clay_stoneware ×2 | ceramic_alembic ×1 / cucurbit ×1 | wheel + kiln（R 高温）|
| unfired_decor_tile / unfired_terracotta_piece（送 Stoneworks 烧）| fine_clay ×1 + glaze ×1 | unfired_decor_tile ×2 | mold（坯件）|

---

## 4) 上下游

```
[ Mine_Site ]
   └─ raw_clay → Pottery.clay_prep

[ Salt_Works ]
   └─ salt_coarse → Pottery (盐釉炻器)

[ Smeltery ]
   ├─ lead → lead_glaze → Pottery
   ├─ tin → tin_glaze → Pottery
   └─ cobalt-bearing → colorant → Pottery
   ← (反向) crushed_brick / cullet 副产可由 Stoneworks 回收

[ Chemistry_Lab ]
   ├─ pigment_mix（cobalt/copper/iron/manganese）→ Pottery
   └─ ceramic_alembic / cucurbit ← Pottery (本工坊产出化学陶器)

[ Glasshouse ]
   └─ glass_cullet → Pottery.soft_porcelain (Medici 试制)

[ Pasture_Shed ]
   └─ bone_ash → Pottery.soft_porcelain

[ Fuel_Alkali_Works ]
   └─ charcoal → Pottery.kiln

[ Pottery 输出 ]
   ├─ tableware_sigillata / glazed_pottery / lustreware / maiolica → 餐厅 / 贸易奢侈品
   ├─ stoneware_pot / amphora → 容器 / 酿造 / 贸易
   ├─ oil_lamp → 照明（Crafts_Lapidary 蜡烛之外的选项）
   ├─ unfired_brick/tile/decor_tile → Stoneworks（协烧）
   ├─ ceramic_alembic / cucurbit → Chemistry / Alchemy
   └─ soft_porcelain → 神器级稀有
```

---

## 5) 危害与特殊

- **燃料密集**: 与 Smeltery / Glasshouse 并列三大燃料消耗户
- **铅釉毒性**: 长期使用铅釉餐具 → 微弱"贵族铅中毒"事件钩
- **虹彩还原烧**: 工艺敏感；成功率 80%
- **软瓷成功率 30%**: 碎瓷回炉为 cullet

---

## 6) 与 industry md 的对应

详细历史脉络 + 平衡数值，见 [Ceramics_Pottery_Industry_Design.md](../Ceramics_Pottery_Industry_Design.md)。

（完）
