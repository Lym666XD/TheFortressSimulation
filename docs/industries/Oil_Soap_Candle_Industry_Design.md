# 油脂 / 肥皂 / 蜡烛 — 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Butchery](../workshops/Butchery.md) · [Chemistry_Lab](../workshops/Chemistry_Lab.md) · [Crafts_Lapidary](../workshops/Crafts_Lapidary.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 动物油脂熬炼 (tallow/lard)、植物油（橄榄 / 亚麻籽 / 菜籽）、肥皂（castile / aleppo / 软皂）、蜡烛（兽脂烛 / 蜂蜡烛 / 灯心草烛 rushlight）、油灯（与陶瓷油灯挂接）。
**Out-of-scope**: 现代植物油氢化、合成洗涤剂、煤气照明、鲸油 / 鲸蜡（whaling，超出本作"低海军"基调）。
**Goal**: 把农业 / 畜牧 / 化学 / 烹饪 / 纺织都引用了的"油 / 皂 / 蜡 / 灯"三角统一为一个轻量工坊体系；提供"卫生 / 照明 / 礼仪"三种功能价值。

---

## 1) 历史锚点

- **C**: 罗马 / 希腊以**橄榄油**为食用 + 灯油 + 沐浴擦身；动物脂肪用于药用 / 头发 / 简易蜡烛；罗马"肥皂"（Pliny 记载）做美发剂多于洗涤；油灯（陶 oil lamp）普及。
- **M**: **阿勒颇皂 Aleppo soap**（橄榄油 + 月桂油 + 木灰碱，叙利亚 8c 起，传入欧洲后演变为 **Castile soap**）；**牛羊油 tallow candle** 平民照明；**蜂蜡 beeswax candle** 教堂 / 贵族；**灯心草烛 rushlight**（极便宜，灯心草浸牛羊油）；**烛匠公会** chandler guilds 兴起。
- **R**: 大型皂釜规模化生产（Marseille soap, 14-17c 兴起）；**香皂 / 香水皂** 出现；**鲸油**（saltwater whaling 早期 — 严格说 R 末-巴洛克，仅作贸易品保留，不开生产链）；**Spermaceti candle** 鲸蜡烛（17c+，不收）。

---

## 2) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）

**A. 橄榄油（已在 Agriculture C 期梁式压榨完成）**
- 不重复定义；本工坊只承接 olive_oil 作下游用途
- 用途: 食用（烹饪）/ 灯油（油灯）/ 沐浴 / 简易皂前体

**B. 动物脂肪熬炼**
- 链路: 屠宰副产 raw_fat → 熬脂锅 → tallow（牛羊油）/ lard（猪油）
- IO: `raw_fat ×4 → tallow ×3 + cracklings ×1` 或 `lard ×3 + cracklings ×1`
- 解锁: **熬脂锅（C）**
- 注: 已在 Husbandry M 期出现；本工坊把它正式归类

**C. 油灯（陶 oil lamp 燃烧）**
- 链路: chamber 加油 + 棉 / 麻芯 → 点燃
- 燃料: `olive_oil` 或 `tallow`（融化态）
- 解锁: **油灯（C，来自陶瓷工坊）**
- 注: 油灯是 C/M 期主要室内照明

**D. 罗马式皂（医药 / 头发用）**
- 链路: tallow + 木灰碱 → 软皂（不主要用于洗衣）
- IO: `tallow ×1 + lye_solution ×1 → soft_soap ×1`
- 解锁: **皂釜（C，小型）**
- 用途: 医药 / 头发；不是日常清洁

---

### 中世纪（M）

**A. 阿勒颇皂 / Castile soap（核心台阶）**
- 链路: 橄榄油 + 月桂油（可选）+ 木灰碱（精化 potash）→ 沸煮 → 静置成型 → 切块 → 长时间陈化（数月）
- IO: `olive_oil ×3 + (laurel_oil ×0.5 可选) + lye_solution ×1 → soap_castile ×3`（陈化期 very_long）
- 解锁: **大皂釜（M）**、**皂晾架（M）**
- 影响: 真正用于洗涤（衣物 / 身体）；卫生 +1 buff（接入心情 / 疾病系统）

**B. 兽脂烛 tallow candle**
- 链路: 融化 tallow → 多次浸蘸棉 / 麻芯 → 蜡烛
- IO: `tallow ×2 + wick_string ×0.1 → tallow_candle ×8`
- 解锁: **烛匠台（M）**
- 用途: 平民照明；油灯之外的主要光源；气味重（minor stench tag）

**C. 蜂蜡烛 beeswax candle**
- 链路: 蜂蜡 → 模铸或浸蘸 → 蜡烛
- IO: `beeswax ×1 + wick_string ×0.1 → beeswax_candle ×4`
- 解锁: 共用烛匠台
- 用途: 教堂 / 贵族 / 节庆；美观 +2；无异味；售价高 ×3

**D. 灯心草烛 rushlight（极便宜）**
- 链路: 灯心草秆 → 浸 tallow → 干 → 点
- IO: `rush_stem ×10 + tallow ×0.5 → rushlight ×10`
- 解锁: **草烛台（M）**
- 用途: 最便宜照明；燃烧极快（半小时）；穷人之选；提升下层人口士气

**E. 烛匠公会 / chandler 系统**
- 不是节点，是**社会标签**: 当烛匠数量 ≥ N 时获得 "chandler_guild" 标签 → 触发外贸事件 / 公会订单

