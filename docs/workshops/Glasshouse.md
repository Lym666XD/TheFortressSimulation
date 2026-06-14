# Glasshouse & Lehr 玻璃工坊

**对应 JSON**: `data/core/workshops/core_workshop_glass_house.json`（19 attachments / 8 slots）
**对应 industry md**: [Glassmaking_Industry_Design.md](../industries/Glassmaking_Industry_Design.md)
**era**: C → R
**主要 tags**: glass, melting, forming, anneal, cullet

> 合并来源:
> - `../industries/CHATGPT_PROCESS_CHAIN_SOURCE.md §玻璃工坊 v2.0`
> - `Glassmaking_Industry_Design.md`
> - `core_workshop_glass_house.json`

---

## 1) 用途与定位

玻璃熔炼 → 吹制 / 模制 → 退火（lehr）→ 装饰；产 容器 / 平板 / 镜子 / 彩窗 / 透镜 / 实验玻璃。

**三条主线**:
- 罗马式（C，苏打玻璃）
- 森林玻璃 Waldglas（M，木灰钾玻璃）
- 威尼斯 Cristallo（R，去色精纯玻璃 + 镜 + 早期光学）

文化感: 罗马平板 + 拜占庭彩窗 + 阿拉伯 enameled glass（贸易）+ 威尼斯 Murano + 波西米亚 potash crystal。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `furnace` | 玻璃熔炉 | Crucible Glass Furnace | Forest Glass Furnace | Cristallo Crucible/Tank Furnace |
| `fining` | 澄清 | — | (内嵌升级) | Manganese Clarifier Fining |
| `anneal` | 退火（lehr）| Simple Lehr | Tunnel Lehr | Continuous Lehr |
| `wash` | 原料淘洗 | Sand Wash Bench | Sand & Ash Wash Pit | Fine Sand Levigation |
| `alkali` | 碱准备 | Soda Crucible Prep | Wood-Ash Leaching & Calcine | Pearlash + Calcined Lime |
| `forming` | 成型（吹/模/平板）| Blowing Bench | Crown Spinner | Cylinder Cut & Spread Bench |
| `spinner` | 平板旋盘 | — | Crown Spinner | Improved Spinner |
| `press` | 玻璃砖 / 厚板 | — | — | Glass Brick Press |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）— 罗马苏打-石灰玻璃

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 玻璃熔融（苏打）| sand ×4 + flux_natron ×1 + flux_lime ×1 + charcoal ×4 | glass_melt ×6 | furnace + wash + alkali |
| 吹制器皿 | glass_melt ×1 | glass_vessel ×1（goblet/jug/bottle） | forming + anneal |
| 平板铸（粗）| glass_melt ×1 | glass_pane_rough ×1 | forming + anneal |
| 色玻璃 / 马赛克瓦 | glass_melt ×4 + colorant ×0.5 | mosaic_tessera ×16 | forming |
| 玻璃珠 / millefiori | glass_melt ×1 | glass_bead ×20 | forming |

### 3.2 中世纪（M）— 森林玻璃 + 彩窗

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| **Waldglas 森林玻璃**（替换 C）| sand ×4 + wood_ash ×2 + flux_lime ×1 + charcoal ×4 | glass_melt_green ×6 | furnace M + alkali M |
| 中世纪平板（crown spinner）| glass_melt ×4 | glass_pane_medium ×3 | spinner + anneal |
| **彩窗 stained window** | glass_pane_colored ×N + lead_came ×N + lead_ingot ×1 | stained_window ×1 | forming + assembly |
| 铅玻璃（光学/装饰前置）| glass_melt + litharge ×0.5 | glass_melt_lead ×1 | furnace |

### 3.3 文艺复兴（R）— Cristallo + 镜 + 实验玻璃

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| **Cristallo 招牌**（去色精纯）| sand_fine ×4 + soda_purified ×2 + lime_calcined ×1 + manganese_clarifier ×0.2 + charcoal_hp ×4 | glass_cristallo ×6 | furnace R + fining + alkali R |
| Cristallo 高脚杯 / decanter / pane | glass_cristallo ×1 | glass_goblet_cristallo ×1 / glass_pane_crystal ×1 / decanter ×1 | forming + anneal R |
| **威尼斯镜**（mirror_silvered）| glass_pane_crystal ×1 + tin ×0.2 + mercury ×0.3 | mirror_silvered ×1（90% mercury 回收，10% fume）| forming + 镀背工位 |
| **老花镜 / 阅读石** | glass_cristallo ×1 + 磨工 | lens_reading ×1 | 磨镜工位（forming R + Precision 协作）|
| **实验玻璃器皿** | glass_melt ×2 + 高级吹工 | alembic_glass ×1 / retort ×1 / flask ×3 | forming |
| **Bohemian potash crystal** | sand_fine ×4 + wood_ash_pure ×3 + lime ×1 + charcoal_hp ×4 | glass_bohemian ×6 | furnace R + alkali R |
| 玻璃砖 / 厚板 | glass_melt ×4 | glass_brick ×3（建材）| press |

---

## 4) 上下游

```
[ Mine_Site ]
   ├─ sand / sand_fine → Glasshouse.wash
   └─ ore_alum / colorant minerals → Glasshouse.forming

[ Fuel_Alkali_Works ]
   ├─ wood_ash / wood_ash_pure → Glasshouse.alkali
   ├─ pearlash R → Glasshouse.alkali R
   └─ charcoal_std / charcoal_hp → Glasshouse.furnace

[ Chemistry_Lab ]
   ├─ soda_purified → Glasshouse R (cristallo)
   ├─ lime_calcined → Glasshouse R
   ├─ manganese_clarifier → Glasshouse R fining
   └─ mercury → Glasshouse R 镜

[ Smeltery ]
   ├─ lead → lead_came (彩窗装配) → Glasshouse
   ├─ tin → Glasshouse R 镜
   └─ litharge → Glasshouse (铅玻璃)

[ Stoneworks ]
   └─ flux_lime / lime_calcined → Glasshouse

[ Glasshouse 输出 ]
   ├─ glass_vessel / goblet / decanter / bottle → Kitchen / 餐厅
   ├─ glass_pane → 建筑窗户
   ├─ glass_pane_crystal → 建筑高档窗 / 镜
   ├─ stained_window → 教堂建筑
   ├─ mirror_silvered → 奢侈品 / 贵族
   ├─ lens_reading → Precision (Camera Obscura, Theodolite)
   ├─ alembic_glass / retort / flask → Chemistry / Alchemy（关键耗材）
   ├─ glass_brick → 建筑
   ├─ mosaic_tessera → 装饰
   ├─ glass_bead → 贸易 / 装饰
   └─ glass_cullet（碎玻璃）→ 回炉
```

---

## 5) 危害与特殊

- **燃料消耗**: 与 Smeltery / Pottery 三大燃料户
- **mercury_fume**: 镜匠工位 5%/季事件 → 通风要求
- **glass_cullet**: 副产，必须回收，否则 stench/cuts 场域
- **杂质 / 透明度**: C 杂色 −2 美观；M 绿色 −1；R cristallo +2

---

## 6) 与 industry md 的对应

详细历史 + 平衡：[Glassmaking_Industry_Design.md](../industries/Glassmaking_Industry_Design.md)。

（完）
