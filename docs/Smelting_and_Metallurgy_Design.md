# 冶炼与冶金 — 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Smeltery](workshops/Smeltery.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 矿石 → 金属锭（ore → ingot）的全流程；铁/铜/锡/铅/锌/银/金/字模合金；钢的制备；铸造；铸钟。
**Out-of-scope**: 工业革命级转炉（Bessemer 1856）、电解冶金、坩埚钢工业级（Huntsman 1742）。
**Goal**: 补齐 CHATGPT_PROCESS_CHAIN 金属工坊"直接从金属锭开始"的跳步；衔接采矿（ore）+ 林业（charcoal/charcoal_hp）+ 化学（助熔剂、酸洗）+ 金属工坊（消费金属锭）。

---

## 1) 核心原则

- **熔点 + 还原温度梯度**: 铅 < 锡 < 锌 < 银 < 金 < 铜 < 铁 < 钢；C 期只能搞定低中温金属（铜、铅、锡、青铜、初级铁海绵）；M 期可达高炉真正熔铁出生铁；R 期可精炼出钢和分离贵金属。
- **燃料**: charcoal_std → 普通熔炼；charcoal_hp（高纯木炭，林业 R 期）→ +10% 批量 或 −1 燃料 或 −5% 杂质（择一）。
- **助熔剂 (flux)**: 石灰石 / 石英砂 / 萤石（M+）；与化学工坊挂接。
- **副产闭环**: 矿渣 slag → 道路/混凝土骨料；铅渣（litharge）→ 玻璃/釉料；尾烟 → 事件链（污染/瘟疫钩）。
- **不做**真实化学方程；只用整数倍率 + tag。

---

## 2) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（Classical）

**A. 海绵铁 / 块炼炉（bloomery）**
- 链路: ore_iron + charcoal → 块炼炉鼓风（人力风箱）→ 海绵铁块（bloom）→ 锤打去渣 → 熟铁锭
- IO: `ore_iron_concentrated ×10 + charcoal_std ×8 + flux_lime ×1 → bloom_iron ×4`
       `bloom_iron ×4 + 锤打 → iron_ingot ×3 + slag ×2`
- 解锁: **块炼炉（C）**、**人力风箱（C）**、**锻打台（C，与金属工坊共用）**
- 限制: 出"熟铁"而非铸铁；不能直接铸造，只能锻造

**B. 铜 / 锡 / 青铜 (alloying)**
- 链路: ore_copper + flux → 还原炉 → 铜锭；ore_tin → 锡锭；铜+锡按比例 → 青铜
- IO: `ore_copper_concentrated ×10 + charcoal ×6 → copper_ingot ×6`
       `ore_tin_concentrated ×10 + charcoal ×4 → tin_ingot ×7`
       `copper_ingot ×9 + tin_ingot ×1 → bronze_ingot ×10`（10% 锡为青铜标准）
- 解锁: **还原炉（C）**、**配料台（C）**

**C. 铅 / 银（杯灰法 cupellation — 罗马标志技术）**
- 链路: ore_galena (含银方铅矿) → 还原炉 → 含银粗铅 → **杯灰炉**（多孔骨灰皿吸收熔铅留下银粒）
- IO: `ore_galena ×10 + charcoal ×6 → crude_lead ×7`
       `crude_lead ×10 + bone_ash ×2 + 高温 → lead_ingot ×9 + silver_grain ×0.5 + litharge ×1`
- 解锁: **杯灰炉（C）**、**骨灰皿（C）**
- 副产: litharge（黄丹）→ 玻璃工坊使用

**D. 黄金（淘金/熔铸）**
- 链路: ore_gold + 重力分选 → 金粒 → 熔铸 → 金锭
- IO: `ore_gold_concentrated ×10 + charcoal ×4 → gold_ingot ×3`
- 解锁: **金匠熔铸盘（C，常与珠宝工坊共享）**

---

### 中世纪（Medieval）

