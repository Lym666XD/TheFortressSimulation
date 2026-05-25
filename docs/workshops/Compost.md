# Compost Pit 堆肥工坊

**对应 JSON**: `data/core/workshops/core_workshop_compost.json`（8 attachments）
**对应 industry md**: [Agriculture_Design_v1.md](../Agriculture_Design_v1.md)（堆肥部分）
**era**: C → R
**主要 tags**: workshop, agriculture, fertilizer, compost

---

## 1) 用途与定位

接收**全场景有机废料**（动物粪 / 屠宰 offal / 厨余 food_residue / 农业 pomace/lees/榨饼 / 垫料 / 木灰），转化为**堆肥 compost / 粪肥 manure** → 送 Agriculture 提肥力。

R 期可同时承担**早期硝床 nitre bed** 作业，给 Chemistry 提供 saltpeter_crude 原料（堆肥 + 草木灰 + 石灰长期堆积）。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `pit` | 堆肥坑 | Open Compost Pit | Walled Compost Pit | Layered Compost Pit |
| `mixing` | 翻堆 | Manual Turning | Crank Turning | Treadle Turning |
| `nitre_bed` | 硝床（R 起接化学）| — | — | Nitre Bed |
| `cover` | 覆盖防雨 | Thatch Cover | Vented Cover | (升级) |

---

## 3) 配方索引

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 普通堆肥 | C/M/R | 任意有机渣 ×N（粪/offal/秸秆/食物渣/榨饼/果渣/酒脚/果皮）| compost ×N（用于 田地 +0.5 肥力，上限 3）|
| 粪肥精制 | M+ | manure ×N + straw ×0.5 | manure_refined ×N（+1 肥力）|
| 硝床 | R | manure ×10 + wood_ash ×3 + lime ×1 + 时长 very_long | saltpeter_crude ×2 (→ Chemistry) |
| 草木灰直接施 | C+ | wood_ash ×N | direct fertilizer +0.5 肥力（不经堆肥）|

---

## 4) 上下游

```
输入（全场景汇集）:
   [ Pasture_Shed ] manure / organic_waste
   [ Butchery ] offal
   [ Kitchen ] food_scraps
   [ Agri_Brew_Works ] pomace / lees / vinasse / yeast_cake (废弃部分)
   [ Forestry / Logging ] 树叶 / 小枝
   [ Fuel_Alkali_Works ] wood_ash 副产

输出:
   [ Agriculture / Field ] compost / manure_refined → 肥力 +0.5/+1（上限 3）
   [ Chemistry_Lab ] saltpeter_crude (R 硝床)
   [ Stoneworks ] (可选) chem_slag 中和后骨料化 — 但 chem_slag 主要走 Stoneworks.neutralizer
```

---

## 5) 危害与特殊

- **stench**: workshop beauty −3；远离居民区
- **疾病风险**: 未盖防雨堆 → 雨季产生 swamp 场域
- **硝床产期 very_long**: 一年以上；可叠加多床并行
- **环境标签**: dark green (脏污)

---

## 6) 与 industry md 的对应

- 农业堆肥 + 肥力公式: [Agriculture_Design_v1.md](../Agriculture_Design_v1.md)
- 硝床 + 接化学 saltpeter: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md) §M-D

（完）
