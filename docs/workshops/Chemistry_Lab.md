# Chemistry Lab 化学工坊

**对应 JSON**: `data/core/workshops/core_workshop_chemistry_lab.json`（28 attachments / 13 slots）
**对应 industry md**: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md)（化学部分）
**era**: C → R
**主要 tags**: chemistry, distillation, acids, powder, soap, silvering

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §化学工坊 v2.1`
> - `Chemistry_and_Alchemy_Industry_Design.md`（化学部分；炼金部分见 Alchemy_Workshop.md）
> - `core_workshop_chemistry_lab.json`

---

## 1) 用途与定位

工业化学：酸（硫酸/硝酸/盐酸/王水）、碱（精化）、火药、油墨、染料前体、颜料、香水、Paracelsian 药剂、皂化、镀银。

**纯水**: 统一命名 `pure_water`；**固废**: 统一为 `chem_slag`（送 Stoneworks.neutralizer）

**输入约定**:
- `saltpeter_powder` 来自农业硝床（Compost / Pasture_Shed）
- `dilute_sulfuric_water` 来自冶炼工坊尾气洗涤（M 洗涤坑 / R 淋洗塔）

**边界**: 炼金仪式 / 哲人石 / 长生药 → **Alchemy_Workshop**；皂化反应 → 本工坊（但成品蜡烛在 Crafts_Lapidary，肥皂可两边均可）

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `distillation` | 蒸馏 | Glass/Ceramic Still | Dual-Train Still | Multi-Pot Still |
| `condenser` | 冷凝 | Coil Condenser | Large Condenser | (同 M) |
| `absorber` | 吸收/洗气 | Settling Trough | Wash Pit | Absorption Tower |
| `sulfuric_line` | 硫酸线 | Alum/Vitriol Drying Rack | Alum/Vitriol Dry-Distillation Furnace | Multistage + Concentrator |
| `nitric_line` | 硝酸线 | — | Strong Acid Still (Nitric) | Nitric Acid Train |
| `hydrochloric_line` | 盐酸线 | — | HCl Generator Pot | HCl Generator Furnace |
| `sublimer` | 硫升华 | Sublimation Furnace | Continuous Sublimer | Multichamber Sublimer |
| `grinder` | 粉碎 | — | Hand Mill & Sieve | Water-Powered Muller |
| `granulator` | 颗粒化（火药）| — | Granulation Basin | Rotary Drum Granulator |
| `dryer` | 干燥 | — | Drying Rack | (同 M) |
| `reactor` | 反应釜（皂化 etc.）| Saponification Kettle | (同 C) | Agitated Saponification Kettle |
| `bench` | 通用台 | — | — | Acid Mixing Bench / Acid-Proof Tank |
| `fumehood` | 通风柜（R 必备）| — | — | Fume Hood Enclosure |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）— 基础工业 / 蒸馏起步

| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 灰汁 | wood_ash ×10 + water | lye_solution ×8 | (与 Fuel_Alkali_Works 共线，本工坊也可)|
| 蔷薇水 / 精油 | flower_petals ×10 + water | essential_oil ×1 + rosewater ×3 | distillation C + condenser |
| 橡瘿单宁 | oak_gall ×5 + vinegar ×1 | gallic_tannin ×3 | reactor C |
| 颜料矿物粉 | mineral_ochre/cinnabar/azurite/malachite ×1 | pigment_* ×4 | (无 attachment 需要) |
| 罗马软皂 | tallow ×1 + lye_solution ×1 | soft_soap ×1 | reactor C |

### 3.2 中世纪（M）— 阿拉伯—西欧吸收期

**碱精化**
- `potash_crude ×3 → potash_refined ×2` @ (与 Fuel_Alkali_Works 共线)
- `soda_crude ×3 → soda_refined ×2`

**Spirit of Wine (乙醇蒸馏)**
- `wine ×8 → spirit_of_wine ×3 + vinasse ×1` @distillation M + condenser

**矾类**（媒染 + 墨水 + R 期硫酸前体）
- `ore_alum ×4 → alum_crystal ×2` @sulfuric_line C + dryer
- `ore_pyrite ×4 → green_vitriol ×2` @sulfuric_line C + dryer

**硝石**（与农业硝床 / Compost 配合）
- `manure ×10 + wood_ash ×3 + lime ×1 + 时长 very_long → saltpeter_crude ×2` @sulfuric_line（提供 dryer）/或在 Compost 工坊产
- `saltpeter_crude ×3 → saltpeter_refined ×2` @grinder + dryer

**硫磺**
- `ore_sulfur ×5 + charcoal ×1 → sulfur_refined ×3 + so2_fume` @sublimer

**铁胆墨**
- `gallic_tannin ×3 + green_vitriol ×1 + gum_arabic ×0.5 + water → iron_gall_ink ×4` @reactor

**铅白 / 铜绿（早期合成颜料）**
- `lead ×1 + vinegar ×2 → lead_white ×3` @reactor + dryer
- `copper ×1 + vinegar ×2 → verdigris ×3` @reactor

**Castile / Aleppo 皂**
- `olive_oil ×3 + (laurel_oil ×0.5 可选) + lye_solution ×1 → soap_castile ×3`（陈化 very_long）@reactor + dryer

### 3.3 文艺复兴（R）— 矿物酸工业 + 火药 + 印刷

**硫酸**（与冶炼联动）
- `dilute_sulfuric_water ×8 → sulfuric_acid ×2 + pure_water ×6` @sulfuric_line R + absorber R
- `green_vitriol ×6 + 高温 → sulfuric_acid ×2 + iron_oxide ×3` @sulfuric_line R + distillation R

**硝酸 / 王水**
- `saltpeter_refined ×4 + green_vitriol ×2 + 高温 → nitric_acid ×2` @nitric_line R + distillation R
- `nitric_acid ×3 + hydrochloric_acid ×1 → aqua_regia ×3` @bench + fumehood

**盐酸**
- `salt_coarse ×3 + sulfuric_acid ×1 → hydrochloric_acid ×2 + sodium_sulfate ×1` @hydrochloric_line + condenser

**黑火药**
- `saltpeter_refined ×7.5 + sulfur_refined ×1 + charcoal_std ×1.5 → black_powder ×8` @grinder R + granulator + dryer
- 危害: explosion 标签；火药磨坊必须独立 + 防火室

**印刷油墨**
- `linseed_oil ×3 + lampblack ×1 + pine_resin ×0.5 → printing_ink ×3` @reactor

**香水**
- `spirit_of_wine ×1 + flower_petals ×5 → perfume ×1` @distillation R

**Paracelsian 药剂**
- `mercury ×0.1 + sulfur_refined ×0.5 + herb_bundle ×1 + spirit_of_wine ×0.5 → paracelsian_remedy ×1`（80% 治愈 / 20% 中毒）@reactor R + distillation R

**Marseille 大型皂工坊**
- 在 reactor R（Agitated kettle）+ 印章工艺；soap_castile 吞吐 +50%

**银镜化学（与 Glasshouse 联动）**
- `silvering_compound`（银镜法）作为镀银玻璃前体 → 送 Glasshouse 完成镀背

---

## 4) 上下游

```
[ Fuel_Alkali_Works ]
   ├─ potash / soda → Chemistry_Lab（精化升级）
   └─ charcoal → Chemistry_Lab (火药 / 燃料)

