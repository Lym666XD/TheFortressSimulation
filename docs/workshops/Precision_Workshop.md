# Precision Workshop 精密工坊 ⭐新

**对应 JSON**: `data/core/workshops/core_workshop_precision.json` *(待补)*
**对应 industry md**: — (无独立 industry md；本工坊跨多个产业)
**era**: C → R（核心在 R）
**主要 tags**: workshop, precision, mechanism, clockwork, instrument, automaton

> 来源: 合并自 `../industries/CHATGPT_PROCESS_CHAIN_SOURCE.md §精密工坊` + 与 Metalworks / Crafts_Lapidary 的边界澄清。

---

## 1) 用途与定位

Precision 是**机构件 / 计时 / 导航 / 科学仪器 / 展示自动机**的专用工坊。
- **机构件**: 精密齿轮组、轴/销/滑轮、发条/弹簧、刻度盘（不同于 Metalworks 的"大型机械部件"）
- **计时**: 日晷、水钟、塔钟、便携表
- **导航/科学**: 星盘、臂环仪、磁罗经、戴维斯背尺、经纬仪雏形、暗箱（Camera Obscura）
- **自动机**: 罗马式饮水鸟、水力戏台、天文钟模块、城市天文钟
- **R 终极**: **protype Babbage**（差分机原型，建筑级奇观）

**规则**: 所有产物**不带美观**，只有**质量阶 Q**（Q0 普通 / Q1 精良 / Q2 大师 / Q3 典藏）。装饰动作只能提升 Q，不创建美观；这与 Crafts_Lapidary 的"美观+1"产物明确区分。

**为什么单建**: 精度依赖**车床 + 分度盘 + 擒纵机构 + 磁针校准**等专用工具——这些工具放进 Metalworks 会冲淡其"工业打铁/铸造"主线；放进 Crafts_Lapidary 会冲淡"雕刻/抛光"主线。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `lathe_precision` | 精密车床 | Foot-Pedal Cart Lathe | Treadle Lathe (precision) | Geared Treadle Lathe + Gauges |
| `marking` | 划线 / 分度 | Marking Bench | Gear-Indexing Dial | High-Precision Dividing Head |
| `clock_jig` | 时器治具 | Sundial / Water-Clock Jig | Escapement Jig | Spring Winder + Fine Drill/Mill |
| `magnetic_cal` | 磁校准 | — | Magnetic Needle Calibration Box | (集成入 navigation_bench) |
| `engraving_dial` | 刻盘 | (Crafts 借用) | (Crafts 借用) | Engraving Dial Machine |
| `assembly_bench` | 精密装配 | Small Assembly Bench | Improved Assembly Bench | Precision Assembly Tower |

**说明**:
- C 期能做的只有日晷 / 水钟 / 星盘 / 简单饮水鸟自动机
- M 期解锁塔钟机芯 + 磁罗经 + 自动戏台 → 是"机械文艺复兴前夜"
- R 期解锁便携表 + 暗箱 + 经纬仪雏形 + 差分机原型

---

## 3) 配方索引（按 era）

### 通用机构件（C→R，跨期均可做，质量随期升）

| 配方 | 输入 | 输出 | era | 产能（C/M/R）|
|---|---|---|---|---|
| 精密齿轮组 | 金属条 ×2 | gear_precision ×2 | C→R | 1.0 / 1.15 / 1.25 |
| 轴/销/滑轮 | 金属条 ×1 | shaft_pin ×1 | C→R | 1.0 / 1.15 / 1.25 |
| 发条/弹簧件 | 钢条 ×1 | spring_part ×1 (Q) | **R** | — |
| 刻度盘 | 黄铜片 ×1 | scale_dial ×1 (Q) | **R** | — |

### 计时装置

| 配方 | 输入 | 输出 | era |
|---|---|---|---|
| 日晷 / 水钟 | 石 / 铜 ×2 | fixed_timer ×1 (Q) | C |
| 塔钟机芯 | gear_precision ×4 + shaft_pin ×2 + weight ×1 | tower_clock_movement ×1 (Q) | M |
| 便携表 | gear_precision ×3 + spring_part ×1 + scale_dial ×1 + glass ×1 | pocket_clock ×1 (Q) | R |

### 导航 / 测绘 / 科学仪器

| 配方 | 输入 | 输出 | era |
|---|---|---|---|
| 星盘 astrolabe | brass_plate ×2 + engraved_line ×1 | astrolabe ×1 (Q) | C→M |
| 臂环仪 armillary | brass_ring ×5 + shaft_pin ×1 | armillary_sphere ×1 (Q) | C→R |
| 等距仪 distance gauge | scale_dial ×1 + gear_precision ×2 | distance_gauge ×1 (Q) | M |
| 磁罗经 compass | magnetic_needle ×1 + casing ×1 + scale_dial ×1 | compass ×1 (Q) | M |
| 戴维斯背尺 backstaff | wood_ruler ×1 + scale ×1 | backstaff ×1 (Q) | R |
| 经纬仪雏形 proto-theodolite | small_frame ×1 + scale_dial ×1 + scope_tube ×1 | proto_theodolite ×1 (Q) | R |
| 暗箱 Camera Obscura | wood_box ×1 + lens_reading ×1 + cloth_screen ×1 | camera_obscura ×1 (Q) | R |