**A. 高炉 / 高温炉（Stuckofen → Hochofen）— 核心台阶**
- 链路: ore_iron + charcoal + flux 装入大炉 → **水力鼓风**（持续高温）→ 出**生铁**（液态铸铁，含碳 3-4%）
- IO: `ore_iron_concentrated ×10 + charcoal ×6 + flux_lime ×2 → pig_iron ×8 + slag ×3`
- 解锁: **高炉（M）**、**水力鼓风机（M，依赖动力系统）**
- 关键: 生铁脆，**不能直接锻造**，必须先经"精炼"

**B. 精炼炉（fining hearth / osmund / Walloon）**
- 链路: pig_iron → 精炼炉去碳 → 熟铁锭（wrought iron）
- IO: `pig_iron ×10 + charcoal ×3 → wrought_iron_ingot ×7 + slag ×2`
- 解锁: **精炼炉（M）**
- 与 C 期块炼比: 总吞吐 ×2.5；杂质标签 −1（质量+1）

**C. 渗碳钢（cementation steel）**
- 链路: wrought_iron_ingot 装入木炭密封盒 → 长时间高温烧（数日）→ 表面渗碳 → blister steel
- IO: `wrought_iron_ingot ×10 + charcoal ×8 + 时长 very_long → blister_steel ×8`
- 解锁: **渗碳炉（M）**
- 用途: 金属工坊用 blister_steel 替代 iron 产出"钢制"武器/工具（提升品质标签）

**D. 黄铜（calamine cementation — 中世纪标准黄铜法）**
- 链路: copper_ingot + zinc-bearing ore (菱锌矿 calamine) → 密封罐高温 → 锌蒸气渗入铜 → 黄铜
- IO: `copper_ingot ×8 + ore_calamine ×4 + charcoal ×4 → brass_ingot ×10`
- 解锁: **黄铜渗化炉（M）**

**E. Saigerprozess（铜—银分离，14 世纪）**
- 链路: 含银粗铜 + 铅 → 加热 → 铅吸银（铅银合金沉出）→ 再杯灰
- IO: `crude_copper_silver ×10 + lead_ingot ×5 → copper_ingot ×7 + crude_lead_silver ×5`（后再杯灰）
- 解锁: **Saiger 分银炉（M）**

---

### 文艺复兴（Renaissance — Agricola De Re Metallica 时代）

**A. 大型高炉 + 精炼联动（Walloon 标准化）**
- 效果: M 的高炉 → 精炼链工业化；吞吐 +30%，杂质标签 −1
- 解锁: **大型高炉（R）**、**Walloon 精炼炉（R）**

**B. 字模合金（Pb–Sn–Sb）— 印刷术核心**
- 链路: lead + tin + 锑（antimony，新解锁矿种）→ 比例合金 → 字模锭
- IO: `lead_ingot ×7 + tin_ingot ×2 + ore_antimony ×1 → type_alloy_ingot ×10`
- 解锁: **字模合金炉（R）**
- 关键: 该合金硬度 + 低熔点 + 几乎不收缩 → 是印刷术成立的物理前提

**C. 铸钟青铜（bell metal）**
- 链路: 高锡青铜（22-25% 锡）专用配方
- IO: `copper_ingot ×4 + tin_ingot ×1 + flux ×0.5 → bell_bronze_ingot ×5`
- 解锁: **铸钟坑（R）**（与建筑业塔楼挂接）
- 与普通青铜区分: 用于教堂大钟、火炮（早期）、艺术铸像

**D. 火炮青铜（gun metal） — 14-16 世纪标准**
- 链路: 8-12% 锡的青铜，韧性 + 抗膛压
- IO: `copper_ingot ×9 + tin_ingot ×1 → gun_metal_ingot ×10`
- 解锁: 与铸钟坑或专用 **火炮铸造坑（R）** 共用
- 后续: 进入火器工坊铸炮筒

