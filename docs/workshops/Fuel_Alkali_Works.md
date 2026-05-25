# Fuel & Alkali Works 燃料与碱工坊

**对应 JSON**: `data/core/workshops/core_workshop_fuel_alkali_works.json`（7 attachments / 3 slots）
**对应 industry md**: [Forestry_NavalTimber_Design_v1.md](../Forestry_NavalTimber_Design_v1.md)（charcoal 部分） + [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md)（碱部分）
**era**: C → R
**主要 tags**: fuel, alkali, charcoal, potash

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §燃料与碱工坊 v2.0`
> - Forestry md（charcoal 链）
> - Chemistry md（碱液 → potash / soda 链）
> - `core_workshop_fuel_alkali_works.json`

---

## 1) 用途与定位

**两条核心链**:
1. **木材 → 木炭**（charcoal）— 所有冶炼 / 玻璃 / 化学 / 烹饪烤炉 / 火药的核心燃料
2. **木灰 → 碱**（potash / pearlash）— 玻璃 / 皂化 / 化学 / 染整的核心碱原料

**为何独立工坊**: 这两条链共用木材资源与时空（在林边烧炭顺带收灰，灰再浸滤），物理逻辑天然一体；放进 Forestry 显得偏轻工业，放进 Chemistry 显得偏前端原料。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `carbonize` | 木炭炭化 | Charcoal Pit | Mounded Charcoal Kiln | Retort Charcoal Furnace（密闭回收）|
| `leach` | 浸出 + 蒸发 | Ash Bucket & Sieve | Leach Vats & Evap Pans | Pearlash Kiln（高纯）|
| `crystallize` | 结晶 | — | — | Crystallization Room |

---

## 3) 配方索引（按 era）

### 3.1 木炭线

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 木炭坑 | C | wood ×10 | charcoal_std ×8 + wood_ash ×1 | carbonize C |
| 覆土堆烧炭窑 | M | wood ×10 | charcoal_std ×10 + wood_ash ×1 | carbonize M |
| 干馏炭炉（高纯）| R | rodwood ×12 | charcoal_hp ×10 + wood_ash ×1 + (回收) wood_tar ×1 | carbonize R |

**charcoal_hp 加成**（与林业文档对齐）:
- 在冶炼 / 玻璃 / 化学 配方中择一: +10% 批量 OR −1 fuel OR −5% slag

### 3.2 碱液线

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 灰桶浸出 → 粗碱 | C | wood_ash ×10 + water | lye_solution ×8 → potash_crude ×2 | leach C |
| 浸出槽 + 蒸发盘（精化）| M | wood_ash ×10 + water | lye_solution ×10 → potash_refined ×2 | leach M |
| 珍珠灰窑 + 结晶室 | R | potash_refined ×3 | pearlash ×2（高纯，玻璃 cristallo 必需） | leach R + crystallize |

**地中海 / 阿拉伯地区分支**: 海草烧灰 → 苏打粗品
- C: `seaweed ×10 → soda_crude ×2`（仅沿海地形）
- M: `soda_crude ×3 → soda_refined ×2`（再结晶）
- R: `soda_refined → soda_purified`（送 Glasshouse / Chemistry）

### 3.3 副产 / 反馈

| 副产 | 用途 |
|---|---|
| `wood_tar`（R 干馏副产）| 木材防腐 / 船板防水 / 化学（沥青前体）|
| `wood_ash`（每批主产副）| 自循环 → 碱液；也送 Agriculture（施肥 +0.5 肥力，已在 Agriculture md）|

---

## 4) 上下游

```
[ Logging Camp ]
   ├─ wood / rodwood → Fuel_Alkali_Works.carbonize
   └─ wood_tar 回流到防水 / Forestry naval timber 处理

[ Coastal MAPGEN ]
   └─ seaweed → Fuel_Alkali_Works.leach (地中海/阿拉伯支线)

[ Fuel_Alkali_Works 输出 ]
   ├─ charcoal_std → Smeltery / Pottery / Glasshouse / Kitchen / Chemistry (主燃料)
   ├─ charcoal_hp → Smeltery / Glasshouse (cristallo) / Chemistry (高纯)
   ├─ potash_crude/refined / pearlash → Chemistry / Glasshouse / Tailor (染整) / Oil-Soap (皂化)
   ├─ soda_crude/refined/purified → Glasshouse / Chemistry
   ├─ wood_ash → Agriculture (施肥)
   └─ wood_tar → Forestry naval timber 防水 / Chemistry
```

---

## 5) 危害与特殊

- **烟害**: workshop beauty −3；推荐远离居民区
- **明火 / 闷烧失火**: 5% / 季概率；R 干馏密闭 → 1%
- **燃料消耗大户的上游**: Smeltery / Glasshouse / Pottery 三大消耗户都直接喝本工坊的炭

---

## 6) 与 industry md 的对应

- charcoal 工艺时代台阶: [Forestry_NavalTimber_Design_v1.md](../Forestry_NavalTimber_Design_v1.md) §炭线
- potash / pearlash / 碱液: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md) §1A / §M-A
- 苏打（沿海）: 同上 + Glasshouse md

（完）
