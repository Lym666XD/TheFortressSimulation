# Salt Works 盐场工坊 ⭐新

**对应 JSON**: `data/core/workshops/core_workshop_salt_works.json` *(待补)*
**对应 industry md**: [Salt_Industry_Design.md](../industries/Salt_Industry_Design.md)
**era**: C → R
**主要 tags**: workshop, salt, evaporation, mining_lite

---

## 1) 用途与定位

Salt Works 是**盐**的唯一来源工坊。三种生产形态由 attachment 决定：
- **海/咸湖晒盐**（地中海式）— 仅可建于沿海/咸水湖地形
- **岩盐采矿**（与 Mine_Site 协作，但本工坊负责后续粉碎装袋）
- **卤水煮盐**（内陆 / 北欧 / 沼泽）

R 期招牌：**Graduation Tower (Gradierwerk)** — 浓缩卤水大幅减少煮盐燃料。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `sea_pan` | 海盐多级蒸发塘 | Single Solar Pan | Multi-Stage Atlantic Pans + Sluice | (替换为更大池) |
| `brine_source` | 卤水井 / 卤泉 | Brine Spring Catchment | Drilled Brine Well + Hand Crank | Deep Brine Well + Horse Whim |
| `boil_pan` | 煮盐 | Clay Pot Boiler | Lead/Wood Pan Boiler | Cast Iron Salt Pan |
| `rock_salt_processing` | 岩盐粉碎 | Hand Crusher | Stamp Mill | Roller Mill |
| `graduation_tower` | 浓卤塔 | — | — | Gradierwerk Tower |
| `refinery` | 精盐 | — | — | Dissolve-Precipitate Bench |
| `packing` | 装袋 | Salt Crock Bench | Salt Sack Line | Standardized Sack Line |

**说明**:
- C 期煮盐 = 陶罐 + 柴；M 期煮盐 = 铅锅/木锅 + charcoal；R 期 = 铸铁锅 + Gradierwerk → 单位盐燃料 1.5 → 1.0 → 0.3
- Gradierwerk 是 R 招牌：把弱卤水（5%）浓缩到强卤水（20%）后再煮，燃料节约 80%
- Graduation tower 需要"开放风口"地形（map tag）

---

## 3) 配方索引（按 era）

### C
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 太阳晒盐 | seawater + 烈日工期 (季节×3 夏 / ×0.3 冬) | salt_coarse ×N | sea_pan |
| 岩盐采集（露天）| 岩盐矿脉 | rock_salt_raw ×N + rubble ×小 | rock_salt_processing |
| 陶罐煮盐 | brine ×10 + charcoal ×3 | salt_coarse ×2 | boil_pan |

### M
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 大型盐田 | seawater (季节性) | salt_coarse ×N×1.5 | sea_pan(M) |
| 卤水井煮盐 | brine ×10 + charcoal ×2.5 | salt_coarse ×2.5 | brine_source + boil_pan(M) |
| 岩盐深井 | 深层岩盐矿脉（依赖 Mine_Site M）| rock_salt_raw ×N×1.5 | rock_salt_processing |

### R
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| Gradierwerk 浓卤 | brine_weak ×10 | brine_strong ×3（后续煮盐燃料 ×0.2）| graduation_tower |
| 铁锅煮盐（精盐） | brine ×10 + charcoal ×1.5 | salt_fine ×3 | boil_pan(R) |
| 盐精化 | salt_coarse ×3 | salt_fine ×2 + brine_residue ×1 | refinery |

---

## 4) 上下游

```
[ MAPGEN 地形属性 ]
    ├─ sea / coastal lake → 启用 sea_pan
    ├─ brine_spring → 启用 brine_source
    └─ rock_salt_vein → 与 Mine_Site 协作

[ Salt Works ]
    ├─ salt_coarse / salt_fine → Kitchen (腌制/调味/香料盐)
    │                          → Tannery (white leather)
    │                          → Pasture_Shed (奶酪)
    │                          → Fishery (腌鱼/熏鱼前处理)
    │                          → Chemistry_Lab (HCl 前体 + 杂用)
    │                          → 贸易（内陆盐价 ×3）
    ├─ brine_residue → Chemistry_Lab (含镁卤 bittern)
    └─ rubble → Stoneworks.crushing

输入需求：
- charcoal ← Fuel_Alkali_Works
- iron 大锅 ← Smeltery (R 期铁锅煮盐)
- 木材 / 黑刺李枝 ← Logging (Graduation tower 填料)
```

---

## 5) 危害与特殊

- **季节性**: 海盐塘冬季产能 ×0.3 — Director 应在春夏排盐生产任务
- **地理约束硬性**: 没沿海/咸水/卤泉的内陆地图，只能走岩盐 + 商队进口
- **盐税事件 / 盐道**: 与 incident_director 挂接 — 内陆地图周期性"盐道商队"事件
- **不参与**炼金 / 魔法（盐在炼金里有象征意义，但留给 Alchemy_Workshop 表达）

---

## 6) 与 industry md 的对应

详细 C/M/R 演进 / Hallstatt / Wieliczka / Lüneburg / Salzkammergut 历史锚点 / 平衡值，见：[Salt_Industry_Design.md](../industries/Salt_Industry_Design.md)。

（完）
