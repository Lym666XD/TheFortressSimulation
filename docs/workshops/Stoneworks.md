# Stoneworks & Builder's Yard 石材与营造工坊

**对应 JSON**: `data/core/workshops/core_workshop_stoneworks.json`
**对应 industry md**: [Building_Industry_Simplified_Design.md](../Building_Industry_Simplified_Design.md)
**era**: C → R
**主要 tags**: stone, cutting, shaping, engraving, lime, mortar, concrete, aggregate, recycling

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §石材工坊 v1.0` + `§石灰&混凝土工坊`
> - `Building_Industry_Simplified_Design.md`（建筑业的石灰链 / 砂浆 / 混凝土全部并入此工坊）
> - `core_workshop_stoneworks.json`（8 个 slot + 23 个 attachment）

---

## 1) 用途与定位

Stoneworks 是**石材精加工 + 石灰窑 + 砂浆/混凝土场 + 骨料回收**的统一工坊。

**核心规则**:
- 原矿石 / 毛石**不能直接建墙** — 必须先在本工坊加工为 `stone_block`
- 石灰链（石灰石 → 生石灰 → 熟石灰 → 砂浆）全部在本工坊
- 混凝土（罗马式火山灰混凝土 + R 改良）也在此
- 接收 **Smeltery 炉渣 + Mine_Site 废石 + Chemistry 化学渣** 作骨料（recycling）

**边界**: 不做石材建筑物本体（建筑工程在 Construction System）；只做"砌块/板/家具/雕像/铭刻 + 砂浆/混凝土"等**建材半成品 + 装饰件**。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `cutting` | 切石 | Hand Stonecutter Table | Water-Powered Saw | Reciprocating Frame Saw + Polisher |
| `forming` | 大件成型 | Carving Frame | Hoist & Jig Frame | Template + Lathe/Grinder |
| `engraving` | 精细件 / 铭刻 | Engraving Bench | Templating & Gauges | Fine Cut & Polish Combo |
| `kiln` | 石灰窑 / 砖窑 | Clamp Kiln | Updraft Kiln | Updraft Kiln R (稳定出砖瓦) |
| `slaker` | 石灰熟化 | Slaking Pit | Standardized Slaking Pit | Slaking & Settling Bench |
| `formwork` | 砂浆 / 混凝土拌合 | Mortar Trough | Mortar Trough M (Improved) | Mixing Yard |
| `crushing` | 骨料粉碎 | Hand Crusher | Stamp Mill | Roller Crusher |
| `neutralizer` | 化学渣中和 | — | Lime Neutralization Pit | Neutralization + Settle Tower |

**说明**:
- `cutting` 升级是吞吐 ×1 / ×1.6 / ×2.0；R 增加抛光质量+1
- `kiln` 共用砖瓦（与 Pottery 平行；建材类砖瓦在此，餐具类陶器在 Pottery）
- `neutralizer` 是接收 Smeltery / Chemistry 化学渣的关键，转化为安全骨料

---

## 3) 配方索引（按 era）

### 3.1 基础切割（原石 → 石块/板料）

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 手工解石 | C | raw_stone ×10 | stone_block ×5 + stone_chip ×1 | cutting C |
| 水力切石 | M | raw_stone ×10 | stone_block ×8 + stone_chip ×1（需水力） | cutting M |
| 框锯精切 + 抛光 | R | raw_stone ×10 | stone_block ×10 + stone_chip ×1 | cutting R |
| 厚板料 | C/M/R | raw_stone ×10 | stone_slab ×10 / 12 / 14 + chip ×1 | cutting + forming |

**矿石品质标签**: 花岗岩 / 大理石 → 装饰等级+1（数量不变）

### 3.2 大件与家具（DF 风格，本工坊主线）

| 物品 | C/M/R 每 10 原石产量 | 工位 |
|---|---|---|
| 石门 / 石闸板 | 3 / 4 / 5 | forming |
| 石桌 / 石椅 | 5 / 6 / 7 | forming |
| 石柜 / 石箱 | 4 / 5 / 6 | forming |
| 石雕像 / 柱础 / 台座 | 2 / 3 / 4（R 解锁"精雕"质量标签） | forming + engraving |
| 石板（纪念碑/铭刻） | 6（C 起即可，铭刻需 engraving） | forming + engraving |

**石磨盘 quern / millstone**: 在本工坊产出，交给 Agri_Brew_Works 使用（DF 兼容）。

### 3.3 现场整形（地形/构筑辅助）

| 动作 | era | 效果 | 工位 |
|---|---|---|---|
| 墙面/地面光顺 smoothing | M+ | 不产物；场所价值 + 移动速度 +5% | engraving M |
| 防御垛口 / 射击孔 | M/R | 已光顺墙 → 开孔（少量 stone_chip ×1）| engraving M/R |

### 3.4 石灰链（C→R）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 石灰煅烧 | limestone/chalk/shell ×1 + fuel ×1 | quicklime ×1 | kiln |
| 熟化 | quicklime ×1 + water ×1（耗时） | slaked_lime ×1 | slaker |
| 空气砂浆 | slaked_lime ×1 + sand ×2 | mortar_air ×6 | formwork |
| 水硬砂浆 | slaked_lime ×1 + pozzolan ×1 + sand ×2 | mortar_hydraulic ×6 | formwork |

### 3.5 罗马混凝土

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 预拌混凝土 | slaked_lime ×1 + pozzolan ×1 + aggregate ×3 | concrete_mix ×6 | formwork（M+ Mixing Yard）|
| 建造地坪/道路 | concrete_mix ×10 | concrete_floor (10 格 / 块) | 现场（建筑系统）|

> pozzolan 来源: 火山地形采集 或 crushed_brick（碎砖粉，来自 Pottery 副产）

### 3.6 骨料回收（recycling，本工坊特色）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 炉渣 → 骨料 | slag (Smeltery 副产) ×10 | aggregate ×8 + dust ×2 | crushing |
| 化学渣 → 骨料（中和稳定）| chem_slag ×10 + slaked_lime ×0.5 | aggregate_safe ×7 + dust ×3 | neutralizer + crushing |
| 废石 → 骨料 | rubble (Mine_Site) ×10 | aggregate ×9 | crushing |

### 3.7 砖瓦（与 Pottery 配合）

> 建筑用砖瓦在 Stoneworks 烧制（kiln slot 共用）；餐具陶器在 Pottery（更复杂釉色 / 装饰）

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 砖 brick | clay ×1 + fuel ×1 | brick ×2 | kiln |
| 屋面瓦 roof_tile | clay ×1 + fuel ×1 | roof_tile ×2 | kiln |
| 装饰瓦 decor_tile | fine_clay ×1 + glaze ×1 + fuel ×1 | decor_tile ×2 | kiln |

### 3.8 投石弹（攻城）

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 投石机弹丸 trebuchet stone | C/M/R | raw_stone ×3 | trebuchet_stone ×5 | cutting + forming |
| 抛石机弹丸 catapult shot | C/M/R | raw_stone ×2 | catapult_shot ×8 | cutting |
| 火炮石弹（替代铁弹）| M/R | raw_stone ×4 | cannon_stone_shot ×2 | cutting + forming（精修需 R 抛光）|

---

## 4) 上下游

```
[ Mine_Site ]
   ├─ raw_stone / raw_marble / raw_granite → Stoneworks (cutting)
   ├─ rubble → Stoneworks.crushing (回收)
   └─ limestone / chalk → Stoneworks.kiln (石灰)

