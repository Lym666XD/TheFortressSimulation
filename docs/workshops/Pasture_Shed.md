# Pasture Shed 放牧棚

**对应 JSON**: `data/core/workshops/core_workshop_pasture_shed.json`（17 attachments）
**对应 industry md**: [Husbandry_Design_v1.md](../Husbandry_Design_v1.md)
**era**: C → R
**主要 tags**: workshop, husbandry, pasture

> 合并来源:
> - `CHATGPT_PROCESS_CHAIN.md §放牧棚 v1.0`
> - Husbandry industry md
> - `core_workshop_pasture_shed.json`

---

## 1) 用途与定位

DF 的"分区牧场"被本设计升级为**单一建筑 + 围栏区**：棚内外一体化管理（饲槽 / 饮水 / 隔栏 / 越冬 / 清粪 / 挤奶 / 剪毛 / 收蛋）。

**与 DF 关键差异**:
- 引入**干草 + 配合饲料 + 冬季饲喂**（榨饼/酒糟可替代部分干草）
- 挤奶/剪毛/收蛋在本棚内就地完成（DF 在 farmer's workshop）
- **粪污 + 卫生**: 输出粪肥/有机废弃物到 Compost；M/R 附件降低疾病事件

---

## 2) Attachment Slots（含 C/M/R 升级链）— JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `fence_gate` | 围栏门 / 分群 | Fence Gate | Improved Fence + Pens | (升级) |
| `water_trough` | 饮水 | Water Trough | Improved Trough | Auto Water Trough |
| `bedding_rack` | 垫料架 | Bedding Rack | (升级) | (升级) |
| `tether_post` | 牵拴桩 | Tether Post | (同) | (同) |
| `milking_rack` | 挤奶架 | Milking Rack | Improved Milking | (升级) |
| `shearing_rack` | 剪毛架 | Shearing Rack | Improved Shearing | (升级) |
| `nest_box` | 巢箱位 | Nest Box | (升级) | (升级) |
| `hay_store` | 干草棚 / 饲料架 | — | Hay Bay & Feed Rack | (升级) |
| `vent_panel` | 通风窗 + 防潮板 | — | Vent Panel | (升级) |
| `dung_channel` | 粪槽 / 刮粪沟 | — | Dung Channel | Dung Channel R |
| `birth_pen` | 隔栏（产/病栏）| — | Birth/Sick Pen | (升级) |
| `feed_mix_bench` | 饲料拌和台 | — | — | Feed Mix Bench |
| `insulated_wall` | 封闭保温墙体 | — | — | Insulated Wall |
| `disinfect_bath` | 消毒洗槽 | — | — | Disinfect Bath |

---

## 3) 配方索引（按 era / 链）

### 3.1 日常饲养

| 配方 | era | 输入 | 输出 |
|---|---|---|---|
| 补水 | C/M/R | water ×1（按群规模/时段）| 解除口渴计时 |
| 投喂（夏）| C | grass / hay_bale ×1 | 饲槽填充 |
| 冬季饲喂 | M/R | hay_bale ×1 OR feed_mix ×1 | 饲槽填充；缺口≤30% 时 feed_mix 可替代干草 |
| 饲料拌和 | R | hay ×0.7 + food_residue ×0.3 | feed_mix ×1 @feed_mix_bench |

### 3.2 产出作业

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 挤奶 | C→R | 成母畜 + barrel/rock_pot ×1 | raw_milk ×1 | milking_rack |
| 剪毛 | C→R | sheep ×1 | wool_fleece ×1 | shearing_rack（年 1 次）|
| 收蛋 | C→R | nest_box（自动）| egg ×N | nest_box |
| 孵化 | M+ | egg ×N + 适宜温度 | chick ×p（成功率 60–80%） | nest_box |

### 3.3 卫生 / 副产

| 配方 | era | 输出 |
|---|---|---|
| 清粪 | M→R | manure ×1 或 organic_waste ×1 @dung_channel |
| 更换垫料 | C→R | 输入 straw ×1 → 舒适度+ / organic_waste ×0.5 @bedding_rack |
| 群体消毒 | R | （减少疾病事件 −25%）@disinfect_bath |

### 3.4 物种参数（参考 Husbandry md）

| 物种 | 干草需求 | 主要产出 |
|---|---|---|
| 牛 cattle | 成年 1/日 | 奶 / 肉 / 皮 / 脂 |
| 羊 sheep | 1/日 | 毛 / 奶 / 肉 |
| 山羊 goat | 0.5/日（耐贫瘠）| 奶 / 肉 |
| 猪 pig | 0（吃 feed/food_residue）| 肉 / 脂 |
| 马 / 驴 equids | 役用 | 役 / 少量粪 |
| 鸡 / 鸭 / 鹅 | 0（吃谷物 + food_residue）| 蛋 / 肉 / 羽 / 鹅绒 |
| 蜂箱（可选）| — | 蜂蜜 / 蜂蜡（→ Agri_Brew_Works 蜂蜜酒 + Crafts 蜂蜡烛）|

---

## 4) 上下游

```
[ MAPGEN ]
   └─ grass biome → 草地放牧

[ Agriculture / Field ]
   ├─ hay_bale (M+) → Pasture_Shed
   ├─ straw (副产) → 垫料 / 燃料
   └─ feed crops → 通过 Agri_Brew_Works 产 spent_grain / presscake → Pasture R

[ Agri_Brew_Works ]
   ├─ spent_grain / presscake → Pasture R feed_mix
   └─ food_residue 总称 → feed_mix

[ Pasture_Shed 输出 ]
   ├─ raw_milk → Kitchen (奶酪) / 直接饮用
   ├─ wool_fleece → Tailor (纺线/呢)
   ├─ egg → Kitchen
   ├─ live animal → Butchery (屠宰)
   ├─ horn / sinew → Woodworking (composite_bow)
   ├─ goose_feather / down → Tailor (羽绒) / Metalworks (fletching)
   ├─ honey / beeswax (蜂箱) → Agri_Brew_Works / Crafts
   ├─ manure → Compost / Chemistry (硝床)
   └─ organic_waste → Compost
```

---

## 5) 危害与特殊

- **冬季饲喂检查**: 缺口警报 = 预测 X 天后干草不足
- **疾病**: 基础概率低；M 通风+粪槽 −30%；R 再 −25%
- **生产冷却**: 挤奶日清 / 剪毛年 1 次 / 收蛋按巢箱轮询
- **AI 行为**: 优先去最近饲槽/饮水槽；缺料自动派"补料/补水/清粪"作业
- **分群规则**: 每栏雄×1 + 雌×N；可启用"阉割"标记（在本工坊作业）

---

## 6) 与 industry md / 其他工坊的对应

- 详细演进 + 平衡: [Husbandry_Design_v1.md](../Husbandry_Design_v1.md)
- 屠宰 + tallow render: → [Butchery.md](Butchery.md)
- 皮 → 皮革: → [Tannery.md](Tannery.md)
- 毛 → 纺织: → [Tailor.md](Tailor.md)

（完）
