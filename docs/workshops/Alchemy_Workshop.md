# Alchemy Workshop 炼金工坊

**对应 JSON**: `data/core/workshops/core_workshop_alchemy.json` *(当前只有 1 个 attachment，需扩充)*
**对应 industry md**: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md)（炼金部分）
**era**: 主要 R；少量 M 前期
**主要 tags**: workshop, alchemy, occult, elixir, transmutation, ritual

---

## 1) 用途与定位

Alchemy 与 Chemistry_Lab 是**两个不同的工坊**：
- **Chemistry_Lab**: 工业化学（酸 / 碱 / 火药 / 油墨 / 颜料）— 拼工艺与产量
- **Alchemy**: **仪式 + 哲学 + 准魔法**（七金属循环、哲人石、长生药、灵魂净化）— 拼时机与神秘

**为什么单独存在**: 在中低魔法世界里，Alchemy 是"魔法系统的现实拟身"。它表现为：
1. 长时间的仪式工序（按月计）
2. 与星象 / 月相窗口绑定的成功率加成
3. 高失败率 + 失败时的事件链（爆炸 / 中毒 / 召唤）
4. 产物作为**神器素材**而非常规工业品

承载历史人物原型：**Paracelsus (1493–1541)** + **Jabir ibn Hayyan** + **Hermes Trismegistus** + **Nicolas Flamel 传说**。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `transmutation_circle` | 金属嬗变 | — | — | Transmutation Circle Table |
| `sublimation_tower` | 升华塔 | — | — | Sublimation Tower |
| `philosopher_retort` | 哲人曲颈瓶 | — | — | Glass Philosopher Retort（依赖玻璃工坊）|
| `spagyric_press` | 草药提纯 | — | — | Spagyric Press & Distiller |
| `star_alignment` | 星象窗口 | — | — | Celestial Alignment Bench（铜浑天仪）|
| `focusing_crystal` | 凝魔水晶 | — | — | Focusing Crystal Holder（消耗 raw_gem / cristallo）|
| `ritual_circle` | 仪式法阵 | — | M-lite 简易盐圈 | Engraved Bronze Circle |
| `homunculus_vessel` | 人造小人器皿 | — | — | Homunculus Vessel（极稀有）|

**说明**:
- 所有核心 attachment 都是 R 期解锁
- M 期"前传": 只有简易盐圈，可执行轻量"驱邪 / 净化"配方（与 PANTHEON_RELIGION 接口）
- 该工坊**不**做 Chemistry 工业品；玩家想做硫酸/硝酸去 Chemistry_Lab

---

## 3) 配方索引（按 era）

### M-lite（前传）
| 配方 | 输入 | 输出 | 工位 | 备注 |
|---|---|---|---|---|
| 驱邪盐圈 | salt_coarse ×3 + lye ×1 + 占卜师 | blessed_salt_ring ×1（房间标签）| ritual_circle | 与宗教/魂灵事件挂接 |

### R（主配方组）
| 配方 | 输入 | 输出 / 概率 | 工位 | 备注 |
|---|---|---|---|---|
| **哲人石碎片**（标志配方） | gold_ingot ×1 + mercury ×1 + sulfur_refined ×1 + alchemic_salt ×1 + 仪式时长 very_long | philosophers_stone_fragment ×0.05（每次 5% 概率成功） | transmutation_circle + philosopher_retort + star_alignment | 累积 20 次产 1 完整哲人石 |
| 完整哲人石合成 | philosophers_stone_fragment ×20 | philosophers_stone ×1（神器级，永久）| transmutation_circle 满级 | 玩家工坊唯一神器产出 |
| **金属嬗变** | iron_ingot ×3 + alchemic_salt ×1 + 仪式 | "purified_iron" ×3（mana_conductivity +20% tag）| transmutation_circle | 不是真变金，是给材料加魔法 tag |
| 嬗变铜 → 银（假性） | copper_ingot ×5 + alchemic_salt ×0.5 + 仪式 | "fool_silver" ×5（外观银，重量铜）| transmutation_circle | 贸易诈骗品/装饰品 |
| **小型长生药** | herb_rare ×3 + spirit_of_wine ×1 + philosophers_stone_fragment ×0.1 + 仪式 | elixir_of_life_minor ×1 | spagyric_press + philosopher_retort | 治疗严重伤病，稀有 |
| 万能解药 | mercury ×0.5 + spirit_of_wine ×1 + herb_bundle ×3 + 仪式 | universal_antidote ×1 | spagyric_press | 解毒事件用 |
| 月相药水 | rosewater ×1 + raw_gem_silver ×0.1 + 满月窗口 | moonlight_potion ×1（提供短期 mana 加成） | star_alignment | 月相窗口加成 +30% 成功率 |
| **同人造小人 homunculus**（极稀有） | mercury ×0.5 + raw_fat ×3 + spirit_of_wine ×1 + alchemic_salt ×2 + 仪式 very_very_long | homunculus_creature ×1（小型 AI 单位）| homunculus_vessel | 与 BESTIARY_SPEC 挂接；致敬 Paracelsus |
| 驱魂 / 净化 | beeswax_candle ×3 + blessed_salt ×1 + 仪式 | exorcism_effect（清场 ghost/wraith）| ritual_circle | 与 BESTIARY 灵体支线挂接 |

