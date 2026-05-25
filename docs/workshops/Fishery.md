# Fishery 渔业工坊 ⭐新

**对应 JSON**: `data/core/workshops/core_workshop_fishery.json` *(待补)*
**对应 industry md**: — *(暂无独立 industry md；如有需要后续可补 Fishing_and_Aquaculture_Design)*
**era**: C → R
**主要 tags**: workshop, fishing, aquaculture, preservation

---

## 1) 用途与定位

Fishery 是**水产捕捞 + 简易养殖 + 现场预处理**的工坊。"切鱼 / 腌鱼 / 熏鱼"成品交给 Kitchen / Salt 处理；本工坊只做**到岸 + 去内脏 + 初步晾晒**。

地理决定性: 必须建在**沿海 / 湖泊 / 大河**地形。内陆地图本工坊不可建（与 Salt_Works 类似）。

致敬 DF: DF 的渔业极简（"go fish"动作）；本设计扩展到**捕鱼 + 池塘养殖 + 晾鱼架 + 鱼油副产**，但仍保持轻量。

---

## 2) Attachment Slots（含 C/M/R 升级链）

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `fishing_method` | 捕捞方式 | Hand Line / Spear | Net Boat & Trap | Trawl Net & Long Line |
| `drying_rack` | 晾鱼 | Open Drying Rack | Covered Drying Shed | Salt-Dry Combined Shed |
| `gutting_bench` | 去内脏 | Gutting Bench | Improved Bench + Brine Tub | Mechanized Gutting Bench |
| `pond_culture` | 池塘养殖（鲤/鳗）| — | Carp Pond | Stocked Multi-Pond + Sluice |
| `oyster_bed` | 牡蛎/贝养殖 | — | Oyster Bed | Mussel Rope Farm |
| `pearl_bench` | 珍珠采集（罕见）| — | — | Pearl Sorting Bench |
| `fish_oil_press` | 鱼油榨取（副产）| — | Bone Press | Improved Bone Press |

**说明**:
- C 期只有手钓 + 鱼叉（人力）；M 期解锁渔网 + 鱼笼 + 池塘养殖；R 期解锁拖网 + 大规模养殖
- `pearl_bench`: 极罕见（与贸易/奢侈品系统挂接）
- 远洋拖网 / 鲸鱼 / 大型海洋捕捞**不开**（超出 R 早期的低海军基调）

---

## 3) 配方索引（按 era）

### C
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 手钓 | bait ×1（可选）+ 劳力 | fresh_fish ×1–3（按地形） | fishing_method |
| 鱼叉捕捞 | 劳力 | fresh_fish ×2–4 + (溪/河可获 freshwater_fish) | fishing_method |
| 现场去内脏 | fresh_fish ×4 | gutted_fish ×4 + fish_offal ×1（→ 畜禽饲料 / 鱼酱）| gutting_bench |
| 开放晾鱼 | gutted_fish ×4 | dried_fish ×3（开放，受天气影响）| drying_rack |

### M
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 渔网捕捞 | net ×1（耗材，每 N 次磨损）+ 船 + 劳力 | fresh_fish ×6–10 | fishing_method |
| 鱼笼 | trap ×3 + bait ×0.5 | fresh_fish ×3–5 / 季 | fishing_method |
| 池塘养殖（鲤鱼）| fingerlings ×N + feed ×季 | fresh_fish ×N（季末收获）| pond_culture |
| 牡蛎养殖 | oyster_seed ×N + 海水地形 | oysters ×N + (低概率) pearl_seed | oyster_bed |
| 鱼油榨取 | fish_offal ×4 + bones | fish_oil ×1 + bone_meal ×1（→ 化学/堆肥）| fish_oil_press |