[ Pottery ]
   └─ unfired_brick / unfired_roof_tile → Stoneworks.kiln (协烧)
   ↔ crushed_brick (来自 Pottery 边角) → pozzolan 替代

[ Smeltery ]
   └─ slag (Smeltery 副产) → Stoneworks.crushing (回收)

[ Chemistry_Lab ]
   └─ chem_slag → Stoneworks.neutralizer (中和)

[ Logging ]
   └─ fuel (charcoal/wood) → Stoneworks.kiln

[ Stoneworks 输出 ]
   ├─ stone_block / brick / roof_tile / mortar_* / concrete_mix → 建筑系统
   ├─ stone_door / floodgate / table / chair / coffer / statue → 室内装饰 / 防御
   ├─ stone_slab → 纪念碑 / 铭刻 / 文化系统
   ├─ trebuchet_stone / cannon_stone_shot → 攻城系统
   ├─ quern / millstone → Agri_Brew_Works
   └─ aggregate → 建筑混凝土 / 道路
```

---

## 5) 危害与特殊

- **粉尘**: cutting / crushing 重度作业 → 工坊周围 dust 场域；与卫生/疾病挂接
- **石灰烫伤**: slaker 工位 → 偶发 slaking burn 事件（生石灰遇水放热）
- **塌方风险**: 不存在（地表工坊）
- **环境标签**: workshop beauty 0；周围有"美观地坪"buff（罗马道路 / 装饰瓦）

---

## 6) 与 industry md 的对应

- 设计原理 + 时代台阶: [Building_Industry_Simplified_Design.md](../Building_Industry_Simplified_Design.md)
- 历史锚点（罗马混凝土 / Hypocaust / 文艺复兴雕刻技艺）: 由 industry md 提供

---

## 7) 与 Mine_Site / Pottery 的边界

- 与 Mine_Site: Mine_Site 出 `raw_stone`；Stoneworks 切割 → `stone_block`。**不重叠**。
- 与 Pottery: 建材砖瓦 在 Stoneworks（与 stoneblock 流向相同 — 建筑施工）；餐具/容器/精装釉色陶 在 Pottery。kiln slot 可以共用建模，但配方所属工坊明确不重叠。

（完）
