# Firearms Workshop 火器工坊

**对应 JSON**: `data/core/workshops/core_workshop_firearms.json`
**对应 industry md**: — *(无独立 industry md；本工坊为 R 期专用)*
**era**: M（轻型火器起步）→ R（火枪/火炮装配核心）
**主要 tags**: workshop, armaments, firearms, artillery

> 合并来源:
> - `../industries/CHATGPT_PROCESS_CHAIN_SOURCE.md §火器工坊 — 精简装配版`
> - `core_workshop_firearms.json`（10 attachments）

---

## 1) 用途与定位

**Firearms 是装配工坊，不是制造工坊**：枪管 / 炮筒 / 木托 / 击发机构 / 火药 / 弹丸都从其他工坊采购，本工坊负责**校直、组装、安全分装**。

- 火药本体: **Chemistry_Lab** R 期 `black_powder`
- 炮筒铸造: **Smeltery** R 期 `gun_metal_ingot` → 本工坊精镗
- 弹丸（铅/铸铁）: **Smeltery** 普通流水
- 石弹: **Stoneworks**
- 燃烧弹: **Chemistry_Lab**
- 木托 / 枪托: **Woodworking** R 期 `musket_stock` / `pistol_stock`

**封顶约束（重要）**: 严格 R 早期 — 燧发枪 flintlock 不解锁（1610s 之后）。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | M | R |
|---|---|---|---|
| `firearm_assembly` | 枪械装配 | Matchlock Assembly Bench | Precision Gunmaker Bench |
| `barrel_station` | 枪管校直/磬口 | Barrel Straighten & Bore Mouth | Straighten + Bore Polish Combo Bench |
| `cannon_assembly` | 炮装配 | Tripod Hoist | Crane/Rail System |
| `carriage_assembly` | 炮架装配 | Carriage Assembly Bench | Limbered & Swivel Mount Bench |
| `trunnion_jig` | 炮耳/轴 | Trunnion Locating Jig | Axle & Recoil Tooling |
| `powder_safety` | 火药安全 | Smoke Room + Powder Cabinet | Safety Partition + Fire-Proof Floor |

**说明**:
- 没有对应 attachment 即不能执行该配方
- `powder_safety` 是建筑层硬约束：未满足 → 触发爆炸事件概率 +20%

---

## 3) 配方索引（按 era）

### M 期（轻型火器 + 早期火炮）

**卡利弗 Caliver（轻型火绳枪）×10**
- 需 attachment: `firearm_assembly` M + `barrel_station` M + `powder_safety` M
- 输入: smooth_barrel_med ×10 + matchlock_mech ×10 + musket_stock ×10 + fastener_set ×40
- 输出: caliver ×10

**枢轴炮 Swivel Gun ×1**（M 直射轻炮）
- 需 attachment: `cannon_assembly` M + `carriage_assembly` M + `trunnion_jig` M + `powder_safety` M
- 输入: swivel_cannon_barrel ×1 + swivel_mount ×1 + axle_bearing ×2 + fastener_set ×12
- 输出: swivel_gun ×1

### R 期（重型火枪 + 野战炮 + 迫击炮）

**火绳火枪 Musket（长重型）×10**
- 需 attachment: `firearm_assembly` R + `barrel_station` R + `powder_safety` M/R
- 输入: smooth_barrel_long_heavy ×10 + matchlock_mech ×10 + musket_stock ×10 + fastener_set ×50 + (可选) musket_rest ×10（添加"持叉"标签）
- 输出: musket ×10

**Saker 野战炮 ×1**（R 直射主力炮）
- 需 attachment: `cannon_assembly` R + `carriage_assembly` R + `trunnion_jig` R + `powder_safety` M/R
- 输入: saker_barrel ×1 + field_carriage ×1 + axle_bearing ×2 + wheel ×2 + fastener_set ×24
- 输出: saker_field_gun ×1

**Mortar 迫击炮 ×1**（R 曲射）
- 需 attachment: `cannon_assembly` R + `carriage_assembly` M/R + `powder_safety` M/R
- 输入: mortar_barrel ×1 + mortar_bed ×1 + fastener_set ×12
- 输出: mortar ×1

### 弹药消耗（运行期，本工坊只提供"装弹"动作）

| 弹药 | 来源 | 与本工坊关系 |
|---|---|---|
| `black_powder` | Chemistry_Lab | 战斗期消耗 |
| `lead_ball` / `iron_round_shot` / `grape_shot` | Smeltery / Metalworks | 战斗期消耗 |
| `stone_shot` | Stoneworks | 投石/部分炮 |
| `incendiary_shell` | Chemistry_Lab | 燃烧弹 |
| `match_cord` | Tailor 或 Chemistry（硝石处理麻绳）| 持续消耗 |

---

## 4) 上下游

```
[ Chemistry_Lab ]
   └─ black_powder, incendiary_shell, match_cord → Firearms

[ Smeltery ]
   ├─ gun_metal_ingot → smooth_barrel_med/long/heavy (本工坊精镗) → 装配
   ├─ saker_barrel / swivel_cannon_barrel (在 Smeltery 铸造，本工坊精修)
   ├─ matchlock_mech 部件 → (本工坊或 Metalworks 制造)
   ├─ axle_bearing, wheel, fastener_set
   └─ lead_ball, iron_round_shot, grape_shot

[ Woodworking ]
   └─ musket_stock, pistol_stock, swivel_mount, field_carriage, mortar_bed, musket_rest

[ Tailor ]
   └─ match_cord 替代来源（麻绳浸硝）

[ Firearms 输出 ]
   ├─ caliver / musket → 军事装备库存
   ├─ swivel_gun / saker_field_gun / mortar → 攻城/守城炮台
   └─ pistol（未列；R 早期可选轻装）
```

---

## 5) 危害与特殊

- **爆炸**: 未装 `powder_safety` → 5% / 季事件；装了 → 1%
- **火源管理**: 工坊周围 N tile 禁止其他热源工坊（与 Smeltery / Glasshouse 必须保持距离）
- **持叉 musket_rest** 是历史细节: 长重火绳枪需要双脚架持叉支撑射击
- **不要**做 flintlock / wheellock 民用化（超期）

---

## 6) 与其他工坊的边界

| 物品 | Firearms | Smeltery | Metalworks | Woodworking | Chemistry |
|---|---|---|---|---|---|
| 炮筒铸造 | ❌（精修）| ✅（gun metal 铸坯） | ❌ | ❌ | ❌ |
| 枪管校直/拉膛 | ✅ | ❌ | ❌ | ❌ | ❌ |
| 击发机部件 | ❌（装配）| ❌ | ✅（小零件） | ❌ | ❌ |
| 木托 | ❌ | ❌ | ❌ | ✅ | ❌ |
| 黑火药 | ❌（消耗） | ❌ | ❌ | ❌ | ✅ |
| 弹丸 | ❌（消耗） | ❌（铅金属） | ✅（铸件）| ❌ | ❌ |
| 整枪装配 | ✅ | ❌ | ❌ | ❌ | ❌ |
| 炮架/限位 | ✅ | ❌ | ❌ | ✅（木件）| ❌ |

（完）
