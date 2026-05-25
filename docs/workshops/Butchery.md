# Butchery & Meatworks 屠宰与肉品工坊（含 tallow 熬脂）

**对应 JSON**: `data/core/workshops/core_workshop_butchery.json`（15 attachments）
**对应 industry md**: [Husbandry_Design_v1.md](../Husbandry_Design_v1.md)（屠宰部分） + [Oil_Soap_Candle_Industry_Design.md](../Oil_Soap_Candle_Industry_Design.md)（tallow 部分）
**era**: C → R
**主要 tags**: workshop, food, butchery, meat

> 合并来源:
> - Husbandry md（屠宰章节）
> - Oil_Soap_Candle md（tallow 熬脂部分按用户决策并入此工坊）
> - `core_workshop_butchery.json`

---

## 1) 用途与定位

屠宰 + 分割 + 内脏处理 + **熬脂 tallow rendering** + 腊腌前处理。

**与 Kitchen 的边界**:
- Butchery: 从"活畜 / 鲜鱼"到"鲜肉/原皮/脂/骨/内脏"的初加工
- Kitchen: 从"鲜肉"到"炖/烤/腌/熏成品菜"
- 鲜肉短保；腊腌成品在 Kitchen 的 Saltery 完成

**与 Crafts_Lapidary 的边界**:
- Butchery: tallow 熬脂（出油）
- Crafts: tallow → candle 浸蘸
- Chemistry: tallow → soap（皂化）

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐 + 新增 tallow_render

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `slaughter_bench` | 屠宰台 | Slaughter Bench | Improved Bench (Drain) | Standardized Bench |
| `gambrel_rail` | 挂肉杆 | Wooden Gambrel | Iron Gambrel + Hoist | Pulley Hoist |
| `cleaving_block` | 分割砧 | Wood Block + Cleaver | Iron-Bound Block | Marble Bench + Iron Cleaver |
| `offal_pit` | 内脏处理 | Offal Pit | Vented Offal Pit | Composted Offal Bay |
| `chilling_room` | 冷藏（轻量）| — | Cold Cellar | Ice Cellar |
| `tallow_render` ⭐新 | 兽脂熬炼 | Rendering Pot | Iron Rendering Cauldron | Improved Rendering Stove |
| `salting_bench` | 干腌前处理 | Salting Bench | Salt Vat | Brine Tub & Curing Rack |
| `smokehouse_door` | 熏制（与 Kitchen 协作） | Smokehouse Door | (升级) | (升级) |

---

## 3) 配方索引（按 era / 链）

### 3.1 屠宰

| 配方 | 输入 | 输出 |
|---|---|---|
| 屠宰成畜 | adult_animal ×1 | meat ×K + raw_hide ×1 + raw_fat ×R + bone_bundle ×1 + offal ×1 + (horn / sinew per species) |
| 屠宰家禽 | poultry ×1 | meat_poultry ×k + feather_bundle ×1 + (down for goose) |
| 鱼内脏处理（Fishery 配合）| gutted_fish ×N | gutted_fish ×N（已去内脏，直接送 Kitchen）|

### 3.2 熬脂 tallow rendering（⭐新加 slot）

| 配方 | era | 输入 | 输出 | 工位 |
|---|---|---|---|---|
| 牛油熬炼 | C→R | raw_fat ×4（牛/羊）| tallow ×3 + cracklings ×1 | tallow_render |
| 猪油熬炼 | C→R | raw_fat ×4（猪）| lard ×3 + cracklings ×1 | tallow_render |
| 鱼油榨（来自 Fishery 选择）| M→R | fish_offal ×4 + bones | fish_oil ×1 + bone_meal ×1 | tallow_render（可选）|

**tallow / lard 下游**:
- → Kitchen 烹饪用油
- → Crafts 蜡烛
- → Chemistry 皂化
- → cracklings (副产) → Kitchen 零食 / 家禽饲料

### 3.3 内脏与副产

| 配方 | 输入 | 输出 |
|---|---|---|
| 内脏处理 | offal ×N | sausage_filling ×N（M+ 制肠衣） / animal_feed ×N |
| 骨胶 | bone_bundle ×1 | bone_glue ×1（蒸煮提胶） |
| 骨灰（→化学/冶炼）| bone_bundle ×3（高温烧）| bone_ash ×1 |
| 干腌前处理 | meat ×4 + salt_coarse ×1 | meat_pre_cured ×4（送 Kitchen 完成熏/腌成品） |

---

## 4) 上下游

```
[ Pasture_Shed ]
   └─ live animal → Butchery

[ Fishery ]
   └─ (可选) fish_offal → Butchery (鱼油提取，否则 Fishery 内做)

[ Salt_Works ]
   └─ salt_coarse → Butchery (干腌前处理)

[ Butchery 输出 ]
   ├─ meat → Kitchen
   ├─ meat_pre_cured → Kitchen (熏/腌成品)
   ├─ raw_hide → Tannery (核心)
   ├─ raw_skin_light (羊/小牛) → Tannery (白革)
   ├─ tallow → Kitchen (油) / Crafts (蜡烛) / Chemistry (soap)
   ├─ lard → Kitchen / Chemistry
   ├─ fish_oil → Chemistry / Crafts (灯油)
   ├─ cracklings → Kitchen / Pasture_Shed (饲料)
   ├─ bone_bundle → Crafts (骨工艺) / Chemistry (bone_glue, bone_ash)
   ├─ bone_glue → Woodworking (复合弓) / 装订
   ├─ bone_ash → Chemistry / Smeltery (杯灰)
   ├─ horn → Woodworking (composite bow) / Crafts
   ├─ sinew → Woodworking (sinew_string 弓弦)
   ├─ feather_bundle → Tailor (羽绒) / Metalworks (fletching)
   ├─ down (goose) → Tailor (羽绒)
   └─ offal → Pasture_Shed (家禽饲料) / Compost
```

---

## 5) 危害与特殊

- **stench 场域**: workshop beauty −2；建议远离居民区 + 通风
- **疾病事件**: 屠宰量大 + 未及时清理 offal_pit → 疾病概率↑；M 通风 / R 冷藏 −80%
- **血腥度**: workshop 周围有 "blood" 标签；与可视化系统接入
- **冷藏（M+）**: 鲜肉保质从短延到中

---

## 6) 与 industry md / 其他工坊的对应

- 屠宰演进: [Husbandry_Design_v1.md](../Husbandry_Design_v1.md)
- tallow → 蜡烛全链: [Oil_Soap_Candle_Industry_Design.md](../Oil_Soap_Candle_Industry_Design.md)
- 皂化反应在 Chemistry_Lab；蜡烛浸蘸在 Crafts_Lapidary
- 鱼内脏可在 Fishery 或 Butchery 处理（二选一）

（完）
