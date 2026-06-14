# Mine Site 矿场综合工坊 ⭐新

**对应 JSON**: `data/core/workshops/core_workshop_mine_site.json` *(待补)*
**对应 industry md**: [Mining_Industry_Design.md](../industries/Mining_Industry_Design.md)
**era**: C → R
**主要 tags**: workshop, mining, extraction, safety

---

## 1) 用途与定位

Mine Site 是矿口/竖井的**地表综合设施**。"挖"的具体动作由引擎层 MININGSYSTEM_SPEC 处理（玩家指定挖掘命令、矿工到达地下、按 tile 进度推进）；本工坊负责：
- **支撑作业**: 工具发放、班次更替、装矿出矿
- **安全**: 支护木 / 通风 / 排水设施
- **勘探**: 矿测、罗盘制图（R 期）
- **辅助工艺**: 选矿（粗）、火裂法（C）、爆破（R 末，依赖化学）

下游主要为 **Smeltery / Stoneworks / Pottery / Chemistry / Fuel_Alkali**（不同矿石/原料分别去不同工坊）。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `adit` | 平窿入口 / 维护 | Pit Entry & Timber Props | Reinforced Adit Mouth | Surveyed Adit Mouth |
| `shaft` | 竖井卷扬 | — | Wooden Windlass | Horse-Whim & Iron Cage |
| `drainage` | 排水 | Hand Buckets | Water-Wheel Pump / Archimedes Screw | Multi-Stage Chain Pump |
| `ventilation` | 通风 | Oil Lamp & Open Adit | Fire-Draught Stack | Cross-Cut Ventilation |
| `firesetting` | 火裂法 | Fire-Setting Bay | — | — |
| `support` | 木支护 | Timber Prop Rack | Standardized Frame | (替换为石灰浆 R) |
| `dressing` | 选矿（粗）| Hand-Picking Table & Wash Trough | Stamp Mill & Jig | Roaster Bed (+焙烧, R) |
| `surveyor` | 矿测 | — | — | Surveyor's Bench (罗盘+链尺) |
| `blast_station` | 爆破（R 末） | — | — | Powder Charging Bay |

**说明**:
- `firesetting` C 期独有；M 期被卷扬+支护替代
- `blast_station` 解锁要求：Chemistry_Lab 已产 `black_powder`
- `surveyor` R 解锁：提供"未探矿脉"显示 + 误挖塌方 −30%

---

## 3) 配方索引（按 era）

### C
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 火裂开采 | 矿脉 + charcoal_std×2 + water×1 | ore_raw ×N + rubble ×0.5 | firesetting |
| 平窿开采 | 矿脉（深层）+ timber_prop ×2 + lamp_oil | ore_raw ×N | adit + support |
| 手选 | ore_raw ×10 | ore_concentrated ×6 + rubble ×4 | dressing |

### M
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 竖井开采 | 矿脉（深层）+ timber_prop ×3 | ore_raw ×N + 深层富矿+15% | shaft + support |
| 马力卷扬 | （恒定能力） | 提矿吞吐 +50% | shaft（马驱动）|
| 水力排水启用 | （恒定能力） | 井深上限 +50% | drainage |
| 水力选矿 | ore_raw ×10 | ore_concentrated ×8 + rubble ×2 | dressing（stamp mill）|

### R
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 深井开采 | 矿脉（深层）+ timber_prop ×3 | ore_raw ×N + 稀有矿种 | shaft + ventilation + drainage 满级 |
| 矿测 | prospect_sample ×3 + 矿测员劳力 | 区域矿脉图（reveal） | surveyor |
| 焙烧 | ore_sulfide ×5 | ore_roasted ×4 + so2_fume | dressing.roaster |
| 爆破开采 | 硬岩 + black_powder ×1 | ore_raw ×N×1.6 + rubble ×N×1.5 | blast_station |

---

## 4) 上下游

```
[ MAPGEN / MININGSYSTEM_SPEC ]
        ↓
   矿脉 (in-tile)
        ↓
[ Mine Site ]
   ├─ ore_iron/copper/tin/lead/silver/gold/sulfur/saltpeter/zinc → Smeltery
   ├─ raw_stone / raw_marble / raw_granite / raw_limestone / raw_chalk → Stoneworks
   ├─ raw_clay → Pottery
   ├─ raw_gem → Crafts_Lapidary
   ├─ ore_alum / ore_pyrite / ore_sulfur / ore_saltpeter → Chemistry_Lab
   ├─ ore_cinnabar → Smeltery.mercury_line / Chemistry (mercury)
   └─ rubble → Stoneworks.crushing (作骨料)

输入需求：
- timber_prop ← Woodworking
- lamp_oil ← Crafts_Lapidary 或 Oil-Soap 子线
- charcoal_std ← Fuel_Alkali_Works (火裂法)
- black_powder ← Chemistry_Lab (R 末爆破)
- 马 ← Pasture_Shed (M 期马力卷扬)
```

---

## 5) 危害与特殊

- **塌方 cave-in**: 基线 5%/季/矿井；M 期标准化支护 −60%；R 期矿测 −30%
- **缺氧 / 毒气**: C/M 概率 3%/矿工/季；ventilation 满级 −80%
- **涌水**: 未启用排水时 5%/季；启用 drainage → 仅 R 期罕见极端事件
- **爆破事故** (R 末): 5% 概率触发塌方 + 邻接矿工受伤
- **mercury_fume** (ore_cinnabar 焙烧): 矿工健康事件 → 路由到化学工坊处理
- **so2_fume** (sulfide 焙烧): 同上

---

## 6) 与 industry md 的对应

详细的历史背景 / C/M/R 工艺演进 / 平衡值，见：[Mining_Industry_Design.md](../industries/Mining_Industry_Design.md)。

本工坊文档 = 实现层；industry md = 叙事 + 设计原理层。

---

## 7) 与 Stoneworks 的边界（重要）

由用户决策：**Mine_Site 与 Stoneworks 分开建设**。理由：
- Mine_Site 建在矿口（地理约束）
- Stoneworks 建在建筑工地附近（地理约束）
- Mining 危害（塌方/缺氧/涌水）vs Stoneworks 危害（粉尘/高温）不同
- 工种分开（矿工 vs 石匠）

接口物：`raw_stone / raw_marble / rubble` 从 Mine_Site 出，到 Stoneworks 进；不在 Mine_Site 做切割成型。

（完）
