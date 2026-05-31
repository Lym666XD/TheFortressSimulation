# 化学与炼金 — 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Chemistry_Lab](workshops/Chemistry_Lab.md) · [Alchemy_Workshop](workshops/Alchemy_Workshop.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 灰碱 / 苏打 / 蒸馏 / 酸（醋酸 / 硫酸 / 硝酸 / 王水）/ 硝石 / 硫磺 / 黑火药 / 染料前体 / 颜料 / 墨水 / 早期药物 / 炼金传统。
**Out-of-scope**: 元素周期表 / 现代有机化学 / 工业化学合成（19 世纪后）；保留**炼金作为 R 期收尾**，但不实际产出"哲人石"，只产出"高品质催化剂 / 长生药" 作为可收集神器。
**Goal**: 把多处文档（造纸、皮革、玻璃、火器、纺织、肥皂）"假装存在"的化学前体补齐；同时作为**魔法/炼金系统的现实拟身**，为后续 MAGIC_SYSTEM_SPEC 提供物质层挂钩。

---

## 1) 核心原则

- **三层定位**: ①**工业化学**（实用，C/M）②**药物化学 iatrochemistry**（R，Paracelsus 风）③**炼金 alchemy**（R 末，仪式 + 哲学，与魔法系统挂接）
- **数据驱动**: 不模拟分子；用"投入→产出+副产+危害"整数倍率
- **副产闭环**: 酸性废液 → 事件链；硝盐 → 火药；蒸馏残液 → 醋；含汞废 → 健康事件
- **危害**: poison_fume、acid_burn、explosion；R 期可建"防火室"减灾
- **与魔法的连接**: `mana_conductivity`（来自 MATERIALS_SPEC）+ 某些催化剂 tag → 制作"炼金催化物 / 神秘试剂"

---

## 2) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）

**A. 灰碱 / 苏打（基础碱液）**
- 链路: 木灰 + 水浸滤 → 灰汁 → 蒸发 → 粗灰碱（potash 粗品）；或地中海海草烧灰 → 苏打粗品（natron 粗品）
- IO: `wood_ash ×10 + water → lye_solution ×8` → `lye_solution ×8 → potash_crude ×2`
       `seaweed ×10 → soda_crude ×2`
- 解锁: **灰汁缸（C）**、**蒸碱坑（C）**
- 用途: 皂化（肥皂 C/M）、玻璃熔剂（C）、纺织漂白（C/M）

**B. 醋（醋酸）**
- 链路: 低度酒 / 葡萄酒 → 醋母 → 醋酸（已经在酿造业 R 期 vinegar 工坊；本工坊提供 C 期简化版给非酿酒文明）
- IO: `low_abv_liquid ×8 → vinegar ×6` @陶罐发酵堆（C）
- 解锁: **陶罐醋作（C）**

**C. 简易蒸馏（精油 / 蔷薇水 — 阿拉伯—希腊源流）**
- 链路: 植物 + 水 → 简易陶土 alembic → 蒸气冷凝 → 精油 / 蔷薇水
- IO: `flower_petals ×10 + water → essential_oil ×1 + rosewater ×3`
- 解锁: **陶 alembic 台（C）**

**D. 颜料矿物粉（与画家工坊共享）**
- 链路: 矿物碾磨 → 颜料粉
- IO: `mineral_ochre / cinnabar / azurite / malachite ×1 → pigment_* ×4`
- 解锁: **颜料碾盘（C）**

**E. 铁胆墨前体（与造纸 R 期挂接的基础）**
- 链路: 橡瘿 + 醋 → 单宁液
- IO: `oak_gall ×5 + vinegar ×1 → gallic_tannin ×3`
- 解锁: **橡瘿浸提缸（C）**

---

### 中世纪（M — 阿拉伯 + 西欧吸收期）