> **重要边界**: 不解锁**望远镜 / 显微镜**（严格超出 R 早期上限）。Camera Obscura 是合规上限——它本身在中世纪就有，文艺复兴普及。

### 自动机 / 展示机械

| 配方 | 输入 | 输出 | era | 用途 |
|---|---|---|---|---|
| 饮水鸟 / 自开门 | mechanism ×1 + vessel ×1 | small_automaton ×1 (Q) | C | 房间装饰 / 触发器 |
| 水力戏台 | mechanism ×N + water_wheel_iface ×1 | water_theater ×1 (Q) | M | 节庆 / 公共娱乐 |
| 天文钟模块 | tower_clock_movement ×1 + star_chart_disk ×1 | astro_clock_module ×1 (Q) | M→R | 城市天文钟前置 |
| 城市天文钟（奇观） | astro_clock_module ×N + stained_window ×N | city_astro_clock_building ×1 | R | 建筑级，永久荣誉值 |

### R 终极：protype Babbage 差分机原型

| 配方 | 输入 | 输出 | era |
|---|---|---|---|
| **protype Babbage** | gear_precision ×40 + shaft_pin ×20 + scale_dial ×4 + brass_plate ×20 + screw_set ×N + machine_base ×1 + (可选) quadrant_panel ×2 | protype_babbage ×1 (Q3 典藏) | R 末 |

- **建造条件**: R 期 attachment 满级 + 极长工时 + 占地大
- **效果建议**: 放置于学术区 → 研究/制图/编目速度 +X% **或** 王国声望 +大；**不影响战斗**
- **意义**: 致敬 Babbage（1791-1871），严格说历史上的差分机晚于 R 期 200 年，但作为"机械奇观"上限保留——这是为玩家提供的**仪式性收尾目标**，类似 DF 的 megaproject

---

## 4) 上下游

```
[ Metalworks ]
   ├─ brass_plate / brass_ring / steel_strip / iron_wire → Precision_Workshop
   ├─ machine_base / large_mechanical_part → Precision (差分机)
   └─ screw_set / screw_nut → Precision

[ Smeltery ]
   └─ magnetic_needle (磁铁矿小件) → Precision (compass)

[ Glasshouse ]
   ├─ lens_reading → Precision (Camera Obscura, proto-theodolite)
   ├─ glass → Precision (pocket_clock)
   └─ stained_window → Precision (城市天文钟)

[ Woodworking ]
   ├─ wood_box / wood_ruler / scope_tube → Precision
   └─ frame / small_frame → Precision

[ Crafts_Lapidary ]
   └─ raw_gem (装饰高档表) → Precision (Q+1)

[ Precision_Workshop ]
   ├─ gear_precision / shaft_pin / spring_part → 全局机械（含 Firearms 击发机 R+）
   ├─ tower_clock_movement → 建筑 (塔钟)
   ├─ pocket_clock / compass / astrolabe → 探险家装备 / 贵族礼物 / 贸易
   ├─ camera_obscura → 早期学院 / 制图
   ├─ city_astro_clock → 奇观建筑
   └─ protype_babbage → 学术区效率 buff / 声望
```

---

## 5) 危害与特殊

- **不属于工业危害**: 无塌方、无毒气、无爆炸；只有"精密工件报废"事件
- **质量 Q 阶机制**: 失败时不降本体 Q，但装饰材料浪费
- **学术区配套**: 塔钟 / 天文钟 / 差分机均在学术建筑中触发声望加成 → 与未来 SCHOLARSHIP_ACADEMY_SPEC 接入
- **R 末解锁条件**: 差分机要求玩家工坊有 **R 期所有 attachment 满级** + 学院型建筑 + 一名 Q2+ 工匠

---

## 6) 与其他工坊的边界（重要）

| 物品 | 由 Precision 产 | 由 Metalworks 产 | 由 Crafts_Lapidary 产 |
|---|---|---|---|
| 一般金属砌块 / 链条 / 工具 / 武器 | ❌ | ✅ | ❌ |
| 大型机械部件（机构件，>1 米） | ❌ | ✅ | ❌ |
| 精密齿轮组 / 轴销 / 发条 / 刻度盘 | ✅ | ❌ | ❌ |
| 钟表 / 仪器 / 自动机 | ✅ | ❌ | ❌ |
| 杯 / 盘 / 镶嵌 / 雕像 / 王冠 | ❌ | ❌ | ✅ |
| 宝石切磨 | ❌ | ❌ | ✅ |
| 兽脂蜡烛 | ❌ | ❌ | ✅（用 cast_station 改作 wax）|

---

## 7) 与 industry md / Process Chain 的对应

- 源材料: `../industries/CHATGPT_PROCESS_CHAIN_SOURCE.md §精密工坊（Precision Workshop）`（已整合到本文档）
- 未来未单独写 industry md；如需"精密 / 计时 / 导航 史"主题叙事，可后续补 `docs/Precision_Industry_Design.md`

（完）