**E. 早期坩埚熔铸（小批量，贵金属/精密件）**
- 链路: 黏土坩埚 → 焦炭/木炭外焰加热 → 小批量熔铸银/金/特殊合金
- 解锁: **坩埚台（R）**
- 注: 不是真正的"坩埚钢"（那是 1742），只是用坩埚做精铸

**F. 水银—金 / 水银—银汞齐法（amalgamation，简化）**
- 链路: 低品位金/银矿 + 水银 → 汞齐 → 蒸馏出汞 → 留下贵金属
- IO: `low_grade_gold_ore ×10 + mercury ×1 → gold_ingot ×2 + mercury ×0.9`（汞循环使用）
- 解锁: **汞齐场（R）**（需化学工坊提供水银 mercury）
- 危害: mercury_fume 场域 → 矿工健康事件
- 用途: 复原西班牙 patio process 的简化版

---

## 3) 物品（Items）

**矿石（来自采矿）**: `ore_iron_concentrated`, `ore_copper_concentrated`, `ore_tin_concentrated`, `ore_galena`, `ore_calamine`, `ore_gold_concentrated`, `ore_antimony`, `low_grade_gold_ore`
**金属锭**: `iron_ingot`, `wrought_iron_ingot`, `blister_steel`, `copper_ingot`, `tin_ingot`, `bronze_ingot`, `brass_ingot`, `gun_metal_ingot`, `bell_bronze_ingot`, `lead_ingot`, `silver_grain` / `silver_ingot`, `gold_ingot`, `type_alloy_ingot`, `pig_iron`
**中间品**: `bloom_iron`, `crude_lead`, `crude_lead_silver`, `crude_copper_silver`
**副产 / 入下游**: `slag`（→建筑骨料）、`litharge`（→玻璃/釉料）、`bone_ash`（→ 杯灰皿 / 化学）、`mercury_fume`（场域）
**助熔剂**: `flux_lime`（石灰石）、`flux_quartz`、`flux_fluorspar`（M+）

---

## 4) 配方索引（按时代）

### 古典（C）
1. **块炼铁**: `ore_iron_conc ×10 + charcoal_std ×8 + flux_lime ×1 → bloom_iron ×4` @块炼炉
2. **锻打去渣**: `bloom_iron ×4 → iron_ingot ×3 + slag ×2` @锻打台
3. **熔铜**: `ore_copper_conc ×10 + charcoal_std ×6 → copper_ingot ×6` @还原炉
4. **熔锡**: `ore_tin_conc ×10 + charcoal_std ×4 → tin_ingot ×7` @还原炉
5. **合金青铜**: `copper_ingot ×9 + tin_ingot ×1 → bronze_ingot ×10` @配料台
6. **粗铅**: `ore_galena ×10 + charcoal_std ×6 → crude_lead ×7` @还原炉
7. **杯灰分银**: `crude_lead ×10 + bone_ash ×2 → lead_ingot ×9 + silver_grain ×0.5 + litharge ×1` @杯灰炉
8. **熔金**: `ore_gold_conc ×10 + charcoal_std ×4 → gold_ingot ×3` @金匠熔铸盘

### 中世纪（M）
9. **高炉出生铁**: `ore_iron_conc ×10 + charcoal_std ×6 + flux_lime ×2 → pig_iron ×8 + slag ×3` @高炉（需水力鼓风）
10. **精炼熟铁**: `pig_iron ×10 + charcoal_std ×3 → wrought_iron_ingot ×7 + slag ×2` @精炼炉
11. **渗碳钢**: `wrought_iron_ingot ×10 + charcoal_std ×8 → blister_steel ×8`（耗时 very_long） @渗碳炉
12. **黄铜**: `copper_ingot ×8 + ore_calamine ×4 + charcoal_std ×4 → brass_ingot ×10` @黄铜渗化炉
13. **Saiger 分银**: `crude_copper_silver ×10 + lead_ingot ×5 → copper_ingot ×7 + crude_lead_silver ×5` @Saiger 炉

