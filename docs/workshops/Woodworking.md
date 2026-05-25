# Woodworking 木工工坊（含 cooperage 桶匠 + bowyer 弓匠）

**对应 JSON**: `data/core/workshops/core_workshop_woodworking.json`（11 attachments）
**对应 industry md**: [Woodworking_Industry_Design.md](../Woodworking_Industry_Design.md)
**era**: C → R
**主要 tags**: workshop, wood, carpentry

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §木工工坊 v1.1（简化件版）`
> - `Woodworking_Industry_Design.md`（合并木工 + 桶匠 + 弓匠/弩匠）
> - `core_workshop_woodworking.json`

---

## 1) 用途与定位

通用木工 + 细木匠 + **桶匠 cooperage** + **弓匠 bowyer**（含弩臂木件、火器木托）的合并工坊。一主建筑、多 attachment 升级，避免建筑列表爆炸。

**与林业的边界**: Logging 负责 `timber/boards/rodwood/bow_stave` 等原材料；本工坊从原材料开始制作**家具 / 容器 / 工具柄 / 桶 / 弓 / 弩 / 火器木托 / 训练/工程木件**。

**产物一律不带美观**；要美观去 Crafts_Lapidary。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐 + 扩展

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `saw_line` | 锯解 | Hand Saw Frame | Pit Saw Bench | Water-Powered Sawmill |
| `joinery` | 榫卯/装配 | Mortise-Tenon Bench | Clamp + Lever Drill | Plane/Drill Press + Jigs |
| `lathe` | 车削 | — | Treadle Lathe | Improved Lathe + Gauges |
| `drying_shed` | 干燥 / 蒸弯 | Drying Shed | Vented Drying Bay | Low-Temp Drying Kiln |
| `cooperage_bench` | 桶匠 | Cooper's Bench (wood hoop) | Cooper's Bench (iron hoop) | Standardized Cooper's Bench |
| `bowyer_bench` | 弓匠 | Bowyer's Bench | Bowyer's Bench (long bow / composite) | Bowyer & Stockmaker Bench |
| `marquetry` | 镶嵌（R 装饰）| — | — | Marquetry Bench |

**说明**: M `joinery` → 装配用时 −10%；R 用时 −20%。M `lathe` 开启；R 增加精度档。`saw_line` 升级 → 锯解产能 1.0/1.25/1.50。`drying_shed` M = 抗变形 tag；R = 抗变形 + 耐久+10%。

---

## 3) 配方索引（按 era）

### 3.1 基础材料（核心）

| 原料 → 半成品 | C / M / R 产量 | 说明 |
|---|---|---|
| `log ×1` → `boards ×N` | 12 / 14 / 16 | 锯解 saw_line |
| `log ×1` → `beam ×N` | 2 / 3 / 4 | 重型梁，工程件 |
| `boards ×1` → `wood_rod ×N` | 12 / 14 / 16 | 木杆（箭杆/标枪杆/工具柄/弩矢杆原料）|
| `boards ×1` → `wood_part ×N` | 10 / 12 / 14 | 木工零件（小五金/轮轴/枢件统称）|

**统一映射（迁移参考）**:
- 木钉/木榫包 → `wood_part ×1`
- 木轮件 ×1 → `wood_part ×3`
- 木轴/枢轴 ×1 → `wood_part ×2`
- 木杆（长/短）→ `wood_rod`

### 3.2 容器与承载

| 物品 | 配方 | 备注 |
|---|---|---|
| 木桶 barrel | `boards ×4 + wood_part ×2` | 主用酒/水 |
| 木箱 bin | `boards ×3 + wood_part ×1` | 储物 |
| 水桶 bucket | `boards ×1 + wood_part ×1` | — |
| 手推车 cart | `boards ×4 + wood_part ×5` | 含轮/轴等效 |
| 矿车 mine cart | `boards ×6 + wood_part ×8` | 含双轮 |
| 木笼 cage | `boards ×2 + wood_part ×1` | 关动物/囚犯 |

**桶匠分支（cooperage_bench）**:
- C: `barrel_small` (boards_oak ×4 + iron_ingot ×0.2)（罗马式 staves + 简单铁箍）
- M: `barrel_wine`（boards_oak ×5 + iron_hoop ×3，防漏，wine/beer 必备）
- M: `barrel_large`（boards_oak ×6 + iron_hoop ×4）
- R: `barrel_aging_oak`（boards_oak_seasoned ×6 + iron_hoop ×4 + 干燥 R）→ 陈酿酒品质 +1
- Tag: white_cooper / wet_cooper 用作产物标签（不开为独立建筑）

### 3.3 家具（DF 主项）

| 物品 | 配方 | 备注 |
|---|---|---|
| 床 bed | `boards ×6 + wood_rod ×2` | |
| 门 door | `boards ×2` | 不带 panel |
| 门（M 起 panel）| `boards ×3` | 美观+1（仍不算"装饰"）|
| 桌 table | `boards ×3` | |
| 椅 / 王座 | `boards ×2` | |
| 柜 / 箱柜 | `boards ×4` | |
| 武器架 | `boards ×4 + wood_part ×2` | |
| 盔甲架 | `boards ×4 + wood_part ×2` | |
| 栅格 grate | `boards ×2` | 防御/通风 |
| 地窖盖 hatch | `boards ×2 + wood_part ×1` | |
| 闸门 floodgate | `beam ×1 + boards ×2` | 水工系统 |

> 用时: 装配类随 M/R 下降 −10%/−20%；锯解类随 M/R 产量上升 ×1.25/×1.50

### 3.4 工程 / 军用木件（对接火器 / 攻城）

| 物品 | 配方 |
|---|---|
| 木制枪托 musket_stock / pistol_stock | `boards ×2 + wood_part ×1`（干燥 ≥C +抗变形）|
| 枪叉 musket_rest | `wood_rod ×1 + wood_part ×1` |
| 枢轴座/回转钩 swivel_mount | `beam ×1 + boards ×2 + wood_part ×3` |
| 野战炮车 field_carriage | `beam ×4 + wood_part ×12` |
| 迫击炮床 mortar_bed | `beam ×3 + boards ×2 + wood_part ×2` |

### 3.5 弓 / 弩 / 箭矢坯

| 物品 | 配方 | 备注 |
|---|---|---|
| 短弓 short_bow | `bow_stave ×1 + sinew_string ×1` (C, bowyer_bench) | DF 兼容 |
| 长弓 long_bow_yew | `bow_stave_yew ×1 + sinew_string ×1` (M) | 英式 |
| 复合弓 composite_bow | `horn ×2 + sinew ×2 + boards_pine ×1` (M) | 草原/中东风格 |
| 木制弩机匣/托 crossbow_stock | `boards ×2`（基础）；`crossbow_stock_R = boards ×1 + crossbow_lock_kit_R ×1`（R 精度+1）| |
| 箭杆 arrow_shaft ×100 | `wood_rod ×10` | |
| 弩矢杆 bolt_shaft ×100 | `wood_rod ×12` | |

> 成品"箭矢/弩矢"金属箭头与最终装配在 Metalworks 完成。

### 3.6 训练与基础工具

| 物品 | 配方 |
|---|---|
| 训练斧 / 训练矛 / 训练剑 | `boards ×1` |
| 拐杖 ×2 | `boards ×1` |
| 夹板 ×4 | `boards ×1` |
| 爬梯 | `boards ×3 + wood_part ×2` |
| 动物陷阱 | `boards ×2 + wood_part ×1` |
| 木盾 / 圆盾 | `boards ×2 + wood_part ×1` |

### 3.7 R 装饰（marquetry / veneer）

| 物品 | 配方 |
|---|---|
| 镶嵌镶板 intarsia_panel | `boards_walnut ×1 + boards_maple ×1 + boards_ebony_traded ×0.5` |
| 贴皮 veneer_sheet | `timber ×1 → veneer_sheet ×3` |

> R 装饰产物**带"R 装饰"质量标签 +4 美观**，但**整体方针仍是"美观去 Crafts"**；marquetry 是少数例外（因木材本身就是装饰）。

---

## 4) 上下游

```
[ Logging Camp ]
   ├─ log → Woodworking.saw_line
   ├─ rodwood → Woodworking (箭杆 / 弩矢杆 / 工具柄原料)
   ├─ bow_stave / bow_stave_yew → Woodworking.bowyer_bench
   └─ boards (已锯) 也可直接到 Woodworking