### R
| 配方 | 输入 | 输出 | 工位 |
|---|---|---|---|
| 拖网 | trawl_net ×1（耗材）+ 大船 + 劳力 | fresh_fish ×15–25 | fishing_method |
| 长线 | long_line ×1 + bait ×3 | fresh_fish_premium ×5（高档：金枪 / 鲈）| fishing_method |
| 多池养殖 + 闸门 | fingerlings ×N + feed | fresh_fish ×N×1.5 / 季 | pond_culture |
| 贻贝绳养殖 | mussel_seed ×N + 海域 | mussels ×N | oyster_bed |
| 珍珠采集 | oysters ×100 | pearl ×0–2（极低概率）| pearl_bench |
| 改良鱼油 | fish_offal ×4 | fish_oil ×1.5 + bone_meal ×1 | fish_oil_press |

---

## 4) 上下游

```
[ MAPGEN 地形属性 ]
   ├─ sea / coast → 启用 fishing_method (船)
   ├─ river / lake → 启用 fishing_method (鱼叉 / 鱼笼)
   └─ marsh / pond-buildable → 启用 pond_culture

[ Fishery ]
   ├─ fresh_fish / dried_fish → Kitchen (烹饪 / 熏鱼 / 鱼酱)
   ├─ gutted_fish + salt → Kitchen.saltery (M 起做"腌鱼"成品)
   ├─ oysters / mussels → Kitchen (贝类高档菜)
   ├─ fish_offal → Pasture_Shed (家禽饲料) / Kitchen (garum 鱼酱原料 C 期)
   ├─ fish_oil → Chemistry_Lab (蜡烛 / 灯油 / R 早期工业)
   ├─ bone_meal → Chemistry_Lab (bone_ash 替代) / Compost
   └─ pearl → Crafts_Lapidary / 贸易奢侈品

输入需求：
- net / trap / trawl_net / long_line → Tailor.fishing_net_line（轻量配方，麻绳编网）
- boat (M+) → Woodworking.shipbuilding_lite（如未开船，先用 generic_boat 占位）
- bait → 来自烹饪/农业残料
- fingerlings / oyster_seed / mussel_seed → 自然刷新 + Pasture_Shed.aquaculture_buy（贸易输入）
```

---

## 5) 危害与特殊

- **地理硬约束**: 内陆地图无渔业；不报错，UI 直接灰显
- **季节性**: 冬季冰封河面 → C/M 期产能 ×0.4；R 期可破冰捕捞但事故率 +10%
- **过捕**: 同一水域连续 N 季高强度捕捞 → 鱼群恢复时间 +X；与 ecology / 生态系统挂接（远期）
- **风暴 / 海难**: 沿海船型作业，季度小概率船只损失事件
- **腐鱼**: gutted_fish 必须在 2–3 天内晾干或盐腌；否则废弃 + 触发 stench 场域
- **海妖事件**: 与 BESTIARY 阿拉伯支线（marid 水妖）+ 凯尔特 selkie + 克苏鲁深潜者支线接入——远海/雾港罕见事件

---

## 6) 与 industry md / 其他工坊的对应

- 与 Kitchen: fresh_fish / dried_fish 是 Kitchen 的核心原料；保藏（盐腌 / 熏 / 醋渍）在 Kitchen 完成
- 与 Salt_Works: 腌鱼依赖 salt_coarse / salt_fine
- 与 Pasture_Shed: fish_offal 作为家禽辅料；鱼油作畜禽营养
- 与 Tailor: net / long_line 的麻绳编织（轻量配方）
- 与 Woodworking: 船只（M+）；如未实现造船业，暂用 generic_boat 占位
- 与 Crafts_Lapidary: pearl 进入珠宝
- 与 BESTIARY (未来): 海洋神话生物事件钩

---

## 7) 设计要点（给 Codex）

- 渔业是**轻量补全 DF 缺漏**——不要做成"渔业大亨"模拟
- 配方数控制在 ~15 个；attachment ~7 个 slot
- 重点是把"鱼"从 DF 一刀切的"go fish"提升为**地理 → 工艺 → 保藏 → 副产**的小完整闭环
- 珍珠 / 海妖事件作为**叙事种子**而非常规玩法

（完）