**仪式特殊机制**:
- **时长**: very_long (一季) ~ very_very_long (一年)
- **星象窗口**: star_alignment 工位可设定"等待满月 / 月食"，命中窗口 → 成功率 +20–30%
- **失败事件**: 5% 概率工坊爆炸；3% 概率召唤敌对生物（小妖、魂体）；2% 概率施工者中毒（mercury_fume）
- **占卜师 / 炼金师 NPC 加成**: 高技能匠人 +10% 成功率

---

## 4) 上下游

```
[ Chemistry_Lab ]
   ├─ mercury / sulfur_refined / alchemic_salt → Alchemy_Workshop
   ├─ spirit_of_wine → 
   └─ rosewater / herb_bundle →

[ Smeltery ]
   ├─ gold_ingot / silver_grain / copper_ingot / iron_ingot → Alchemy_Workshop

[ Glasshouse ]
   ├─ retort / alembic_glass / cristallo (raw_gem 替代) → Alchemy_Workshop

[ Crafts_Lapidary ]
   └─ raw_gem (focusing_crystal) → Alchemy_Workshop

[ Pasture_Shed / Apiary ]
   └─ beeswax (ritual_candle) → Alchemy_Workshop

[ Alchemy_Workshop ]
   ├─ philosophers_stone_fragment → 累积合成完整哲人石
   ├─ philosophers_stone → 神器（永久 buff，与 MAGIC_SYSTEM 挂接）
   ├─ "purified_iron" → Metalworks (打造 mana-active 武器)
   ├─ elixir_of_life_minor → Kitchen 储藏 / 医疗
   ├─ universal_antidote → 医疗
   ├─ moonlight_potion → 单位 mana buff
   ├─ homunculus_creature → 战场 / 助手单位
   └─ exorcism_effect → 场域净化
```

---

## 5) 危害与特殊

- **爆炸 5%/季**: 防火室建筑必需（与 Chemistry_Lab 同标准）
- **召唤事件 3%/仪式**: 失败时可能召唤小妖 / 魂灵（与 BESTIARY 阿拉伯支线 / 凯尔特森林 / 克苏鲁支线挂接）
- **mercury_fume**: 几乎所有重要配方都用 mercury → 工坊矿工健康事件
- **MANA 浓度**: 工坊周围 N tile 内 mana_field +N，影响敌对生物吸引（远期）
- **声誉 + 信仰冲突**: 在多神/单神文明中，Alchemy 可能被视为**异端** → 影响外交关系
- **塌方风险**: 不存在；本工坊地表建筑

---

## 6) 与 industry md / 软背景的对应

- **化学/工业部分**: [Chemistry_and_Alchemy_Industry_Design.md](../Chemistry_and_Alchemy_Industry_Design.md) — 注意"炼金"章节在 R 期；Chemistry_Lab 工坊承载工业化学。
- **魔法学派**: 待写 `worldbuilding/MAGIC_SYSTEM_SPEC.md` 中"炼金学派"将与本工坊直连
- **神话生物**: 待写 `worldbuilding/BESTIARY_SPEC.md` 的 homunculus / golem / 召唤事件由本工坊触发

---

## 7) 设计要点（给 Codex）

- 本工坊**不**是普通生产线；仪式时长以"季 / 年"为单位，玩家会派炼金师长期投入
- 配方成功率应该**显式**地展示在 UI（让玩家明白这是赌博线，不是稳定产线）
- 月相 / 星象窗口 = 通过 Director / Calendar 系统提供周期事件 → 玩家可"等窗口再施工"
- 失败事件触发**剧情**而非简单错误信息（爆炸 + 召唤怪物 + 中毒都是叙事种子）

（完）
