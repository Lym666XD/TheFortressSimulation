# Metalworks 金属工坊（含铸币 / Mint）

**对应 JSON**: `data/core/workshops/core_workshop_metalworks.json`
**对应 industry md**: — *(本工坊主要承载 PROCESS_CHAIN 的金属成品章节，没有独立 industry md)*
**era**: C → R
**主要 tags**: metal, forge, form, assemble

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §金属工坊`（武器/护甲/工具/构件主体）
> - `core_workshop_metalworks.json`（6 个 attachment slot, 15 个 attachment）
> - **新增 §3.6 Mint 铸币线**（按用户要求并入此工坊）

---

## 1) 用途与定位

接收 **Smeltery** 的金属锭，做出：通用构件（砌块/链/零件/大型机械部件/家具/板条）、工具套件（采矿/伐木/农业）、近战武器、远程武器金属件、护甲、攻城弹头、**铸币**。

**规则**:
- 产物**一律不带美观**；需要美观去 Crafts_Lapidary
- 产能基线 C=1.00 / M=×1.25 / R=×1.50（每升级 forge_form attachment）
- 不做火药武器本体（→ Firearms_Workshop）
- 不做精密机构件（→ Precision_Workshop）

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `forge_form` | 成形/打击 | Hand Hammer & Anvil | Water Hammer | Screw/Lever Press |
| `shear_punch` | 切断/冲孔 | Chisel & Cutting Bench | Lever Shears | Press Shears |
| `draw_chain` | 拉丝/制链 | Hand Draw Plate | Crank Drawing | Fine Drawing |
| `assemble_rivet` | 铆接/装配 | Cold Riveting Bench | Hot Riveting Hearth & Bench | Threaded/Bolted Fitting Jig |
| `limb_heat` | 弓臂退火/回火 | — | Limb Annealing & Tempering Hearth | Isothermal Tempering Bench |
| `heat_source` | 加热源（含火山岩浆可选） | (默认 charcoal) | (默认 charcoal) | Lava Forge（地形升级，可选 — 持有则需加热的配方燃料 = 无）|

**说明**: `heat_source = Lava Forge` 是 R 期地形升级（火山附近），不是必须；持有时所有需加热配方的燃料归零。

---

## 3) 配方索引（按 era）

### 3.1 通用构件

**金属砌块 Blocks**
- C: `metal_ingot ×10 → metal_block ×10`
- M: `metal_ingot ×10 → metal_block ×12`
- R: `metal_ingot ×10 → metal_block ×14`

**链条 Chains**（需 draw_chain）
- C: `metal_ingot ×2 → chain ×5`
- M: `metal_ingot ×2 → chain ×6`
- R: `metal_ingot ×2 → chain ×7`

**小零件 Small Parts**（需 forge_form + shear_punch）
- C: `metal_ingot ×2 → small_part ×6`
- M: `metal_ingot ×2 → small_part ×7`
- R: `metal_ingot ×2 → small_part ×8`

**大型机械部件 Large Mechanical Parts**（需 forge_form M + assemble_rivet M）
- C: `metal_ingot ×6 + small_part ×4 → large_mech_part ×1`（时间长）
- M: 同料 → 时间↓
- R: 同料 → 时间↓↓

**陷阱部件 Trap Kit**（需 assemble_rivet）
- C: `metal_ingot ×2 + small_part ×2 → trap_kit ×1`
- M/R: 同料 → 时间↓ / ↓↓

**金属家具 Metal Furniture**（不带美观；需 assemble_rivet）
- C: `metal_ingot ×4 + chain ×1 + small_part ×2 → metal_furniture ×1`
- M/R: 同料 → 时间↓

**金属板 Plate**（轧机更优；需 forge_form）
- C: `metal_ingot ×10 → metal_plate ×12`
- M（手动轧辊）: `metal_ingot ×10 → metal_plate ×14`
- R（动力轧机）: `metal_ingot ×10 → metal_plate ×16`

**金属条 Strip**（R 扩展；需 shear_punch ≥M）
- R: `metal_plate ×10 → metal_strip ×12`

### 3.2 工具套件（3 线 × 3 时代）

**采矿工具**
- C 古典: `iron×2 + charcoal×1 + water×1 → 采矿工具组(C) ×1`（标签: 重锤+尖凿×2+撬+冷淬）
- M 中世: `iron×3 + chain×1 → 采矿工具组(M) ×1`（大锤+铁楔×4+手摇绞）— 需 assemble_rivet ≥C
- R 文艺: `steel×3 + small_part×2 → 采矿工具组(R) ×1`（钢钎×2+扁楔×4+滚杠/导轨）— 需 assemble_rivet ≥M

**伐木工具**
- C: `iron×2 + timber×1 → 伐木工具组(C) ×1`（钩刀+小框锯+楔×2）
- M: `iron×2 + timber×2 + chain×1 → 伐木工具组(M) ×1`（坑锯+楔×4）
- R: `steel×2 + timber×2 → 伐木工具组(R) ×1`（双人横切锯+楔×4）

**农业工具**
- C: `iron×2 + timber×1 → 农业工具组(C) ×1`（镰+锄+轻犁+扬箕）
- M: `iron×3 + timber×2 + leather×1 → 农业工具组(M) ×1`（重犁/犁壁+长柄镰+连枷）
- R: `steel×2 + timber×1 → 农业工具组(R) ×1`（钢镰+改良犁+耙齿）

### 3.3 弓弩与弹药（金属部分）

**金属弓臂弩**（M 起；与 Woodworking 木制机匣装配）
- M: `steel×2 + small_part×2 + wood_stock×1 → metal_crossbow ×1`（需 limb_heat M + assemble_rivet ≥C）
- R: 同料 → metal_crossbow ×1（标签 durability+20%；需 limb_heat R 等温回火）

**箭头 / 弩矢头**（需 shear_punch ≥C）
- C: `metal_ingot ×1 → arrowhead ×300`
- M: `metal_ingot ×1 → arrowhead ×360`
- R: `metal_ingot ×1 → arrowhead ×420`
- 弩矢头同公式

**箭矢成品装配**（需 assemble_rivet ≥C）
- C: `arrowhead×100 + arrow_shaft×100 + fletching×100 → arrows ×100`
- M: 同料 → arrows ×120；R → ×140
- 弩矢装配同公式（替换为 bolt_shaft）

### 3.4 全量武器（不带美观）

> 规则: 默认需 forge_form ≥C；长柄/复合件还需 assemble_rivet ≥C。
> 产量基准: 每 10 锭出件数 C/M/R 三档（基础 / ×1.25 / ×1.50）

| 类别 | 武器 | C/M/R 每 10 锭产量 |
|---|---|---|
| 斩击 | 战斧 battle axe | 6/8/10（+装配）|
| | 大斧 great axe | 4/5/6（+装配）|
| | 戟 halberd | 5/6/7（+装配）|
| | 钩镰 bill | 5/6/7（+装配）|
| | 短剑 short sword | 8/10/12 |
| | 长剑 long sword | 6/8/10 |
| | 双手剑 two-handed sword | 4/5/6 |
| | 弯刀 scimitar | 6/8/10 |
| 钝击 | 战锤 war hammer | 6/8/10 |
| | 钉头锤 mace | 6/8/10 |
| | 连枷 flail | 5/6/7（+装配）|
| | 重槌 maul | 4/5/6 |
| 穿刺/混合 | 矛 spear | 8/10/12（+装配）|
| | 长枪 pike | 5/6/7（+装配）|
| | 晨星 morningstar | 5/6/7 |
| | 匕首 dagger | 12/15/18 |
| | 镐（武器） | 6/8/10 |
| 远程主件 | 短弓/长弓本体 | (在 Woodworking 制作；本工坊只做金属护金/弓尖升级包 锭×1→升级包×1) |
| | 吹管 blowgun | 锭×2→2（或留给 Woodworking）|
| 投射 | 标枪/投矛 javelin | 锭×2 + 木杆×2 → 2（+装配）|

### 3.5 护甲

> 不带美观。每件独立配方，可组合套装。产量基准: 每 10 锭出件数 C/M/R 三档。

**5.1 古典（C）**
- 罗马式头盔: 6/7/8
- 锁子甲 mail shirt: 5/6/7
- 鳞甲 scale cuirass（+装配；含 small_part ×2）: 5/6/7
- 条片胸甲 segmentata（+装配；含 small_part ×2）: 4/5/6
- 护臂 vambrace: 8/10/12
- 护手（简）: 8/10/12
- 胫甲 greaves: 6/8/10
- 高靴 metal boots: 6/8/10
- 大盾 scutum（+装配；含 chain ×1）: 4/5/6

**5.2 中世纪（M）**
- 鼻盔/碟盔/早期头盔: 6/7/8
- 锁子长袍 hauberk: 5/6/7
- 甲片衣/札甲 brigandine（+装配；含 small_part ×2）: 4/5/6
- 胸甲 breastplate: 5/6/7
- 保肩/臂甲/肘甲: 6/8/10
- 全指护手 gauntlets: 6/8/10
- 大腿板+护膝 cuisses & poleyn: 5/6/7
- 胫甲: 6/8/10
- 板靴 sabatons: 6/8/10
- 盾（圆/鸢/加热）（+装配 chain ×1）: 4/5/6

**5.3 文艺复兴（R）— Maximilian 风格全板甲**
- 条纹头盔（带面甲）: 6/7/8
- 条纹胸甲 + 背甲: 5/6/7
- 全臂甲（含保肩）: 6/8/10
- 复杂护手: 6/8/10
- 全腿甲（含大腿/护膝/胫）: 5/6/7
- 板靴: 6/8/10
- 小圆盾 targe（可选）: 5/6/7

> 注: 不提供"半身甲"（按原 PROCESS_CHAIN 用户决策）

### 3.6 攻城弹头线（按 PROCESS_CHAIN 备注移至本工坊）

| 弹种 | era | 配方 |
|---|---|---|
| 弩炮头 ballista head | C/M/R | metal_ingot ×3 + small_part ×1 → ballista_head ×4 |
| 实心炮弹 round shot | M/R | metal_ingot ×4（铸造）→ round_shot ×3 |
| 实心散弹 grape shot | M/R | metal_ingot ×3 → grape_shot ×40（小铸件）|
| 大型机械部件（攻城用）| C/M/R | 参考 §3.1 重型件 |

**说明**: 投石机/抛石机的石弹在 **Stoneworks**；燃烧弹在 **Chemistry_Lab**。

### 3.7 ⭐ Mint 铸币线（新增，按用户决策并入本工坊）

> Mint 不单独建工坊；作为 Metalworks 的一个**配方组 + 工具升级链**存在。

**所需 attachment**:
- forge_form ≥C（基础锻打）
- shear_punch（必备 — 切币胚）
- (M+ 推荐) assemble_rivet — 用于钱袋封缄

**核心配方**:

| 配方 | era | 输入 | 输出 | 说明 |
|---|---|---|---|---|
| **手锤币 hammered coin** | C/M | metal_ingot ×1 + die_pair ×1（耗损）| coin ×100 | 古希腊罗马—中世纪标准 |
| **掺铅劣币** | C/M | metal_ingot_silver ×0.5 + lead_ingot ×0.5 + die_pair ×1 | debased_coin ×100（事件钩: 名声 −）| 历史上常见的"债务危机"机制 |
| **磨边币 milled coin** | R | metal_ingot ×1 + die_pair_milling ×1 | milled_coin ×100（防剪边；售价 +5%）| Royal Mint 1660s 的简化版本，R 早期已有钱币机的雏形——可保留 |
| **币模制作 die-making** | C→R | steel_ingot ×0.5 + 雕刻师劳力 | die_pair ×N（耐用度有限）| die_pair 在每 N 次铸币后报废 |

**特殊机制**:
- **金属信誉**: 金 / 银 / 铜的纯度由本配方决定；debased 配方 → "假币事件" 触发外交不信 / 商队拒收
- **磨边币 R**: 防剪边（历史上"剪边"是常见盗窃），milled coin 的售价加成代表名誉值
- **铸币权**: 与未来 GOVERNMENT_LAW_SPEC 接入；只有"贵族 / 王室 / 大商会"派系可使用，玩家私铸触发事件
- **不解锁**复式记账法 / 银行 / 信用券（超出 R 早期范畴）

**输出去向**: `coin` → 经济/贸易系统；`die_pair` → 内部消耗。

---

## 4) 上下游

```
[ Smeltery ]
   └─ 各类 metal_ingot → Metalworks (核心输入)