**A. 苏打 / 灰碱精化**
- 链路: 粗碱 + 多次再结晶 → 较纯 potash / soda
- IO: `potash_crude ×3 → potash_refined ×2 + 副产`
       `soda_crude ×3 → soda_refined ×2`
- 解锁: **再结晶池（M）**
- 用途: 玻璃业 cristallo 前置 / 皂化高纯版

**B. 蒸馏 ethanol（spirit of wine, ~1100 起意大利 Salerno）**
- 链路: 低度酒 → 玻璃/陶 alembic（多次）→ 高度酒精
- IO: `wine ×8 → spirit_of_wine ×3 + vinasse ×1`
- 解锁: **蒸馏室（M）**（与酿造 R brandy 部分共用；可视为"先于 brandy 的工艺前传"）
- 用途: 医药 / 防腐 / 后续化学溶剂

**C. 矾类 (alum, vitriol — 媒染 / 墨水 / 早期酸前体)**
- 链路: 明矾石 / 黄铁矿 → 焙烧 + 水浸 → 结晶
- IO: `ore_alum ×4 → alum_crystal ×2`
       `ore_pyrite ×4 → green_vitriol ×2`（绿矾 = 硫酸亚铁）
- 解锁: **矾焙焜（M）**、**矾结晶槽（M）**
- 用途: 纺织媒染 / 铁胆墨 / R 期硫酸前体

**D. 硝石 (saltpeter)**
- 链路: 厩肥 + 草木灰 + 石灰长期堆肥 → "硝田 nitre bed" 结晶 → 提纯
- IO: `manure ×10 + wood_ash ×3 + lime ×1 + 时长 very_long → saltpeter_crude ×2`
       `saltpeter_crude ×3 → saltpeter_refined ×2`
- 解锁: **硝田（M）**、**硝提炼槽（M）**
- 用途: M 期主要作防腐 / 玻璃熔剂 / 烟火；R 期黑火药核心

**E. 硫磺 (sulfur)**
- 链路: 火山区采硫 → 焙烧分离杂质 → 块硫 / 粉硫
- IO: `ore_sulfur ×5 + charcoal ×1 → sulfur_refined ×3 + so2_fume ×副产`
- 解锁: **硫精炼炉（M）**
- 用途: 黑火药（R 起）、医药、皮革"硫熏"防虫

**F. 铁胆墨（M 标准书写墨水）**
- 链路: 橡瘿单宁 + 绿矾 + 阿拉伯胶 + 水 → 黑墨水
- IO: `gallic_tannin ×3 + green_vitriol ×1 + gum_arabic ×0.5 + water → iron_gall_ink ×4`
- 解锁: **墨房（M）**
- 用途: 抄本 / R 期印刷油墨前置

**G. 早期合成颜料（铅白 / 铜绿）**
- 链路: 铅 + 醋蒸气 → 铅白；铜 + 醋 → 铜绿
- IO: `lead_ingot ×1 + vinegar ×2 → lead_white ×3`
       `copper_ingot ×1 + vinegar ×2 → verdigris ×3`
- 解锁: **腐蚀堆（M）**
- 用途: 颜料 / 化妆品（lead_white 长期使用有毒，事件钩）

---

### 文艺复兴（R — Paracelsus + 早期化学工业 + 炼金哲学）

**A. 硫酸 (oil of vitriol)**
- 链路: 绿矾干馏 → 浓硫酸蒸气 → 玻璃 retort 冷凝
- IO: `green_vitriol ×6 + 高温 → sulfuric_acid ×2 + iron_oxide ×3`
- 解锁: **硫酸蒸馏台（R）**（需 retort，由玻璃工坊提供）
- 危害: acid_burn 场域；副产 iron_oxide 可入颜料

**B. 硝酸 / 王水 (aqua fortis / aqua regia)**
- 链路: 硝石 + 绿矾干馏 → 硝酸；硝酸 + 盐酸 → 王水（溶金标志试剂）
- IO: `saltpeter_refined ×4 + green_vitriol ×2 + 高温 → nitric_acid ×2`
       `nitric_acid ×3 + hydrochloric_acid ×1 → aqua_regia ×3`