[ Smeltery ]
   ├─ dilute_sulfuric_water （Wash Pit / Leaching Tower 副产）→ Chemistry.sulfuric_line（关键反向流）
   ├─ green_vitriol / litharge / mercury → Chemistry
   ├─ lead / copper → Chemistry (颜料合成)
   └─ iron_oxide ← Chemistry (硫酸副产) 反向送 Smeltery 颜料

[ Mine_Site ]
   ├─ ore_alum / ore_pyrite / ore_sulfur / ore_saltpeter → Chemistry
   └─ ore_cinnabar → Smeltery mercury（不在本工坊产汞，但本工坊消费汞）

[ Glasshouse ]
   ├─ alembic_glass / retort / flask → Chemistry（关键耗材）
   └─ ← (反向) Chemistry 提供 soda_purified / lime_calcined / manganese_clarifier → Glasshouse

[ Agri_Brew_Works ]
   ├─ wine / vinegar / olive_oil / linseed_oil → Chemistry
   ├─ flower_petals → 蔷薇水 / 香水
   ├─ pine_resin → 印刷油墨
   └─ oak_gall → 铁胆墨

[ Compost / Pasture_Shed ]
   └─ manure → saltpeter_crude 来源（M 硝床）

[ Stoneworks ]
   ├─ limestone / lime → 助熔 / pH 中和
   └─ chem_slag → Stoneworks.neutralizer（关键废物处理）

[ Pasture_Shed ]
   └─ bone_ash → Chemistry / 杯灰 (Smeltery)

[ Chemistry_Lab 输出 ]
   ├─ acids (sulfuric/nitric/HCl/aqua_regia) → 蚀刻 / 炼金 / 金属精炼
   ├─ black_powder → Firearms / Mine_Site (爆破)
   ├─ printing_ink → Paper
   ├─ iron_gall_ink → Paper / 抄写
   ├─ perfume → Crafts / 贸易奢侈品 / 香皂
   ├─ pigment / paint media → Pottery / Crafts / 油画
   ├─ lead_white / verdigris → 颜料 / 化妆品
   ├─ soap_castile/marseille → 卫生
   ├─ paracelsian_remedy → 医疗
   ├─ saltpeter / sulfur_refined → 火药
   ├─ silvering_compound → Glasshouse（镜）
   ├─ alchemic_salt / mercury → Alchemy_Workshop（炼金前体）
   └─ chem_slag → Stoneworks.neutralizer
```

---

## 5) 危害与特殊

- **acid_burn / explosion**: 各 3% / 季 / 工坊；防火室 + fume hood −80%
- **so2_fume**（硫升华 / 硫化矿焙烧）: 工坊周围场域 → 健康事件
- **mercury_fume**（Paracelsian / 镀银）: 同上
- **火药磨坊**: 强制独立建筑 + 防火地面（与建筑业接入硬约束）
- **不**做现代有机化学；保持"古典/中世/Paracelsus"基调

---

## 6) 与 industry md 的对应

- 化学部分: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md)（§1-3 + R 期化学部分）
- 炼金 / 仪式 / 哲人石 / 长生药: [Alchemy_Workshop.md](Alchemy_Workshop.md)（独立工坊）

（完）
