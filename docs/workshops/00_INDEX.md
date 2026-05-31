# 工坊文档索引（Workshops Index）

**目的**: 工坊层文档（per-workshop reference）。与 `data/core/workshops/*.json` 一一对应；同时是 `docs/industries/*.md`（产业级叙事）与 PROCESS_CHAIN 历史档的**实现层落点**。

**版本约定**: 工坊文档随对应 JSON 更新；JSON 是真理来源，markdown 是人类阅读层。

---

## 工坊清单（24 个 = 20 现有 + 4 新）

### A. 原料采集（4）
| 工坊 | 时代 | 对应 JSON | 主要产出 |
|---|---|---|---|
| [Mine_Site](Mine_Site.md) ⭐新 | C→R | （待补） | ore / raw_stone / raw_gem / raw_clay |
| [Salt_Works](Salt_Works.md) ⭐新 | C→R | （待补） | salt_coarse / salt_fine |
| [Logging_Camp](Logging.md) | C→R | core_workshop_logging.json | timber / boards / rodwood / bow_stave |
| [Fishery](Fishery.md) ⭐新 | C→R | （待补） | fresh_fish / dried_fish / pearl / fish_oil |

### B. 农牧食饮（5）
| 工坊 | 时代 | 对应 JSON |
|---|---|---|
| [Agri_Brew_Works](Agri_Brew_Works.md) | C→R | core_workshop_agri_brew_works.json |
| [Pasture_Shed](Pasture_Shed.md) | C→R | core_workshop_pasture_shed.json |
| [Butchery](Butchery.md) | C→R | core_workshop_butchery.json |
| [Compost_Pit](Compost.md) | C→R | core_workshop_compost.json |
| [Kitchen](Kitchen.md) | C→R | core_workshop_kitchen.json |

### C. 冶炼/材料/化学（5）
| 工坊 | 时代 | 对应 JSON |
|---|---|---|
| [Smeltery](Smeltery.md) | C→R | core_workshop_metallurgy.json |
| [Glasshouse](Glasshouse.md) | C→R | core_workshop_glass_house.json |
| [Pottery](Pottery.md) | C→R | core_workshop_pottery.json |
| [Chemistry_Lab](Chemistry_Lab.md) | C→R | core_workshop_chemistry_lab.json |
| [Fuel_Alkali_Works](Fuel_Alkali_Works.md) | C→R | core_workshop_fuel_alkali_works.json |

### D. 加工/装备（5）
| 工坊 | 时代 | 对应 JSON |
|---|---|---|
| [Stoneworks](Stoneworks.md) | C→R | core_workshop_stoneworks.json |
| [Metalworks](Metalworks.md) ⭐含 Mint | C→R | core_workshop_metalworks.json |
| [Firearms_Workshop](Firearms_Workshop.md) | M→R | core_workshop_firearms.json |
| [Woodworking](Woodworking.md) ⭐含 cooperage/bowyer | C→R | core_workshop_woodworking.json |
| [Tannery](Tannery.md) | C→R | core_workshop_tannery.json |

### E. 纺织/造纸（2）
| 工坊 | 时代 | 对应 JSON |
|---|---|---|
| [Tailor](Tailor.md) | C→R | core_workshop_tailor.json |
| [Paper](Paper.md) | C→R | core_workshop_paper.json |

### F. 艺术/奇术/精密（3）
| 工坊 | 时代 | 对应 JSON |
|---|---|---|
| [Crafts_Lapidary](Crafts_Lapidary.md) ⭐含蜡烛/工艺品/宝石 | C→R | core_workshop_crafts.json |
| [Alchemy_Workshop](Alchemy_Workshop.md) | R 为主 | core_workshop_alchemy.json |
| [Precision_Workshop](Precision_Workshop.md) ⭐新 | M→R | （待补） |

---

## 工坊上下游依赖图（简化）

```
Mine_Site ──ore──> Smeltery ──ingots──> Metalworks ──parts──> Firearms / Crafts
        ╲                                                          │
         ╲──raw_stone──> Stoneworks ──blocks──> 建筑              │
         ╲──raw_clay──> Pottery ──vessels/tiles──> Kitchen/建筑   │
         ╲──ore_sulfur/saltpeter──> Chemistry_Lab ──acid/powder──┤
                                            │                     │
                                            ↓                     │
                                    Alchemy_Workshop              │
                                                                  │
Logging_Camp ──timber──> Woodworking ──stocks──> Firearms / 建筑 ─┤
            ╲──wood_ash──> Fuel_Alkali_Works ──charcoal/potash──> Smeltery/Chemistry/Glass

Salt_Works ──salt──> Kitchen / Tannery / Husbandry / Chemistry

Agri_Brew_Works ──crops──> Kitchen
                ╲──malt/wort──> Brewing 内嵌
                ╲──flax──> Tailor
                ╲──flowers──> Chemistry (perfume)

Pasture_Shed ──animals──> Butchery ──hide──> Tannery ──leather──> Tailor
                                  ╲──fat──> Crafts (candle) / Chemistry (soap)
                                  ╲──bone──> Crafts / Chemistry (bone_ash)

Fishery ──fish──> Kitchen / 贸易

Glasshouse ──glassware──> Chemistry / Alchemy / 建筑 / 贸易
Pottery ──containers/tiles──> 全场景
```

---

## 文档约定

- **每份工坊 md** 含：用途、attachment_slots 表（C/M/R 升级链）、配方索引（按 era）、上下游、危害、与 industry md 的对应
- **跨工坊配方**：如果一个配方跨 2 个工坊（例：金属箭头 + 木箭杆 → 完整箭矢），在两份 md 都标注，但**主要工坊**承担"装配最终步骤"
- **JSON 与 md 对齐**：md 中的 attachment 名必须与 JSON 中的 attachment id 对得上；md 是人类阅读层，JSON 是机读真理

## 历史档案

- `docs/CHATGPT_PROCESS_CHAIN.md`：本批 markdown 拆分的源材料；已**归档**（移到 docs/archive/ 或保留只读）
- `docs/<Industry>_Industry_Design.md` × 18：产业级叙事，**保留**；每份顶部加"对应工坊"链接

（完）