- 解锁: **酸蒸馏室（R）**
- 用途: 贵金属分离 / 蚀刻 / 炼金仪式

**C. 黑火药**
- 链路: 硝石 + 硫磺 + 木炭 → 研磨 → 颗粒化
- IO: `saltpeter_refined ×7.5 + sulfur_refined ×1 + charcoal_std ×1.5 → black_powder ×8`
- 解锁: **火药磨坊（R，需水力）**、**颗粒化台（R）**
- 危害: explosion 标签；强制独立"防火磨坊"建筑（与建筑业接入）
- 用途: 火器工坊 / 矿业 R 末爆破

**D. 印刷油墨 (oil-based ink)**
- 链路: 亚麻籽油熬制 + 松烟黑 + 树脂 → 油墨
- IO: `linseed_oil ×3 + lampblack ×1 + pine_resin ×0.5 → printing_ink ×3`
- 解锁: **油墨炼台（R）**
- 用途: 造纸/印刷 R 期印刷所必需

**E. 香料蒸馏 / 香水（早期）**
- 链路: 高度酒精 + 鲜花 → 蒸馏 → 香水
- IO: `spirit_of_wine ×1 + flower_petals ×5 → perfume ×1`
- 解锁: **香水蒸馏室（R）**
- 用途: 贵族奢侈品 / 礼物 / 士气

**F. Paracelsian 药物 (iatrochemistry)**
- 链路: 矿物（汞 / 锑 / 硫）+ 植物 + 蒸馏 → 简易"药剂"
- IO: `mercury ×0.1 + sulfur_refined ×0.5 + herb_bundle ×1 + spirit_of_wine ×0.5 → paracelsian_remedy ×1`
- 解锁: **药剂工台（R）**
- 用途: 医疗工坊（如果存在）；玩家治疗特定疾病；副作用 = 中毒事件
- 注: Paracelsus 死于 1541，正好压"R 早期"上限

**G. 炼金（Alchemy）— R 末仪式工艺**
- 链路: 七金属循环 / 蒸馏 / 升华 / 蒙古结晶 + 神秘配方
- IO（示例）:
  - `gold_ingot ×1 + mercury ×1 + sulfur ×1 + 仪式 → philosophers_stone_fragment ×0.05`（极低概率，逐次累积）
  - `iron_ingot ×3 + 仪式 + alchemic_salt ×1 → "purified_iron"（mana_conductivity +20% 标签）`
  - `herb_rare ×3 + spirit_of_wine ×1 + 仪式 → elixir_of_life_minor ×1`（healing 神器，稀有）
- 解锁: **炼金堂（R）**、**升华塔（R）**
- 特殊: 不是普通制造业；带"仪式时长"、"魔力 / 占星窗口"、"风险事件"；与 MAGIC_SYSTEM_SPEC 的"炼金学派"接入

---

## 3) 物品（Items）

**基础碱/酸/盐**: `lye_solution`, `potash_crude/refined`, `soda_crude/refined`, `vinegar`, `alum_crystal`, `green_vitriol`, `saltpeter_crude/refined`, `sulfur_refined`, `sulfuric_acid`, `nitric_acid`, `hydrochloric_acid`, `aqua_regia`
**蒸馏 / 溶剂**: `essential_oil`, `rosewater`, `spirit_of_wine`, `perfume`
**颜料 / 墨**: `pigment_ochre`, `cinnabar`, `azurite`, `malachite`, `lead_white`, `verdigris`, `iron_oxide`, `lampblack`, `gallic_tannin`, `gum_arabic`, `iron_gall_ink`, `printing_ink`
**炼金 / 药物**: `mercury`, `alchemic_salt`, `paracelsian_remedy`, `philosophers_stone_fragment`, `elixir_of_life_minor`
**火药及前体**: `black_powder`, `slow_match`（火绳）
**副产 / 危害**: `acid_burn`（场域）、`so2_fume`、`mercury_fume`、`explosion_event`、`iron_oxide`（可回收颜料）

