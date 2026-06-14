# Tailor 纺织与裁缝工坊

**对应 JSON**: `data/core/workshops/core_workshop_tailor.json`（17 attachments）
**对应 industry md**: [Textile_Industry_Research_and_Design_CN.md](../industries/Textile_Industry_Research_and_Design_CN.md)
**era**: C → R
**主要 tags**: workshop, clothing, textile, tailor

---

## 1) 用途与定位

把"纤维准备 → 纺 → 织 → 缩呢/漂洗 → 染整 → 裁缝"全链整合到一座工坊（多 slot 附件升级）。两条专线：
- **羊毛高端呢**（M 核心）: 洗毛 → 梳理 → 纺车 → 脚踏织机 → 水力缩呢 → 拉绒剪毛 → 染整
- **丝绸**（R 奢侈）: 蚕茧/生丝 → 丝车 → 整经 → 提花织机 → 染整

> 红色染料统一使用 **kermes**（地中海产）+ **茜草**；不开新大陆 cochineal（按用户决策，cochineal 已删除）。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `fiber_prep` | 纤维准备（洗/沤/梳）| Wash Trough / Retting Pit | Improved Comb & Wash | (升级) |
| `spinning` | 纺纱 | Hand Spindle | Spinning Wheel | (升级) |
| `weaving` | 织布 | Hand Loom | Treadle Floor Loom | Drawloom (提花，R 丝) |
| `fulling` | 缩呢 | Foot Fulling Tub | Water-Powered Fulling Mill | (升级) |
| `dyeing` | 染整 | Dye Vat (基础) | Mordant Vat + Indigo Tub | Refined Dye House |
| `bleaching` | 漂白 | Grass Bleaching | Alkali Bleaching | Improved Bleach Bath |
| `silk_throw` | 丝车（抛丝/合股）| — | — | Silk Filatoio |
| `tailoring_bench` | 裁缝台 | Tailoring Bench | Improved Tailoring Bench | Pattern + Shears Bench |
| `fishing_net_line` | 渔网编织 | — | Net Bench | (升级) |

> 渔网编织 fishing_net 是 Fishery 工坊的关键耗材；按用户决策 Fishery 不另建编网工坊，本工坊承担。

---

## 3) 配方索引（按 era / 链）

### 3.1 古典（C）

| 配方 | 输入 | 输出 |
|---|---|---|
| 羊毛清洗 | wool_fleece ×10 | washed_wool ×9 |
| 梳理 | washed_wool ×9 | combed_wool ×8 |
| 纺线（手锭）| combed_wool ×8 | wool_yarn ×8 |
| 手工织呢（坯）| wool_yarn ×8 | wool_cloth_raw ×6 |
| 徒手缩呢 + 整布 | wool_cloth_raw ×6 | wool_cloth ×5 |
| 亚麻处理 | flax_stem ×10 | flax_fiber ×6 → flax_yarn ×5 → linen ×5 |
| 基础染色 | wool_cloth/linen + 染料（茜/黄草/菘蓝）+ alum | dyed_cloth |

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 |
|---|---|---|
| 纺车纺线 | combed_wool ×8 | wool_yarn ×8（吞吐 +25%）@spinning M |
| 脚踏织机织呢 | wool_yarn ×8 | wool_cloth_raw ×7（吞吐 +20%）|
| **水力缩呢** | wool_cloth_raw ×7 | wool_cloth ×6（品质+1 标签，吞吐 +30%）|
| 拉绒/剪毛 | wool_cloth ×6 | wool_cloth_refined ×6（高档呢）|
| 福斯丁 fustian | flax_yarn ×5 + cotton_yarn ×5（贸易棉）| fustian ×8 |
| 染色（菘蓝/茜草/黄草 + alum）| 标准化生产 | dyed_cloth |
| 麻绳 / 渔网 | flax_fiber ×N | rope_hemp ×N → fishing_net / long_line @fishing_net_line |

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 |
|---|---|---|
| 丝车抛丝/合股 | silk_raw ×10 | silk_yarn ×8 @silk_throw |
| 整经 + 提花织机 | silk_yarn ×8 | silk_fabric ×6（奢侈标签 / 售价↑）@weaving R |
| Kermes 红呢（高档）| wool_cloth ×6 + alum ×1 + kermes ×1 | wool_cloth_kermes_red ×6（奢侈，售价↑）|
| 丝染（明矾+kermes/茜草）| silk_fabric + alum + kermes/茜草 | silk_fabric_dyed |
| 帆布 canvas (画家 R) | flax_yarn 密织 | canvas ×N（→ Crafts 油画）|

### 3.4 裁缝（C→R 通用）

| 配方 | 输入 | 输出 |
|---|---|---|
| 衣物 | wool_cloth / linen / fustian / silk_fabric + leather_scrap | shirt / trousers / robe / dress / coat |
| 羽绒被/枕 | down_goose / feather_bundle + canvas | quilted_bedding |
| 帆 / 旗帜 | canvas | sail / banner |
| 麻绳 → 帆船索具 | rope_hemp | rigging |

---

## 4) 上下游

```
[ Pasture_Shed ]
   ├─ wool_fleece → Tailor
   ├─ down / feather → Tailor (羽绒)
   └─ silk_raw (蚕茧 / 生丝) → Tailor (丝)

[ Agriculture ]
   ├─ flax_stem → Tailor (亚麻)
   ├─ hemp_stem → Tailor (麻)
   └─ cotton (贸易输入) → Tailor (福斯丁)

[ Chemistry_Lab ]
   ├─ alum_crystal → Tailor (媒染)
   ├─ woad_paste / madder_root / weld_yellow / kermes → Tailor (染料)
   └─ alkali_solution → Tailor (漂白)

[ Tannery ]
   └─ leather_scrap → Tailor (服装配件)

[ Fuel_Alkali_Works ]
   └─ wood_ash / potash → Tailor (染整漂白)

[ Tailor 输出 ]
   ├─ wool_cloth / linen / fustian / silk_fabric → 衣物
   ├─ canvas → Crafts (油画) / 帆船
   ├─ rope_hemp / fishing_net / long_line → Fishery
   ├─ wick_string → Crafts (蜡烛)
   ├─ clothing / robe / coat → 居民装备
   ├─ sail / banner → 海军 / 仪式
   ├─ quilted_bedding → 卧室
   └─ rigging → 船
```

---

## 5) 危害与特殊

- **染料废水**: 染整有"缩呢废水" → 事件链（环境 / 瘟疫）
- **副产**: 羊毛脂 → Chemistry / Crafts (肥皂材料)
- **吞吐**: 纺车 / 脚踏织机 / 水力缩呢是三个明显台阶
- **贸易标记**: cotton / kermes / 优质明矾标记为"贸易输入"

---

## 6) 与 industry md 的对应

详细演进 + 平衡 + 染料体系: [Textile_Industry_Research_and_Design_CN.md](../industries/Textile_Industry_Research_and_Design_CN.md)

（完）