[ Pasture_Shed / Husbandry ]
   ├─ horn / sinew / sinew_string → Woodworking.bowyer_bench
   └─ goose_feather → (实际在 Metalworks 与箭杆装配)

[ Smeltery / Metalworks ]
   ├─ iron_ingot → 桶箍（cooperage M+）
   ├─ iron_hoop → cooperage
   ├─ crossbow_lock_kit / _R → Woodworking (装配机匣)
   └─ 火器金属部件 → Firearms (装配)

[ Chemistry_Lab ]
   └─ linseed_oil_boiled → Woodworking 木材保护涂料（R）

[ Woodworking 输出 ]
   ├─ boards / beam / wood_rod / wood_part → 全场景输出
   ├─ furniture (bed/table/chair/wardrobe/cabinet/etc.) → 建筑装饰
   ├─ door / floodgate / grate / hatch → 建筑组件
   ├─ barrel/_wine/_large/_aging_oak → Agri_Brew_Works (酒桶) / Salt_Works (盐桶) / Kitchen
   ├─ bow / crossbow_stock → Metalworks (装配)
   ├─ arrow_shaft / bolt_shaft → Metalworks (箭矢装配)
   ├─ musket_stock / pistol_stock / swivel_mount / field_carriage / mortar_bed → Firearms
   ├─ training_weapons / crutches / splints / ladder / animal_trap → 训练/医疗/陷阱
   ├─ intarsia_panel / veneer_sheet → 高档家具装饰
   ├─ cart / mine_cart → 物流系统 / Mining
   └─ cage → 牢/动物管理
```

---

## 5) 危害与特殊

- **木屑/粉尘**: minor stench；推荐通风
- **干燥房失火**: 5%/季；R 低温干燥窑 −80%
- **不带美观**: 设计硬规则（marquetry 例外，因木材本身美观）
- **桶产能限制**: 是酿造系统的实际瓶颈 — 玩家会需要多个 cooperage_bench 才能跟上 brewing 节奏

---

## 6) 与 industry md / 其他工坊的对应

- 设计原理 + 历史锚点（罗马 mortise-tenon / 大教堂 truss / 英长弓 / 莱茵桶匠 / R intarsia）: [Woodworking_Industry_Design.md](../Woodworking_Industry_Design.md)
- 弓 / 弩 / 火器装配最终归 Metalworks / Firearms

（完）