---

## 4) 配方索引（按时代）

C: 1)灰汁  2)粗 potash/soda  3)陶罐醋  4)蔷薇水/精油  5)橡瘿单宁  6)颜料碾磨
M: 7)碱精化  8)蒸馏 ethanol  9)alum/绿矾  10)硝石  11)硫磺精炼  12)铁胆墨  13)铅白/铜绿
R: 14)硫酸  15)硝酸/王水  16)黑火药  17)印刷油墨  18)香水  19)Paracelsian 药剂  20)炼金（仪式配方组）

---

## 5) 工坊（按时代分组）

- **C**: 灰汁缸、蒸碱坑、陶罐醋作、陶 alembic 台、颜料碾盘、橡瘿浸提缸
- **M**: 再结晶池、蒸馏室、矾焙焜、矾结晶槽、硝田、硝提炼槽、硫精炼炉、墨房、腐蚀堆
- **R**: 硫酸蒸馏台、酸蒸馏室、火药磨坊（独立防火）、颗粒化台、油墨炼台、香水蒸馏室、药剂工台、**炼金堂**、**升华塔**

---

## 6) 平衡默认值

- **碱液 / 苏打**: M 精化版给玻璃/皂化 +10% 品质
- **硝石产期**: very_long（多季）；可叠加多硝田并行
- **黑火药配比**: 75:10:15 saltpeter/sulfur/charcoal（历史标准）
- **acid_burn / explosion 事件**: 各 3% / 季 / 工坊；防火室 −80%
- **炼金成功率**: 哲人石碎片每次 5%；累积制 30% → 1 完整哲人石
- **Paracelsian 药剂**: 治愈 80%，中毒 20%（无随机不公平：每次造一份独立 roll）

---

## 7) 与其他系统的挂接

- **林业**: wood_ash 是 C/M 碱的核心入口；charcoal 是燃料
- **采矿**: ore_alum, ore_pyrite, ore_sulfur, ore_saltpeter（M 期可由硝田补足）, mercury 来源（ore_cinnabar 焙烧）
- **冶炼**: 提供 lead/copper/iron 用于颜料；接收 litharge / iron_oxide 副产
- **玻璃**: 双向 — soda_purified 给玻璃；玻璃 retort/alembic 给本工坊
- **造纸/印刷**: iron_gall_ink（M）/ printing_ink（R）是必需耗材
- **皮革**: alum（白革）；硫磺熏（防虫）
- **纺织**: alum（媒染）；染料前体
- **火器**: black_powder 是火器工坊与攻城工坊核心耗材
- **酿造**: 醋 / spirit_of_wine 双向
- **MAGIC_SYSTEM**: 炼金堂是炼金学派的施法工位；alchemic_salt / philosophers_stone 是关键神器素材
- **MATERIALS_SPEC**: 炼金 "purified" 后缀的金属获得 mana_conductivity +20% 标签

---

## 8) 与 DF 的差异

- DF 几乎没有化学线，只在工坊里隐式 lye/potash；本设计**结构化**所有酸/碱/盐/染料/颜料/墨水/火药/炼金为一个完整可调度产业
- 把 Paracelsus 与 alchemy 拉入 R 期作为**文化收尾 + 魔法系统的桥**

---

## 9) 数据字段建议

- **chem_reagents.csv**: `id, era, kind(acid/base/salt/solvent/pigment/medicine/explosive/ritual), purity_tier, hazards`
- **chem_recipes.csv**: `id, era, inputs, outputs, byproducts, workshop, time, hazard_tags`
- **alchemy_rituals.csv**: `id, inputs, success_prob, output, side_effects, time_band`

---

（完）