[ Woodworking ]
   ├─ wood_stock → Metalworks (金属弓臂弩装配)
   ├─ tool_handle → Metalworks (工具组装柄)
   └─ arrow_shaft / bolt_shaft / fletching → Metalworks (箭矢装配)

[ Fuel_Alkali_Works ]
   └─ charcoal → Metalworks (加热燃料)

[ Pasture_Shed / Husbandry ]
   └─ goose_feather → fletching → Metalworks (箭矢)

[ Metalworks 输出 ]
   ├─ tool_kits → 采矿/伐木/农业工坊
   ├─ weapons / armor → 军事系统 / 装备
   ├─ metal_furniture → 建筑装饰
   ├─ chains / small_parts / large_mech_parts → 全场景
   ├─ trap_kits → 防御系统
   ├─ siege heads → 攻城武器
   ├─ metalwork_blocks → 建筑
   ├─ arrows / bolts / metal_crossbows → 军事
   ├─ coin → 经济/贸易
   └─ raw input 给 Precision (gear-grade 条 / 板 / 黄铜片)
```

---

## 5) 危害与特殊

- **热害**: workshop heat_w = 400；推荐通风
- **不带美观**: 设计硬规则；想要美观必走 Crafts_Lapidary
- **铸币事件**: debased coin → 名声 / 外交连锁
- **大型机械部件** 是后期玩家"大型工程"的关键零件（齿轮塔、闸门、起重）
- **Lava Forge** R 期可选（地形）: 持有 → 加热配方燃料归零

---

## 6) 与其他工坊的边界（重要）

| 物品 | Metalworks | Smeltery | Crafts_Lapidary | Precision | Firearms |
|---|---|---|---|---|---|
| ingot 制造 | ❌ | ✅ | ❌ | ❌ | ❌ |
| 通用 chains/parts/blocks/furniture | ✅ | ❌ | ❌ | ❌ | ❌ |
| 工具套件 | ✅ | ❌ | ❌ | ❌ | ❌ |
| 普通武器 / 护甲 | ✅ | ❌ | ❌ | ❌ | ❌ |
| 装饰武器 / 仪式甲 | ❌ | ❌ | ✅（提升 Q）| ❌ | ❌ |
| 钱币 / 印模 | ✅（本工坊 §3.7）| ❌ | ❌ | ❌ | ❌ |
| 精密齿轮 / 弹簧 / 钟表 | ❌ | ❌ | ❌ | ✅ | ❌ |
| 火药武器本体 / 炮筒装配 | ❌ | ❌（产 gun_metal）| ❌ | ❌ | ✅ |
| 攻城弹头（金属）| ✅ | ❌ | ❌ | ❌ | ❌ |
| 攻城石弹 | ❌ | ❌ | ❌ | ❌ | （在 Stoneworks）|
| 燃烧弹 | ❌ | ❌ | ❌ | ❌ | （在 Chemistry）|

（完）