### 文艺复兴（R）
14. **大型高炉**: 高炉吞吐 +30%，杂质 −1
15. **字模合金**: `lead_ingot ×7 + tin_ingot ×2 + ore_antimony ×1 → type_alloy_ingot ×10` @字模合金炉
16. **铸钟青铜**: `copper_ingot ×4 + tin_ingot ×1 + flux_quartz ×0.5 → bell_bronze_ingot ×5` @铸钟坑
17. **火炮青铜**: `copper_ingot ×9 + tin_ingot ×1 → gun_metal_ingot ×10` @火炮铸造坑
18. **汞齐金**: `low_grade_gold_ore ×10 + mercury ×1 → gold_ingot ×2 + mercury ×0.9` @汞齐场
19. **坩埚熔铸**: 小批量精铸金/银/合金 → quality+1 标签 @坩埚台

---

## 5) 工坊与建筑

- **块炼炉（C）/ 还原炉（C）/ 杯灰炉（C）/ 锻打台（C）/ 金匠熔铸盘（C）**
- **高炉（M）/ 水力鼓风机（M）/ 精炼炉（M）/ 渗碳炉（M）/ 黄铜渗化炉（M）/ Saiger 分银炉（M）**
- **大型高炉（R）/ Walloon 精炼炉（R）/ 字模合金炉（R）/ 铸钟坑（R）/ 火炮铸造坑（R）/ 汞齐场（R）/ 坩埚台（R）**

---

## 6) 平衡默认值

- **块炼 vs 高炉**: 总产铁倍率 1.0 → 2.5
- **熟铁 vs 钢**: 同重量钢的"品质标签" = wrought+1；游戏内"钢制武器/工具"装备本工坊产出 blister_steel 即可
- **青铜 / 黄铜 / 火炮青铜**: 视为不同物品（不同 mana_conductivity / weight / wear）
- **杯灰分银产率**: 0.5 silver/10 lead；M 期 Saiger 法可叠加
- **charcoal_hp 加成**: 各熔炼配方择一：+10% 批量 OR −1 fuel OR −5% slag（与林业文档一致）
- **mercury 循环**: 汞齐法 90% 回收；10% 损失为 mercury_fume

---

## 7) 与其他系统的挂接

- **采矿**: 唯一矿石来源；ore_concentrated 是合法入口
- **林业**: charcoal_std / charcoal_hp 是唯一合法燃料
- **化学/炼金**: 提供 flux / mercury / saltpeter（间接）/ bone_ash；同时消费 litharge / mercury_fume
- **金属工坊**: 唯一下游消费金属锭；本文档定义"哪些锭可用"，金属工坊定义"用这些锭做什么"
- **火器工坊**: 直接消费 gun_metal_ingot 铸炮筒
- **印刷工坊**: 直接消费 type_alloy_ingot 铸字模
- **珠宝/铸钟**: 直接消费 gold/silver/bell_bronze
- **建筑/混凝土**: 消费 slag 作骨料
- **玻璃工坊**: 消费 litharge 做铅玻璃 / 釉料

---

## 8) 与 DF / PROCESS_CHAIN 的差异

- DF 一步 smelter 完成所有金属；本设计拆为**还原（粗金属）→ 精炼（成品锭）→ 分离（贵金属）→ 合金（混配）**
- PROCESS_CHAIN 金属工坊直接从"金属锭"开始；本设计补齐 ore→ingot 的全部前置链条
- 加入 **生铁 vs 熟铁 vs 钢** 的真实区分，让"钢制武器"成为 M 末/R 期才能批量量产的台阶

---

## 9) 数据字段建议

- **alloys.csv**: `alloy_id, era, components_json, ratio, properties_tag`
- **smelting_recipes.csv**: `id, era, inputs(json), outputs(json), workshop, time, fuel_kind`
- **furnaces.csv**: `id, era, throughput, max_temp_tier, power_req, hazards`

---

（完）