---

### 文艺复兴（R）

**A. Marseille / Savona 标准化皂业**
- 链路: M 工艺工业化；规模化 + 标准化品质 + 印章保证
- IO: 比 M 期吞吐 +50%；soap_castile_marseille 标签（+10% 售价 + 名声）
- 解锁: **大型皂工坊（R）**、**皂印章（R）**

**B. 香皂 / 玫瑰皂**
- 链路: castile soap + perfume（化学 R 期产物）→ scented soap
- IO: `soap_castile ×1 + perfume ×0.1 → soap_scented ×1`
- 解锁: 共用大皂工坊
- 用途: 奢侈品 / 贸易 / 礼物
- 美观 +1 / 士气 +1

**C. 亚麻籽油（linseed oil — 印刷油墨 + 油画前体）**
- 链路: 亚麻籽（来自农业）→ 螺旋压榨（R 期）→ 亚麻籽油 → 熬制 → boiled linseed oil
- IO: `flax_seed ×4 → linseed_oil ×1 + presscake ×0.5`
       `linseed_oil ×1 + 加热 → linseed_oil_boiled ×1`
- 解锁: **油籽压榨台（R，可与农业螺旋压榨共用）**、**油熬制炉（R）**
- 用途: 印刷油墨（化学/印刷）、油画颜料媒介、木材保护

**D. 油画颜料媒介 (artist's medium)**
- 链路: 颜料粉（化学）+ 亚麻籽油 → 油画颜料
- IO: `pigment_* ×1 + linseed_oil_boiled ×0.5 → oil_paint_* ×3`
- 解锁: **画师调色台（R）**
- 用途: 文艺复兴绘画（接 ART_SYSTEM 占位）

**E. 蜡烛模具化 / 浸蘸塔（chandler tower）**
- 链路: 浸蘸技术从手工提升为多杆批量
- IO: 烛吞吐 +60%
- 解锁: **浸蘸塔（R）**

---

## 3) 物品（Items）

- **油脂**: `raw_fat`, `tallow`, `lard`, `cracklings`（油渣→ 烹饪）、`olive_oil`, `linseed_oil`, `linseed_oil_boiled`, `laurel_oil`（可选 M）
- **碱 / 助剂**: `lye_solution`, `potash_refined`（来自化学）、`perfume`（来自化学 R）
- **皂**: `soft_soap`, `soap_castile`, `soap_castile_marseille`, `soap_scented`
- **蜡**: `beeswax`（来自畜牧蜂箱）、`tallow`（兼蜡烛原料）
- **蜡烛与灯**: `tallow_candle`, `beeswax_candle`, `rushlight`, `oil_lamp`（陶瓷）、`wick_string`（来自纺织）
- **画材**: `oil_paint_*`

---

## 4) 配方索引（按时代）

C: 1) 熬脂 tallow/lard  2) 油灯（陶瓷）  3) 罗马软皂
M: 4) Castile/Aleppo 皂  5) tallow_candle  6) beeswax_candle  7) rushlight  8) 烛匠公会 tag
R: 9) Marseille 皂  10) 香皂  11) 亚麻籽油 + 熬制  12) 油画颜料媒介  13) 浸蘸塔批量蜡烛

---

## 5) 工坊

- **C**: 熬脂锅、皂釜（小型）
- **M**: 大皂釜、皂晾架、烛匠台、草烛台
- **R**: 大型皂工坊、油籽压榨台、油熬制炉、画师调色台、浸蘸塔

---

## 6) 平衡默认值

- **照明亮度 / 时长**:
  - rushlight: 半小时，亮度 1（最弱），廉价
  - tallow_candle: 4 小时，亮度 2，气味
  - beeswax_candle: 6 小时，亮度 3，无气味
  - oil_lamp（橄榄油）: 8 小时 / 一次注油，亮度 2
- **卫生加成 (soap)**: castile / marseille 房间内有皂 → 矮人 / 人物洗漱 → 疾病事件 −30%
- **气味标签**: tallow_candle 触发 minor stench；beeswax 无；rushlight 短期燃烧（5%) 概率引燃
- **皂陈化**: castile 陈化 90 天后才能售卖 (very_long)；可建多架晾架并行

---

## 7) 与其他系统的挂接

- **农业**: olive_oil（C 已实现）、flax_seed → linseed_oil
- **畜牧**: raw_fat / beeswax 主要来源
- **化学**: lye / potash / perfume；皂与油的核心耗材
- **纺织**: wick_string（细麻线）
- **陶瓷**: oil_lamp 容器
- **印刷**: linseed_oil → 油性油墨
- **建筑 / 装饰**: 室内蜡烛配置影响美观 + 房间评分
- **医疗 / 心情**: 肥皂使用 → 疾病 / 心情
- **宗教（未来）**: beeswax candle 是教堂仪式核心 → 与 PANTHEON_RELIGION_SPEC 接入

---

## 8) 与 DF 的差异

- DF 油 / 皂 / 烛是孤立的零散配方；本设计统一为"油 → 皂 / 烛"双下游 + 双功能（卫生 + 照明），并补齐 **rushlight / beeswax / scented soap** 等社会阶层标签

---

## 9) 数据字段建议

- **fats_oils.csv**: `id, source(animal/plant), era, uses_tags, smoke_tag`
- **soaps.csv**: `id, era, hygiene_value, scent, price_mult`
- **candles.csv**: `id, era, burn_time, brightness, smell, price`

---

（完）
